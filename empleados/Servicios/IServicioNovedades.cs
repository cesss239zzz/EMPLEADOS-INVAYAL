using empleados.Datos.Entidades;

namespace empleados.Servicios;

/// <summary>Datos para registrar una incidencia.</summary>
public sealed class DatosIncidencia
{
    public int ColaboradorId { get; set; }
    public TipoIncidencia Tipo { get; set; } = TipoIncidencia.Memorando;
    public DateTime Fecha { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

/// <summary>Datos para programar un periodo de vacaciones.</summary>
public sealed class DatosVacaciones
{
    public int ColaboradorId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string Observacion { get; set; } = string.Empty;
}

/// <summary>Una incidencia lista para mostrar en la ficha.</summary>
public sealed record LineaIncidencia(
    TipoIncidencia Tipo, DateTime Fecha, string Titulo, string Descripcion)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

    public string TipoTexto => Tipo switch
    {
        TipoIncidencia.Amonestacion => "Amonestacion",
        TipoIncidencia.Memorando => "Memorando",
        TipoIncidencia.Felicitacion => "Felicitacion",
        TipoIncidencia.Permiso => "Permiso",
        TipoIncidencia.Ausencia => "Ausencia",
        TipoIncidencia.Tardanza => "Tardanza",
        _ => "Incidencia"
    };
}

/// <summary>Un periodo de vacaciones listo para mostrar en la ficha.</summary>
public sealed record LineaVacacion(
    DateTime FechaInicio, DateTime FechaFin, int Dias, EstadoVacacion Estado, string Observacion)
{
    public string PeriodoTexto =>
        FechaInicio.ToString("dd/MM/yyyy") + "  →  " + FechaFin.ToString("dd/MM/yyyy");

    public string DiasTexto => Dias + (Dias == 1 ? " día" : " días");

    public string EstadoTexto => Estado switch
    {
        EstadoVacacion.Programada => "Programada",
        EstadoVacacion.Aprobada => "Aprobada",
        EstadoVacacion.Gozada => "Gozada",
        EstadoVacacion.Cancelada => "Cancelada",
        _ => "?"
    };
}

/// <summary>
/// Registro y consulta de novedades del expediente: incidencias (memorandos,
/// amonestaciones, permisos) y periodos de vacaciones. Toda escritura asigna la
/// empresa desde el contexto activo, nunca desde el formulario (regla 9).
/// </summary>
public interface IServicioNovedades
{
    Task<ResultadoGuardado> RegistrarIncidenciaAsync(
        DatosIncidencia datos, CancellationToken cancelacion = default);

    Task<ResultadoGuardado> ProgramarVacacionesAsync(
        DatosVacaciones datos, CancellationToken cancelacion = default);

    Task<IReadOnlyList<LineaIncidencia>> ObtenerIncidenciasAsync(
        int colaboradorId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<LineaVacacion>> ObtenerVacacionesAsync(
        int colaboradorId, CancellationToken cancelacion = default);
}
