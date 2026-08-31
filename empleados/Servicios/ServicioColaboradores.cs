using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioColaboradores : IServicioColaboradores
{
    /// <summary>Documentos que se consideran un expediente completo.</summary>
    private const int DocumentosParaExpedienteCompleto = 3;

    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioColaboradores> _registro;

    public ServicioColaboradores(
        IDbContextFactory<ContextoRhManager> fabrica,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        ILogger<ServicioColaboradores> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilaColaborador>> ObtenerAsync(
        FiltroColaboradores filtro,
        CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var puedeVerSalario = _sesion.PuedeVerSalarios;

        // El filtro se arma sobre IQueryable y viaja a SQL. Traer la tabla entera
        // para filtrarla en memoria esta prohibido (CLAUDE.md, regla 13): con 12
        // filas daria igual, con 4000 no.
        var consulta = contexto.Colaboradores.AsQueryable();

        if (filtro.DepartamentoId is { } departamento)
        {
            consulta = consulta.Where(c => c.DepartamentoId == departamento);
        }

        if (filtro.SucursalId is { } sucursal)
        {
            consulta = consulta.Where(c => c.SucursalId == sucursal);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(c => c.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // Se compara contra la columna plegada, no contra los nombres tal
            // como se escribieron. Asi "jose nunez" encuentra a "José Núñez"
            // (CR-08). Ambos lados pasan por la misma normalizacion, que es la
            // unica forma de que la comparacion sea simetrica.
            //
            // Un solo LIKE reemplaza a los seis de antes: TextoBusqueda ya
            // concentra nombres, apellidos, codigo e identidad.
            var patron = "%" + TextoBusqueda.Normalizar(filtro.Texto) + "%";

            consulta = consulta.Where(c => EF.Functions.Like(c.TextoBusqueda, patron));
        }

        var ordenada = consulta
            .OrderBy(c => c.PrimerApellido).ThenBy(c => c.PrimerNombre);

        // El tope viaja como LIMIT. Traer todo y quedarse con los primeros seria
        // exactamente lo que prohibe la regla 13.
        var acotada = filtro.Tope is > 0 ? ordenada.Take(filtro.Tope.Value) : ordenada.AsQueryable();

        var filas = await acotada
            .Select(c => new
            {
                c.Id,
                c.Codigo,
                c.PrimerNombre,
                c.SegundoNombre,
                c.PrimerApellido,
                c.SegundoApellido,
                c.Identidad,
                // Los tres catalogos son opcionales (CR-05), asi que la union es
                // externa y puede venir nula. Sin el condicional, EF traduciria un
                // acceso a null y la fila reventaria al proyectar.
                Puesto = c.Puesto == null ? null : c.Puesto.Nombre,
                Departamento = c.Departamento == null ? null : c.Departamento.Nombre,
                Sucursal = c.Sucursal == null ? null : c.Sucursal.Nombre,
                c.FechaIngreso,
                c.Estado,
                // El salario no se trae siquiera cuando el perfil no puede verlo:
                // ocultarlo solo en la vista dejaria el dato viajando igual.
                Salario = puedeVerSalario ? c.SalarioBase : null,
                Documentos = contexto.Documentos.Count(d => d.ColaboradorId == c.Id)
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        _registro.LogInformation(
            "Colaboradores de {Empresa}: {Cantidad} fila(s). Texto: {Texto}, departamento: {Departamento}, "
                + "sucursal: {Sucursal}, estado: {Estado}. Salario visible: {VeSalario}",
            _contextoEmpresa.NombreEmpresaActiva, filas.Count, filtro.Texto ?? "(sin texto)",
            filtro.DepartamentoId, filtro.SucursalId, filtro.Estado, puedeVerSalario);

        return filas.Select(f => new FilaColaborador(
            f.Id,
            f.Codigo,
            string.Join(' ', new[] { f.PrimerNombre, f.SegundoNombre, f.PrimerApellido, f.SegundoApellido }
                .Where(parte => !string.IsNullOrWhiteSpace(parte))),
            f.Identidad,
            f.Puesto,
            f.Departamento,
            f.Sucursal,
            f.FechaIngreso,
            f.Estado,
            f.Documentos >= DocumentosParaExpedienteCompleto ? EstadoExpediente.Completo
                : f.Documentos > 0 ? EstadoExpediente.Parcial
                : EstadoExpediente.Incompleto,
            f.Salario,
            puedeVerSalario)).ToList();
    }

    /// <inheritdoc />
    public async Task<OpcionesFiltro> ObtenerOpcionesAsync(CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Los catalogos tambien salen filtrados por empresa: el desplegable de una
        // empresa no puede ofrecer departamentos de la otra.
        var departamentos = await contexto.Departamentos
            .Where(d => d.Activo)
            .OrderBy(d => d.Nombre)
            .Select(d => new OpcionFiltro(d.Id, d.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var sucursales = await contexto.Sucursales
            .Where(s => s.Activa)
            .OrderBy(s => s.Nombre)
            .Select(s => new OpcionFiltro(s.Id, s.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var total = await contexto.Colaboradores.CountAsync(cancelacion).ConfigureAwait(false);

        departamentos.Insert(0, new OpcionFiltro(0, "Todos los departamentos"));
        sucursales.Insert(0, new OpcionFiltro(0, "Todas las sucursales"));

        return new OpcionesFiltro(departamentos, sucursales, total);
    }

    /// <inheritdoc />
    public async Task<CatalogosEdicion> ObtenerCatalogosEdicionAsync(CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Solo catalogos activos: un desplegable de alta no ofrece un puesto dado
        // de baja. El filtro global ya los acota a la empresa activa.
        var sucursales = await contexto.Sucursales
            .Where(s => s.Activa)
            .OrderBy(s => s.Nombre)
            .Select(s => new OpcionCatalogo(s.Id, s.Nombre, null))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var departamentos = await contexto.Departamentos
            .Where(d => d.Activo)
            .OrderBy(d => d.Nombre)
            .Select(d => new OpcionCatalogo(d.Id, d.Nombre, null))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // El puesto lleva su departamento solo como dato informativo. Ya NO se
        // usa para encadenar los desplegables: se puede elegir puesto sin haber
        // elegido departamento (solicitud de cambios, CR-05).
        var puestos = await contexto.Puestos
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new OpcionCatalogo(p.Id, p.Nombre, p.DepartamentoId))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new CatalogosEdicion(sucursales, departamentos, puestos);
    }

    /// <inheritdoc />
    public async Task<DatosEdicionColaborador?> ObtenerParaEdicionAsync(
        int colaboradorId,
        CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Sin Where por EmpresaId: si el id fuera de otra empresa, el filtro
        // global no lo encuentra. Ese es justamente el aislamiento (regla 9).
        var datos = await contexto.Colaboradores
            .Where(c => c.Id == colaboradorId)
            .Select(c => new DatosEdicionColaborador
            {
                Id = c.Id,
                Codigo = c.Codigo,
                Identidad = c.Identidad,
                PrimerNombre = c.PrimerNombre,
                SegundoNombre = c.SegundoNombre,
                PrimerApellido = c.PrimerApellido,
                SegundoApellido = c.SegundoApellido,
                Sexo = c.Sexo,
                FechaNacimiento = c.FechaNacimiento,
                FechaIngreso = c.FechaIngreso,
                Estado = c.Estado,
                Telefono = c.Telefono,
                Correo = c.Correo,
                Direccion = c.Direccion,
                SalarioBase = c.SalarioBase,
                SucursalId = c.SucursalId,
                DepartamentoId = c.DepartamentoId,
                PuestoId = c.PuestoId
            })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (datos is null)
        {
            _registro.LogWarning(
                "Se pidió para editar la ficha {Id}, que no existe en la empresa activa.", colaboradorId);
        }

        return datos;
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> GuardarAsync(
        DatosEdicionColaborador datos,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ExigirEmpresaActiva();

        if (!_sesion.PuedeCapturar)
        {
            // Defensa en profundidad: el boton ya se oculta para quien no puede,
            // pero el servicio no confia en la vista.
            _registro.LogWarning(
                "El perfil {Perfil} intentó capturar un colaborador sin permiso.", _sesion.Perfil);
            return ResultadoGuardado.Falla(
                "Su perfil no tiene permiso para dar de alta ni editar expedientes.");
        }

        var codigo = datos.Codigo.Trim();

        // Lo opcional vacio viaja como NULO, no como cadena vacia (CR-04).
        var identidad = Nulo(datos.Identidad);

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Unicidad dentro de la empresa (el filtro global acota a la activa),
        // excluyendo el propio registro al editar. Solo aplica cuando de verdad
        // hay identidad: varios expedientes pueden estar sin ella todavia.
        if (identidad is not null)
        {
            var identidadRepetida = await contexto.Colaboradores
                .AnyAsync(c => c.Identidad == identidad && c.Id != datos.Id, cancelacion)
                .ConfigureAwait(false);
            if (identidadRepetida)
            {
                return ResultadoGuardado.Falla(
                    "Ya existe un colaborador con la identidad " + identidad + " en esta empresa.");
            }
        }

        var codigoRepetido = await contexto.Colaboradores
            .AnyAsync(c => c.Codigo == codigo && c.Id != datos.Id, cancelacion)
            .ConfigureAwait(false);
        if (codigoRepetido)
        {
            return ResultadoGuardado.Falla(
                "Ya existe un colaborador con el código " + codigo + " en esta empresa.");
        }

        Colaborador colaborador;

        if (datos.EsAlta)
        {
            colaborador = new Colaborador
            {
                // La empresa se toma del contexto activo, nunca del formulario:
                // es la frontera de aislamiento (CLAUDE.md, regla 9).
                EmpresaId = _contextoEmpresa.EmpresaActivaId,
                FechaCreacion = DateTime.UtcNow
            };
            contexto.Colaboradores.Add(colaborador);
        }
        else
        {
            var existente = await contexto.Colaboradores
                .FirstOrDefaultAsync(c => c.Id == datos.Id, cancelacion)
                .ConfigureAwait(false);

            if (existente is null)
            {
                return ResultadoGuardado.Falla(
                    "El colaborador que intenta editar ya no está disponible en esta empresa.");
            }

            colaborador = existente;
        }

        colaborador.Codigo = codigo;
        colaborador.PrimerNombre = datos.PrimerNombre.Trim();
        colaborador.PrimerApellido = datos.PrimerApellido.Trim();
        colaborador.FechaIngreso = datos.FechaIngreso;
        colaborador.Estado = datos.Estado;

        // Todo lo opcional pasa por Nulo(): un campo que el usuario dejo en
        // blanco se guarda como NULO y no como "" (CR-04).
        colaborador.Identidad = identidad;
        colaborador.SegundoNombre = Nulo(datos.SegundoNombre);
        colaborador.SegundoApellido = Nulo(datos.SegundoApellido);
        colaborador.Sexo = datos.Sexo;
        colaborador.FechaNacimiento = datos.FechaNacimiento;
        colaborador.Telefono = Nulo(datos.Telefono);
        colaborador.Correo = Nulo(datos.Correo);
        colaborador.Direccion = Nulo(datos.Direccion);
        colaborador.SalarioBase = datos.SalarioBase;
        colaborador.SucursalId = datos.SucursalId;
        colaborador.DepartamentoId = datos.DepartamentoId;
        colaborador.PuestoId = datos.PuestoId;

        // La copia plegada se rehace SIEMPRE aqui, con los valores ya asignados:
        // si se calculara antes, una edicion dejaria el buscador apuntando al
        // nombre viejo (CR-08).
        colaborador.TextoBusqueda = Datos.TextoBusqueda.DeColaborador(
            colaborador.PrimerNombre,
            colaborador.SegundoNombre,
            colaborador.PrimerApellido,
            colaborador.SegundoApellido,
            colaborador.Codigo,
            colaborador.Identidad);

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Respaldo de la validacion de arriba: si dos altas ganan la carrera,
            // el indice unico corta la segunda. Nunca se muestra el ex crudo.
            _registro.LogWarning(ex, "Choque de índice único al guardar el colaborador {Codigo}.", codigo);
            return ResultadoGuardado.Falla(
                "No se pudo guardar: la identidad o el código ya existen en esta empresa.");
        }

        _registro.LogInformation(
            "Colaborador {Codigo} {Accion} en {Empresa} (id {Id}).",
            codigo, datos.EsAlta ? "dado de alta" : "actualizado",
            _contextoEmpresa.NombreEmpresaActiva, colaborador.Id);

        return ResultadoGuardado.Ok(colaborador.Id);
    }

    /// <summary>
    /// Recorta el texto y devuelve NULO si no quedo nada. Es la regla de CR-04
    /// en un solo lugar: un opcional en blanco se guarda como nulo, jamas como
    /// cadena vacia. Con "" en la base, "sin telefono" y "telefono vacio" se
    /// vuelven indistinguibles y los reportes salen con huecos raros.
    /// </summary>
    private static string? Nulo(string? valor)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio;
    }

    private void ExigirEmpresaActiva()
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException(
                "No se pueden consultar colaboradores sin empresa activa.");
        }
    }
}
