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
            Descripcion = datos.Descripcion.Trim(),
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

        if (datos.FechaFin.Date < datos.FechaInicio.Date)
        {
            return ResultadoGuardado.Falla("La fecha de fin no puede ser anterior a la de inicio.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var existe = await contexto.Colaboradores
            .AnyAsync(c => c.Id == datos.ColaboradorId, cancelacion).ConfigureAwait(false);
        if (!existe)
        {
            return ResultadoGuardado.Falla("El colaborador no está disponible en la empresa activa.");
        }

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
            Observacion = datos.Observacion.Trim(),
            RegistradaPorUsuarioId = _sesion.UsuarioId
        };

        contexto.Vacaciones.Add(vacacion);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

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
