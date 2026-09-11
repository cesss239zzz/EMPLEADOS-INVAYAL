using System.Globalization;
using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioCatalogos : IServicioCatalogos
{
    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioCatalogos> _registro;

    public ServicioCatalogos(
        IDbContextFactory<ContextoRhManager> fabrica,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        ILogger<ServicioCatalogos> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilaCatalogo>> ObtenerAsync(
        TipoCatalogo tipo,
        string? busqueda = null,
        bool incluirInactivos = true,
        CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var texto = (busqueda ?? string.Empty).Trim();

        return tipo switch
        {
            TipoCatalogo.Departamento =>
                await LeerDepartamentosAsync(contexto, texto, incluirInactivos, cancelacion).ConfigureAwait(false),
            TipoCatalogo.Puesto =>
                await LeerPuestosAsync(contexto, texto, incluirInactivos, cancelacion).ConfigureAwait(false),
            TipoCatalogo.Sucursal =>
                await LeerSucursalesAsync(contexto, texto, incluirInactivos, cancelacion).ConfigureAwait(false),
            TipoCatalogo.TipoDocumento =>
                await LeerTiposDocumentoAsync(contexto, texto, incluirInactivos, cancelacion).ConfigureAwait(false),
            TipoCatalogo.TipoContrato =>
                await LeerTiposContratoAsync(contexto, texto, incluirInactivos, cancelacion).ConfigureAwait(false),
            _ => Array.Empty<FilaCatalogo>()
        };
    }

    // ─── Lecturas por catalogo ──────────────────────────────────────────────
    //
    // Cada una proyecta en la base la cuenta de uso. Se resuelve alli y no en
    // memoria: traer los colaboradores enteros para contarlos seria justo lo que
    // prohibe la regla 13.

    private static async Task<IReadOnlyList<FilaCatalogo>> LeerDepartamentosAsync(
        ContextoRhManager contexto, string texto, bool incluirInactivos, CancellationToken cancelacion)
    {
        var consulta = contexto.Departamentos.AsQueryable();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(d => d.Activo);
        }

        if (texto.Length > 0)
        {
            consulta = consulta.Where(d => EF.Functions.Like(d.Nombre, "%" + texto + "%"));
        }

        var filas = await consulta
            .OrderBy(d => d.Nombre)
            .Select(d => new
            {
                d.Id,
                d.Nombre,
                d.Activo,
                Colaboradores = contexto.Colaboradores.Count(c => c.DepartamentoId == d.Id),
                Puestos = contexto.Puestos.Count(p => p.DepartamentoId == d.Id)
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas
            .Select(d => new FilaCatalogo(
                d.Id, d.Nombre, d.Activo,
                d.Colaboradores + d.Puestos,
                d.Puestos == 0 ? string.Empty : d.Puestos + " puesto(s) en este departamento"))
            .ToList();
    }

    private static async Task<IReadOnlyList<FilaCatalogo>> LeerPuestosAsync(
        ContextoRhManager contexto, string texto, bool incluirInactivos, CancellationToken cancelacion)
    {
        var consulta = contexto.Puestos.AsQueryable();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(p => p.Activo);
        }

        if (texto.Length > 0)
        {
            consulta = consulta.Where(p => EF.Functions.Like(p.Nombre, "%" + texto + "%"));
        }

        var filas = await consulta
            .OrderBy(p => p.Nombre)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Activo,
                Departamento = p.Departamento == null ? null : p.Departamento.Nombre,
                Colaboradores = contexto.Colaboradores.Count(c => c.PuestoId == p.Id),
                Movimientos = contexto.MovimientosLaborales
                    .Count(m => m.PuestoAnteriorId == p.Id || m.PuestoNuevoId == p.Id)
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas
            .Select(p => new FilaCatalogo(
                p.Id, p.Nombre, p.Activo,
                p.Colaboradores + p.Movimientos,
                string.IsNullOrWhiteSpace(p.Departamento) ? "Sin departamento" : p.Departamento))
            .ToList();
    }

    private static async Task<IReadOnlyList<FilaCatalogo>> LeerSucursalesAsync(
        ContextoRhManager contexto, string texto, bool incluirInactivos, CancellationToken cancelacion)
    {
        var consulta = contexto.Sucursales.AsQueryable();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(s => s.Activa);
        }

        if (texto.Length > 0)
        {
            consulta = consulta.Where(s =>
                EF.Functions.Like(s.Nombre, "%" + texto + "%")
                || EF.Functions.Like(s.Codigo, "%" + texto + "%"));
        }

        var filas = await consulta
            .OrderBy(s => s.Nombre)
            .Select(s => new
            {
                s.Id,
                s.Nombre,
                s.Codigo,
                s.Direccion,
                Activo = s.Activa,
                Colaboradores = contexto.Colaboradores.Count(c => c.SucursalId == s.Id),
                Movimientos = contexto.MovimientosLaborales
                    .Count(m => m.SucursalAnteriorId == s.Id || m.SucursalNuevaId == s.Id)
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas
            .Select(s => new FilaCatalogo(
                s.Id, s.Nombre, s.Activo,
                s.Colaboradores + s.Movimientos,
                string.IsNullOrWhiteSpace(s.Direccion)
                    ? "Código " + s.Codigo
                    : "Código " + s.Codigo + "  ·  " + s.Direccion))
            .ToList();
    }

    private static async Task<IReadOnlyList<FilaCatalogo>> LeerTiposDocumentoAsync(
        ContextoRhManager contexto, string texto, bool incluirInactivos, CancellationToken cancelacion)
    {
        var consulta = contexto.TiposDocumento.AsQueryable();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(t => t.Activo);
        }

        if (texto.Length > 0)
        {
            consulta = consulta.Where(t => EF.Functions.Like(t.Nombre, "%" + texto + "%"));
        }

        var filas = await consulta
            .OrderBy(t => t.Nombre)
            .Select(t => new
            {
                t.Id,
                t.Nombre,
                t.Activo,
                t.RequiereVencimiento,
                t.MesesVigencia,
                t.DiasAvisoAnticipado,
                Documentos = contexto.Documentos.Count(d => d.TipoDocumentoId == t.Id)
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas
            .Select(t => new FilaCatalogo(
                t.Id, t.Nombre, t.Activo, t.Documentos,
                DescribirVigencia(t.RequiereVencimiento, t.MesesVigencia, t.DiasAvisoAnticipado)))
            .ToList();
    }

    private static async Task<IReadOnlyList<FilaCatalogo>> LeerTiposContratoAsync(
        ContextoRhManager contexto, string texto, bool incluirInactivos, CancellationToken cancelacion)
    {
        var consulta = contexto.TiposContrato.AsQueryable();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(t => t.Activo);
        }

        if (texto.Length > 0)
        {
            consulta = consulta.Where(t => EF.Functions.Like(t.Nombre, "%" + texto + "%"));
        }

        var filas = await consulta
            .OrderBy(t => t.Nombre)
            .Select(t => new
            {
                t.Id,
                t.Nombre,
                t.Activo,
                t.RequiereVencimiento,
                Contratos = contexto.Contratos.Count(c => c.TipoContratoId == t.Id)
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas
            .Select(t => new FilaCatalogo(
                t.Id, t.Nombre, t.Activo, t.Contratos,
                t.RequiereVencimiento ? "Exige fecha de vencimiento" : "Sin vencimiento"))
            .ToList();
    }

    /// <summary>Texto legible de la vigencia de un tipo de documento.</summary>
    private static string DescribirVigencia(bool vence, int? meses, int diasAviso)
    {
        if (!vence)
        {
            return "Sin vencimiento";
        }

        var vigencia = meses switch
        {
            null or 0 => "Vigencia variable",
            12 => "Vigencia 1 año",
            var m when m % 12 == 0 => "Vigencia " + (m / 12) + " años",
            1 => "Vigencia 1 mes",
            var m => "Vigencia " + m + " meses"
        };

        return vigencia + "  ·  avisa " + diasAviso + " días antes";
    }

    /// <inheritdoc />
    public async Task<DatosCatalogo?> ObtenerParaEdicionAsync(
        TipoCatalogo tipo, int id, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        switch (tipo)
        {
            case TipoCatalogo.Departamento:
                return await contexto.Departamentos
                    .Where(d => d.Id == id)
                    .Select(d => new DatosCatalogo
                    {
                        Tipo = tipo, Id = d.Id, Nombre = d.Nombre, Activo = d.Activo
                    })
                    .FirstOrDefaultAsync(cancelacion).ConfigureAwait(false);

            case TipoCatalogo.Puesto:
                return await contexto.Puestos
                    .Where(p => p.Id == id)
                    .Select(p => new DatosCatalogo
                    {
                        Tipo = tipo, Id = p.Id, Nombre = p.Nombre, Activo = p.Activo,
                        DepartamentoId = p.DepartamentoId
                    })
                    .FirstOrDefaultAsync(cancelacion).ConfigureAwait(false);

            case TipoCatalogo.Sucursal:
                return await contexto.Sucursales
                    .Where(s => s.Id == id)
                    .Select(s => new DatosCatalogo
                    {
                        Tipo = tipo, Id = s.Id, Nombre = s.Nombre, Activo = s.Activa,
                        Codigo = s.Codigo, Direccion = s.Direccion, Telefono = s.Telefono
                    })
                    .FirstOrDefaultAsync(cancelacion).ConfigureAwait(false);

            case TipoCatalogo.TipoDocumento:
                return await contexto.TiposDocumento
                    .Where(t => t.Id == id)
                    .Select(t => new DatosCatalogo
                    {
                        Tipo = tipo, Id = t.Id, Nombre = t.Nombre, Activo = t.Activo,
                        RequiereVencimiento = t.RequiereVencimiento,
                        MesesVigencia = t.MesesVigencia,
                        DiasAvisoAnticipado = t.DiasAvisoAnticipado,
                        EscalaAviso = t.EscalaAviso
                    })
                    .FirstOrDefaultAsync(cancelacion).ConfigureAwait(false);

            case TipoCatalogo.TipoContrato:
                return await contexto.TiposContrato
                    .Where(t => t.Id == id)
                    .Select(t => new DatosCatalogo
                    {
                        Tipo = tipo, Id = t.Id, Nombre = t.Nombre, Activo = t.Activo,
                        RequiereVencimiento = t.RequiereVencimiento
                    })
                    .FirstOrDefaultAsync(cancelacion).ConfigureAwait(false);

            default:
                return null;
        }
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> GuardarAsync(
        DatosCatalogo datos, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ExigirEmpresaActiva();

        if (!_sesion.PuedeCapturar)
        {
            return ResultadoGuardado.Falla("Su perfil no tiene permiso para administrar catálogos.");
        }

        var nombre = (datos.Nombre ?? string.Empty).Trim();
        if (nombre.Length == 0)
        {
            return ResultadoGuardado.Falla("El nombre es obligatorio.");
        }

        var maximoNombre = datos.Tipo is TipoCatalogo.TipoDocumento or TipoCatalogo.TipoContrato ? 80 : 120;
        if (nombre.Length > maximoNombre)
        {
            return ResultadoGuardado.Falla("El nombre no puede pasar de " + maximoNombre + " caracteres.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);
        var empresaId = _contextoEmpresa.EmpresaActivaId;

        // El nombre repetido dentro del mismo catalogo se rechaza aca con un
        // mensaje entendible, en vez de dejar que choque el indice unico.
        if (await NombreRepetidoAsync(contexto, datos.Tipo, nombre, datos.Id, cancelacion).ConfigureAwait(false))
        {
            return ResultadoGuardado.Falla("Ya existe un valor con ese nombre en este catálogo.");
        }

        try
        {
            var id = datos.Tipo switch
            {
                TipoCatalogo.Departamento =>
                    await GuardarDepartamentoAsync(contexto, datos, nombre, empresaId, cancelacion).ConfigureAwait(false),
                TipoCatalogo.Puesto =>
                    await GuardarPuestoAsync(contexto, datos, nombre, empresaId, cancelacion).ConfigureAwait(false),
                TipoCatalogo.Sucursal =>
                    await GuardarSucursalAsync(contexto, datos, nombre, empresaId, cancelacion).ConfigureAwait(false),
                TipoCatalogo.TipoDocumento =>
                    await GuardarTipoDocumentoAsync(contexto, datos, nombre, empresaId, cancelacion).ConfigureAwait(false),
                TipoCatalogo.TipoContrato =>
                    await GuardarTipoContratoAsync(contexto, datos, nombre, empresaId, cancelacion).ConfigureAwait(false),
                _ => 0
            };

            if (id == 0)
            {
                return ResultadoGuardado.Falla("El valor ya no existe.");
            }

            if (id < 0)
            {
                // Los guardadores devuelven negativo con su propio motivo de
                // rechazo; se traduce en el llamador para no repetir mensajes.
                return ResultadoGuardado.Falla(MotivoRechazo(id));
            }

            _registro.LogInformation("Catálogo {Catalogo}: valor {Nombre} (id {Id}) {Accion} por {Usuario}.",
                datos.Tipo, nombre, id, datos.EsAlta ? "creado" : "modificado", _sesion.NombreUsuario);

            return ResultadoGuardado.Ok(id);
        }
        catch (DbUpdateException ex)
        {
            _registro.LogError(ex, "La base rechazo el guardado en el catálogo {Catalogo}.", datos.Tipo);
            return ResultadoGuardado.Falla("La base de datos rechazo el guardado.");
        }
    }

    private const int RechazoCodigoSucursal = -1;
    private const int RechazoCodigoRepetido = -2;
    private const int RechazoDepartamentoInvalido = -3;

    private static string MotivoRechazo(int codigo) => codigo switch
    {
        RechazoCodigoSucursal => "El código de la sucursal es obligatorio.",
        RechazoCodigoRepetido => "Ya existe otra sucursal con ese código.",
        RechazoDepartamentoInvalido => "El departamento elegido ya no está disponible.",
        _ => "No se pudo guardar el valor."
    };

    private static async Task<int> GuardarDepartamentoAsync(
        ContextoRhManager contexto, DatosCatalogo datos, string nombre, int empresaId, CancellationToken cancelacion)
    {
        Departamento entidad;

        if (datos.EsAlta)
        {
            entidad = new Departamento { EmpresaId = empresaId, FechaCreacion = DateTime.UtcNow };
            contexto.Departamentos.Add(entidad);
        }
        else
        {
            var hallado = await contexto.Departamentos
                .FirstOrDefaultAsync(d => d.Id == datos.Id, cancelacion).ConfigureAwait(false);
            if (hallado is null) { return 0; }
            entidad = hallado;
        }

        entidad.Nombre = nombre;
        entidad.Activo = datos.Activo;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        return entidad.Id;
    }

    private static async Task<int> GuardarPuestoAsync(
        ContextoRhManager contexto, DatosCatalogo datos, string nombre, int empresaId, CancellationToken cancelacion)
    {
        // El departamento es opcional, pero si viene uno tiene que existir y ser
        // de esta empresa. El filtro global se encarga de lo segundo.
        if (datos.DepartamentoId is { } departamentoId)
        {
            var existe = await contexto.Departamentos
                .AnyAsync(d => d.Id == departamentoId, cancelacion).ConfigureAwait(false);
            if (!existe) { return RechazoDepartamentoInvalido; }
        }

        Puesto entidad;

        if (datos.EsAlta)
        {
            entidad = new Puesto { EmpresaId = empresaId, FechaCreacion = DateTime.UtcNow };
            contexto.Puestos.Add(entidad);
        }
        else
        {
            var hallado = await contexto.Puestos
                .FirstOrDefaultAsync(p => p.Id == datos.Id, cancelacion).ConfigureAwait(false);
            if (hallado is null) { return 0; }
            entidad = hallado;
        }

        entidad.Nombre = nombre;
        entidad.Activo = datos.Activo;
        entidad.DepartamentoId = datos.DepartamentoId;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        return entidad.Id;
    }

    private static async Task<int> GuardarSucursalAsync(
        ContextoRhManager contexto, DatosCatalogo datos, string nombre, int empresaId, CancellationToken cancelacion)
    {
        var codigo = (datos.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (codigo.Length == 0) { return RechazoCodigoSucursal; }

        var repetido = await contexto.Sucursales
            .AnyAsync(s => s.Codigo == codigo && s.Id != datos.Id, cancelacion).ConfigureAwait(false);
        if (repetido) { return RechazoCodigoRepetido; }

        Sucursal entidad;

        if (datos.EsAlta)
        {
            entidad = new Sucursal { EmpresaId = empresaId, FechaCreacion = DateTime.UtcNow };
            contexto.Sucursales.Add(entidad);
        }
        else
        {
            var hallado = await contexto.Sucursales
                .FirstOrDefaultAsync(s => s.Id == datos.Id, cancelacion).ConfigureAwait(false);
            if (hallado is null) { return 0; }
            entidad = hallado;
        }

        entidad.Nombre = nombre;
        entidad.Codigo = codigo;
        entidad.Direccion = (datos.Direccion ?? string.Empty).Trim();
        entidad.Telefono = (datos.Telefono ?? string.Empty).Trim();
        entidad.Activa = datos.Activo;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        return entidad.Id;
    }

    private static async Task<int> GuardarTipoDocumentoAsync(
        ContextoRhManager contexto, DatosCatalogo datos, string nombre, int empresaId, CancellationToken cancelacion)
    {
        TipoDocumento entidad;

        if (datos.EsAlta)
        {
            entidad = new TipoDocumento { EmpresaId = empresaId, FechaCreacion = DateTime.UtcNow };
            contexto.TiposDocumento.Add(entidad);
        }
        else
        {
            var hallado = await contexto.TiposDocumento
                .FirstOrDefaultAsync(t => t.Id == datos.Id, cancelacion).ConfigureAwait(false);
            if (hallado is null) { return 0; }
            entidad = hallado;
        }

        entidad.Nombre = nombre;
        entidad.Activo = datos.Activo;
        entidad.RequiereVencimiento = datos.RequiereVencimiento;

        // Un tipo que no vence no arrastra vigencia: dejarla guardada seria un
        // dato muerto que reaparece si alguien vuelve a marcar la casilla.
        entidad.MesesVigencia = datos.RequiereVencimiento ? datos.MesesVigencia : null;
        entidad.DiasAvisoAnticipado = datos.DiasAvisoAnticipado <= 0 ? 30 : datos.DiasAvisoAnticipado;

        // Si la escala queda vacía, se usa el aviso único: la entidad ya sabe
        // caer en DiasAvisoAnticipado cuando no puede leerla (CR-10).
        entidad.EscalaAviso = string.IsNullOrWhiteSpace(datos.EscalaAviso)
            ? datos.DiasAvisoAnticipado.ToString(CultureInfo.InvariantCulture)
            : datos.EscalaAviso.Trim();

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        return entidad.Id;
    }

    private static async Task<int> GuardarTipoContratoAsync(
        ContextoRhManager contexto, DatosCatalogo datos, string nombre, int empresaId, CancellationToken cancelacion)
    {
        TipoContrato entidad;

        if (datos.EsAlta)
        {
            entidad = new TipoContrato { EmpresaId = empresaId, FechaCreacion = DateTime.UtcNow };
            contexto.TiposContrato.Add(entidad);
        }
        else
        {
            var hallado = await contexto.TiposContrato
                .FirstOrDefaultAsync(t => t.Id == datos.Id, cancelacion).ConfigureAwait(false);
            if (hallado is null) { return 0; }
            entidad = hallado;
        }

        entidad.Nombre = nombre;
        entidad.Activo = datos.Activo;
        entidad.RequiereVencimiento = datos.RequiereVencimiento;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        return entidad.Id;
    }

    private static Task<bool> NombreRepetidoAsync(
        ContextoRhManager contexto, TipoCatalogo tipo, string nombre, int id, CancellationToken cancelacion)
        => tipo switch
        {
            TipoCatalogo.Departamento => contexto.Departamentos
                .AnyAsync(x => x.Nombre == nombre && x.Id != id, cancelacion),
            TipoCatalogo.Puesto => contexto.Puestos
                .AnyAsync(x => x.Nombre == nombre && x.Id != id, cancelacion),
            TipoCatalogo.Sucursal => contexto.Sucursales
                .AnyAsync(x => x.Nombre == nombre && x.Id != id, cancelacion),
            TipoCatalogo.TipoDocumento => contexto.TiposDocumento
                .AnyAsync(x => x.Nombre == nombre && x.Id != id, cancelacion),
            TipoCatalogo.TipoContrato => contexto.TiposContrato
                .AnyAsync(x => x.Nombre == nombre && x.Id != id, cancelacion),
            _ => Task.FromResult(false)
        };

    /// <inheritdoc />
    public async Task<ResultadoGuardado> EliminarAsync(
        TipoCatalogo tipo, int id, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        if (!_sesion.PuedeCapturar)
        {
            return ResultadoGuardado.Falla("Su perfil no tiene permiso para administrar catálogos.");
        }

        // La cuenta de uso se vuelve a leer aca y no se confia en la que trajo la
        // pantalla: entre que se dibujo la tabla y se pulso el boton, alguien
        // pudo haber usado el valor.
        var filas = await ObtenerAsync(tipo, null, incluirInactivos: true, cancelacion).ConfigureAwait(false);
        var fila = filas.FirstOrDefault(f => f.Id == id);

        if (fila is null)
        {
            return ResultadoGuardado.Falla("El valor ya no existe.");
        }

        if (fila.EnUso > 0)
        {
            return ResultadoGuardado.Falla(
                "No se puede eliminar \"" + fila.Nombre + "\": lo usan " + fila.EnUso
                + " registro(s). Desactivelo en su lugar y dejara de ofrecerse en "
                + "nuevas selecciones, sin tocar los expedientes que ya lo tienen.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var borradas = tipo switch
        {
            TipoCatalogo.Departamento =>
                await contexto.Departamentos.Where(x => x.Id == id)
                    .ExecuteDeleteAsync(cancelacion).ConfigureAwait(false),
            TipoCatalogo.Puesto =>
                await contexto.Puestos.Where(x => x.Id == id)
                    .ExecuteDeleteAsync(cancelacion).ConfigureAwait(false),
            TipoCatalogo.Sucursal =>
                await contexto.Sucursales.Where(x => x.Id == id)
                    .ExecuteDeleteAsync(cancelacion).ConfigureAwait(false),
            TipoCatalogo.TipoDocumento =>
                await contexto.TiposDocumento.Where(x => x.Id == id)
                    .ExecuteDeleteAsync(cancelacion).ConfigureAwait(false),
            TipoCatalogo.TipoContrato =>
                await contexto.TiposContrato.Where(x => x.Id == id)
                    .ExecuteDeleteAsync(cancelacion).ConfigureAwait(false),
            _ => 0
        };

        if (borradas == 0)
        {
            return ResultadoGuardado.Falla("El valor ya no existe.");
        }

        _registro.LogInformation("Catálogo {Catalogo}: valor {Nombre} (id {Id}) eliminado por {Usuario}.",
            tipo, fila.Nombre, id, _sesion.NombreUsuario);

        return ResultadoGuardado.Ok(id);
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> CambiarActivoAsync(
        TipoCatalogo tipo, int id, bool activo, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        if (!_sesion.PuedeCapturar)
        {
            return ResultadoGuardado.Falla("Su perfil no tiene permiso para administrar catálogos.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var afectadas = tipo switch
        {
            TipoCatalogo.Departamento =>
                await contexto.Departamentos.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, activo), cancelacion).ConfigureAwait(false),
            TipoCatalogo.Puesto =>
                await contexto.Puestos.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, activo), cancelacion).ConfigureAwait(false),
            TipoCatalogo.Sucursal =>
                await contexto.Sucursales.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Activa, activo), cancelacion).ConfigureAwait(false),
            TipoCatalogo.TipoDocumento =>
                await contexto.TiposDocumento.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, activo), cancelacion).ConfigureAwait(false),
            TipoCatalogo.TipoContrato =>
                await contexto.TiposContrato.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, activo), cancelacion).ConfigureAwait(false),
            _ => 0
        };

        if (afectadas == 0)
        {
            return ResultadoGuardado.Falla("El valor ya no existe.");
        }

        _registro.LogInformation("Catálogo {Catalogo}: valor id {Id} {Accion} por {Usuario}.",
            tipo, id, activo ? "activado" : "desactivado", _sesion.NombreUsuario);

        return ResultadoGuardado.Ok(id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OpcionCatalogo>> ObtenerDepartamentosActivosAsync(
        CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        return await contexto.Departamentos
            .Where(d => d.Activo)
            .OrderBy(d => d.Nombre)
            .Select(d => new OpcionCatalogo(d.Id, d.Nombre, null))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    private void ExigirEmpresaActiva()
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException("No se pueden administrar catálogos sin empresa activa.");
        }
    }
}
