using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioNovedades : IServicioNovedades
{
    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioNovedades> _registro;

    public ServicioNovedades(
        IDbContextFactory<ContextoRhManager> fabrica,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        ILogger<ServicioNovedades> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> RegistrarIncidenciaAsync(
        DatosIncidencia datos, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ExigirEmpresaActiva();

        if (!_sesion.PuedeCapturar)
        {
            return ResultadoGuardado.Falla("Su perfil no tiene permiso para registrar incidencias.");
        }

        if (string.IsNullOrWhiteSpace(datos.Titulo))
        {
            return ResultadoGuardado.Falla("El título de la incidencia es obligatorio.");
        }

        if (!Enum.IsDefined(datos.Tipo) || datos.Fecha == default || datos.Fecha.Date > DateTime.Today)
            return ResultadoGuardado.Falla("Seleccione un tipo y una fecha de incidencia válidos, no futura.");
        if (datos.Titulo.Trim().Length > 160 || (datos.Descripcion?.Length ?? 0) > 1000)
            return ResultadoGuardado.Falla("El título admite 160 caracteres y la descripción 1000.");

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // El colaborador debe existir en la empresa activa; el filtro global se
        // encarga de que un id de otra empresa no aparezca.
        var existe = await contexto.Colaboradores
            .AnyAsync(c => c.Id == datos.ColaboradorId, cancelacion).ConfigureAwait(false);
        if (!existe)
        {
            return ResultadoGuardado.Falla("El colaborador no está disponible en la empresa activa.");
        }

        var incidencia = new Incidencia
        {
            EmpresaId = _contextoEmpresa.EmpresaActivaId,
            FechaCreacion = DateTime.UtcNow,
            ColaboradorId = datos.ColaboradorId,
            Tipo = datos.Tipo,
            Fecha = datos.Fecha,
            Titulo = datos.Titulo.Trim(),
            Descripcion = datos.Descripcion?.Trim() ?? string.Empty,
            RegistradaPorUsuarioId = _sesion.UsuarioId
        };

        contexto.Incidencias.Add(incidencia);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Incidencia {Tipo} registrada para colaborador {Colaborador} (id {Id}).",
            datos.Tipo, datos.ColaboradorId, incidencia.Id);

        return ResultadoGuardado.Ok(incidencia.Id);
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> ProgramarVacacionesAsync(
        DatosVacaciones datos, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ExigirEmpresaActiva();

        if (!_sesion.PuedeCapturar)
        {
            return ResultadoGuardado.Falla("Su perfil no tiene permiso para programar vacaciones.");
        }

        if (datos.FechaInicio == default || datos.FechaFin == default || datos.FechaFin.Date < datos.FechaInicio.Date)
            return ResultadoGuardado.Falla("Seleccione un período válido: el fin no puede ser anterior al inicio.");
        if ((datos.Observacion?.Length ?? 0) > 1000)
            return ResultadoGuardado.Falla("La observación admite hasta 1000 caracteres.");

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);
        // La comprobación de cruces y el INSERT forman una única transacción.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);
        var persona = await contexto.Colaboradores
            .Where(c => c.Id == datos.ColaboradorId)
            .Select(c => new { c.Estado, c.FechaIngreso })
            .FirstOrDefaultAsync(cancelacion).ConfigureAwait(false);
        if (persona is null)
            return ResultadoGuardado.Falla("El colaborador no está disponible en la empresa activa.");
        if (persona.Estado != EstadoColaborador.Activo)
            return ResultadoGuardado.Falla("Solo se pueden programar vacaciones para colaboradores activos.");
        if (datos.FechaInicio.Date < persona.FechaIngreso.Date)
            return ResultadoGuardado.Falla("Las vacaciones no pueden empezar antes del ingreso.");
        var inicio = datos.FechaInicio.Date;
        var fin = datos.FechaFin.Date;
        if (await contexto.Vacaciones.AnyAsync(v => v.ColaboradorId == datos.ColaboradorId
                && v.Estado != EstadoVacacion.Cancelada
                && v.FechaInicio <= fin && v.FechaFin >= inicio, cancelacion).ConfigureAwait(false))
            return ResultadoGuardado.Falla("El período se cruza con vacaciones ya registradas para este colaborador.");

        // Dias calendario, extremos incluidos: se guardan para no recalcular en
        // cada lectura.
        var dias = (datos.FechaFin.Date - datos.FechaInicio.Date).Days + 1;

        var vacacion = new Vacacion
        {
            EmpresaId = _contextoEmpresa.EmpresaActivaId,
            FechaCreacion = DateTime.UtcNow,
            ColaboradorId = datos.ColaboradorId,
            FechaInicio = datos.FechaInicio.Date,
            FechaFin = datos.FechaFin.Date,
            Dias = dias,
            Estado = EstadoVacacion.Programada,
            Observacion = datos.Observacion?.Trim() ?? string.Empty,
            RegistradaPorUsuarioId = _sesion.UsuarioId
        };

        contexto.Vacaciones.Add(vacacion);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Vacaciones programadas para colaborador {Colaborador} ({Dias} días, id {Id}).",
            datos.ColaboradorId, dias, vacacion.Id);

        return ResultadoGuardado.Ok(vacacion.Id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LineaIncidencia>> ObtenerIncidenciasAsync(
        int colaboradorId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        return await contexto.Incidencias
            .Where(i => i.ColaboradorId == colaboradorId)
            .OrderByDescending(i => i.Fecha)
            .Select(i => new LineaIncidencia(i.Tipo, i.Fecha, i.Titulo, i.Descripcion))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LineaVacacion>> ObtenerVacacionesAsync(
        int colaboradorId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        return await contexto.Vacaciones
            .Where(v => v.ColaboradorId == colaboradorId)
            .OrderByDescending(v => v.FechaInicio)
            .Select(v => new LineaVacacion(v.FechaInicio, v.FechaFin, v.Dias, v.Estado, v.Observacion))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    private void ExigirEmpresaActiva()
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException("No se pueden registrar novedades sin empresa activa.");
        }
    }
}
