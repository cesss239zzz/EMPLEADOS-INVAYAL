using empleados.Configuracion;
using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioEmpresas : IServicioEmpresas
{
    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioEmpresas> _registro;

    public ServicioEmpresas(
        IDbContextFactory<ContextoRhManager> fabrica,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        ILogger<ServicioEmpresas> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TarjetaEmpresa>> ObtenerDisponiblesAsync(CancellationToken cancelacion = default)
    {
        if (!_sesion.EstaAutenticado)
        {
            throw new InvalidOperationException(
                "No se pueden listar empresas sin una sesión iniciada.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // El SuperAdministrador atraviesa empresas por perfil, tal como declara
        // la entidad Usuario: ve todas, incluidas las que acaba de crear. Los
        // demas perfiles solo ven aquellas para las que hay fila en
        // usuario_empresa. Sin esta distincion, un sistema recien instalado
        // —que no tiene ninguna empresa ni ninguna habilitacion— dejaria al
        // administrador inicial sin forma de crear la primera (CR-01).
        var consulta = contexto.Empresas.Where(e => e.Activa);

        if (_sesion.Perfil != PerfilUsuario.SuperAdministrador)
        {
            var permitidas = await contexto.UsuariosEmpresas
                .Where(ue => ue.UsuarioId == _sesion.UsuarioId)
                .Select(ue => ue.EmpresaId)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            if (permitidas.Count == 0)
            {
                _registro.LogWarning("El usuario {Usuario} no tiene ninguna empresa habilitada.",
                    _sesion.NombreUsuario);
                return Array.Empty<TarjetaEmpresa>();
            }

            consulta = consulta.Where(e => permitidas.Contains(e.Id));
        }

        // Empresa no lleva filtro global (ella ES el inquilino). Los conteos se
        // resuelven en la base con IgnoreQueryFilters, porque en este punto
        // todavia no hay empresa activa y el filtro global dejaria todo en cero.
        var empresas = await consulta
            .OrderBy(e => e.Nombre)
            .Select(e => new
            {
                e.Id,
                e.Nombre,
                e.NombreCorto,
                e.Rtn,
                e.ColorPrimario,
                Colaboradores = contexto.Colaboradores
                    .IgnoreQueryFilters()
                    .Count(c => c.EmpresaId == e.Id && c.Estado == EstadoColaborador.Activo
                        && (_sesion.Perfil != PerfilUsuario.SupervisorSucursal
                            || (_sesion.SucursalId != null && c.SucursalId == _sesion.SucursalId))),
                Sucursales = contexto.Sucursales
                    .IgnoreQueryFilters()
                    .Count(s => s.EmpresaId == e.Id && s.Activa
                        && (_sesion.Perfil != PerfilUsuario.SupervisorSucursal
                            || (_sesion.SucursalId != null && s.Id == _sesion.SucursalId)))
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        _registro.LogInformation("El usuario {Usuario} tiene {Cantidad} empresa(s) disponibles.",
            _sesion.NombreUsuario, empresas.Count);

        return empresas
            .Select(e => new TarjetaEmpresa(e.Id, e.Nombre, e.NombreCorto, e.Rtn,
                e.ColorPrimario, e.Colaboradores, e.Sucursales))
            .ToList();
    }

    /// <inheritdoc />
    public async Task ActivarAsync(int empresaId, CancellationToken cancelacion = default)
    {
        if (!_sesion.EstaAutenticado)
        {
            throw new InvalidOperationException("No se puede activar una empresa sin sesión iniciada.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Se vuelve a comprobar el permiso contra la base. Confiar en que la
        // pantalla solo ofrecio empresas validas seria confiar en la interfaz
        // para algo que es control de acceso.
        if (!await TieneAccesoAsync(contexto, empresaId, cancelacion).ConfigureAwait(false))
        {
            _registro.LogError("El usuario {Usuario} intentó activar la empresa {Empresa}, que no tiene habilitada.",
                _sesion.NombreUsuario, empresaId);

            throw new InvalidOperationException("El usuario no tiene acceso a esa empresa.");
        }

        var empresa = await contexto.Empresas
            .FirstOrDefaultAsync(e => e.Id == empresaId && e.Activa, cancelacion)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("La empresa no existe o está desactivada.");

        _contextoEmpresa.Establecer(empresa.Id, empresa.NombreCorto, empresa.ColorPrimario);

        _registro.LogInformation("Empresa activa: {Empresa} (id {Id}). El filtro global ya apunta ahí.",
            empresa.NombreCorto, empresa.Id);
    }

    /// <inheritdoc />
    public async Task<DatosEmpresa?> ObtenerParaEdicionAsync(int empresaId, CancellationToken cancelacion = default)
    {
        if (!_sesion.EstaAutenticado)
        {
            throw new InvalidOperationException("No se puede consultar una empresa sin sesión iniciada.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        if (!await TieneAccesoAsync(contexto, empresaId, cancelacion).ConfigureAwait(false))
        {
            return null;
        }

        return await contexto.Empresas
            .Where(e => e.Id == empresaId && e.Activa)
            .Select(e => new DatosEmpresa
            {
                Id = e.Id,
                Nombre = e.Nombre,
                NombreCorto = e.NombreCorto,
                Rtn = e.Rtn,
                Direccion = e.Direccion,
                Telefono = e.Telefono,
                Correo = e.Correo,
                ColorPrimario = e.ColorPrimario
            })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> GuardarAsync(DatosEmpresa datos, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (!_sesion.EstaAutenticado)
        {
            throw new InvalidOperationException("No se puede guardar una empresa sin sesión iniciada.");
        }

        // Crear o modificar una empresa es administracion del sistema, no captura
        // corriente: se reserva al SuperAdministrador y se comprueba aca, porque
        // es control de acceso y no una comodidad de la pantalla.
        if (_sesion.Perfil != PerfilUsuario.SuperAdministrador)
        {
            return ResultadoGuardado.Falla("Solo el Super Administrador puede crear o modificar empresas.");
        }

        var (valido, error) = Validar(datos);
        if (!valido)
        {
            return ResultadoGuardado.Falla(error);
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var rtn = datos.Rtn.Trim();

        // El RTN lleva indice unico. Se comprueba antes para poder dar un mensaje
        // entendible en vez de dejar que reviente la restriccion de la base.
        var rtnRepetido = await contexto.Empresas
            .AnyAsync(e => e.Rtn == rtn && e.Id != datos.Id, cancelacion)
            .ConfigureAwait(false);

        if (rtnRepetido)
        {
            return ResultadoGuardado.Falla("Ya existe otra empresa registrada con ese RTN.");
        }

        Empresa empresa;

        if (datos.EsAlta)
        {
            empresa = new Empresa { FechaCreacion = DateTime.UtcNow, Activa = true };
            contexto.Empresas.Add(empresa);
        }
        else
        {
            var encontrada = await contexto.Empresas
                .FirstOrDefaultAsync(e => e.Id == datos.Id, cancelacion)
                .ConfigureAwait(false);

            if (encontrada is null)
            {
                return ResultadoGuardado.Falla("La empresa ya no existe.");
            }

            empresa = encontrada;
        }

        empresa.Nombre = datos.Nombre.Trim();
        empresa.NombreCorto = datos.NombreCorto.Trim();
        empresa.Rtn = rtn;
        empresa.Direccion = datos.Direccion.Trim();
        empresa.Telefono = datos.Telefono.Trim();
        empresa.Correo = datos.Correo.Trim();
        empresa.ColorPrimario = NormalizarColor(datos.ColorPrimario);

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _registro.LogError(ex, "La base rechazo el guardado de la empresa {Nombre}.", empresa.Nombre);
            return ResultadoGuardado.Falla("La base de datos rechazo el guardado de la empresa.");
        }

        // El alta habilita al creador sobre la empresa nueva. El
        // SuperAdministrador la veria igual por perfil, pero la fila deja
        // constancia y sirve cuando mas adelante se le cambie el perfil.
        if (datos.EsAlta)
        {
            var yaHabilitado = await contexto.UsuariosEmpresas
                .AnyAsync(ue => ue.UsuarioId == _sesion.UsuarioId && ue.EmpresaId == empresa.Id, cancelacion)
                .ConfigureAwait(false);

            if (!yaHabilitado)
            {
                contexto.UsuariosEmpresas.Add(new UsuarioEmpresa
                {
                    UsuarioId = _sesion.UsuarioId,
                    EmpresaId = empresa.Id,
                    FechaCreacion = DateTime.UtcNow
                });

                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            }
        }

        _registro.LogInformation("Empresa {Nombre} (id {Id}) {Accion} por {Usuario}.",
            empresa.NombreCorto, empresa.Id, datos.EsAlta ? "creada" : "modificada", _sesion.NombreUsuario);

        return ResultadoGuardado.Ok(empresa.Id);
    }

    /// <inheritdoc />
    public async Task<ResumenBorradoEmpresa?> ResumirBorradoAsync(
        int empresaId, CancellationToken cancelacion = default)
    {
        if (!_sesion.EstaAutenticado)
        {
            throw new InvalidOperationException("No se puede consultar una empresa sin sesión iniciada.");
        }

        if (_sesion.Perfil != PerfilUsuario.SuperAdministrador)
            return null;

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var empresa = await contexto.Empresas
            .FirstOrDefaultAsync(e => e.Id == empresaId, cancelacion)
            .ConfigureAwait(false);

        if (empresa is null)
        {
            return null;
        }

        // IgnoreQueryFilters con EmpresaId explicito: es una operacion de
        // administracion sobre una empresa que NO es la activa, asi que el filtro
        // global la dejaria en cero y la advertencia mentiria. Es la excepcion
        // deliberada a la regla 9, no un Where olvidado en un repositorio.
        return new ResumenBorradoEmpresa(
            empresa.Nombre,
            await contexto.Sucursales.IgnoreQueryFilters()
                .CountAsync(x => x.EmpresaId == empresaId, cancelacion).ConfigureAwait(false),
            await contexto.Colaboradores.IgnoreQueryFilters()
                .CountAsync(x => x.EmpresaId == empresaId, cancelacion).ConfigureAwait(false),
            await contexto.Documentos.IgnoreQueryFilters()
                .CountAsync(x => x.EmpresaId == empresaId, cancelacion).ConfigureAwait(false),
            await contexto.Contratos.IgnoreQueryFilters()
                .CountAsync(x => x.EmpresaId == empresaId, cancelacion).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> EliminarAsync(int empresaId, CancellationToken cancelacion = default)
    {
        if (!_sesion.EstaAutenticado)
        {
            throw new InvalidOperationException("No se puede eliminar una empresa sin sesión iniciada.");
        }

        // Accion critica: reservada al SuperAdministrador. Es control de acceso,
        // no una comodidad de interfaz, asi que se comprueba tambien aca.
        if (_sesion.Perfil != PerfilUsuario.SuperAdministrador)
        {
            return ResultadoGuardado.Falla("Solo el Super Administrador puede eliminar empresas.");
        }

        // No se elimina la empresa que esta activa: dejaria el filtro global
        // apuntando a una empresa que ya no existe.
        if (_contextoEmpresa.HayEmpresaActiva && _contextoEmpresa.EmpresaActivaId == empresaId)
        {
            return ResultadoGuardado.Falla(
                "No puede eliminar la empresa que está abierta. Salga de ella primero.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var empresa = await contexto.Empresas
            .FirstOrDefaultAsync(e => e.Id == empresaId, cancelacion)
            .ConfigureAwait(false);

        if (empresa is null)
        {
            return ResultadoGuardado.Falla("La empresa no existe o ya fue eliminada.");
        }

        var nombre = empresa.NombreCorto;

        // Borrado definitivo, en transaccion. Ya no hay baja logica ni regla de
        // "al menos una empresa": el cliente pidio poder dejar el sistema en cero
        // (solicitud de cambios, CR-02). La advertencia y la confirmacion escrita
        // ocurren antes, en el ViewModel.
        //
        // El orden importa: las claves foraneas de colaborador hacia los catalogos
        // son Restrict, asi que los hijos se borran antes que los padres. Aqui se
        // usa IgnoreQueryFilters con EmpresaId explicito por la misma razon que en
        // ResumirBorradoAsync: la empresa que se borra no es la activa.
        await using var transaccion = await contexto.Database
            .BeginTransactionAsync(cancelacion).ConfigureAwait(false);

        try
        {
            await contexto.MovimientosDocumento.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Avisos.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Documentos.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Vacaciones.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Incidencias.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Contratos.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.MovimientosLaborales.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.ContactosEmergencia.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Colaboradores.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Puestos.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Departamentos.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.TiposContrato.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.TiposDocumento.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Sucursales.IgnoreQueryFilters()
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.UsuariosEmpresas
                .Where(x => x.EmpresaId == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);
            await contexto.Empresas
                .Where(x => x.Id == empresaId).ExecuteDeleteAsync(cancelacion).ConfigureAwait(false);

            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await transaccion.RollbackAsync(cancelacion).ConfigureAwait(false);
            _registro.LogError(ex, "Falló el borrado de la empresa {Empresa} (id {Id}).", nombre, empresaId);
            return ResultadoGuardado.Falla("No se pudo eliminar la empresa. No se borro nada.");
        }

        // Los archivos adjuntos se van con la empresa. Se hace despues de confirmar
        // la transaccion: si la base fallara, los archivos seguirian haciendo falta.
        BorrarCarpetaDocumentos(empresaId);

        _registro.LogWarning("Empresa {Empresa} (id {Id}) ELIMINADA definitivamente por {Usuario}.",
            nombre, empresaId, _sesion.NombreUsuario);

        return ResultadoGuardado.Ok(empresaId);
    }

    /// <summary>El SuperAdministrador entra a cualquier empresa; los demas, solo a las habilitadas.</summary>
    private async Task<bool> TieneAccesoAsync(
        ContextoRhManager contexto, int empresaId, CancellationToken cancelacion)
    {
        if (_sesion.Perfil == PerfilUsuario.SuperAdministrador)
        {
            return true;
        }

        return await contexto.UsuariosEmpresas
            .AnyAsync(ue => ue.UsuarioId == _sesion.UsuarioId && ue.EmpresaId == empresaId, cancelacion)
            .ConfigureAwait(false);
    }

    private static (bool valido, string error) Validar(DatosEmpresa datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            return (false, "La razón social es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(datos.NombreCorto))
        {
            return (false, "El nombre corto es obligatorio: es el que se ve en la barra superior.");
        }

        if (string.IsNullOrWhiteSpace(datos.Rtn))
        {
            return (false, "El RTN es obligatorio.");
        }

        if (datos.Nombre.Trim().Length > 160)
        {
            return (false, "La razón social no puede pasar de 160 caracteres.");
        }

        if (datos.NombreCorto.Trim().Length > 60)
        {
            return (false, "El nombre corto no puede pasar de 60 caracteres.");
        }

        if (datos.Rtn.Trim().Length > 20)
        {
            return (false, "El RTN no puede pasar de 20 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(datos.Correo) && !datos.Correo.Contains('@'))
        {
            return (false, "El correo electronico no tiene un formato valido.");
        }

        return (true, string.Empty);
    }

    /// <summary>Deja el color en formato #RRGGBB, o el azul por omision si no lo esta.</summary>
    private static string NormalizarColor(string color)
    {
        var limpio = (color ?? string.Empty).Trim();

        if (limpio.Length == 7
            && limpio[0] == '#'
            && limpio[1..].All(Uri.IsHexDigit))
        {
            return limpio.ToUpperInvariant();
        }

        return "#0F3D6E";
    }

    /// <summary>Borra la carpeta de documentos de la empresa. De mejor esfuerzo.</summary>
    private void BorrarCarpetaDocumentos(int empresaId)
    {
        try
        {
            var carpeta = Path.Combine(RutasRhManager.CarpetaDocumentos, empresaId.ToString());
            if (Directory.Exists(carpeta))
            {
                Directory.Delete(carpeta, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Los registros de la base ya no existen: dejar archivos sueltos es
            // desprolijo, pero no rompe nada y no justifica fallar la operacion.
            _registro.LogWarning(ex,
                "La empresa {Id} se eliminó, pero su carpeta de documentos no se pudo borrar.", empresaId);
        }
    }
}
