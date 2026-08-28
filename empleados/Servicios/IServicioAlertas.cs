using empleados.Datos.Entidades;

namespace empleados.Servicios;

/// <summary>Un aviso listo para el panel de pendientes.</summary>
public sealed record LineaAviso(
    int Id,
    TipoAviso Tipo,
    EstadoAviso Estado,
    string Titulo,
    string Descripcion,
    DateTime FechaReferencia,
    string Colaborador)
{
    public string FechaTexto => FechaReferencia.ToString("dd/MM/yyyy");

    /// <summary>Dias que faltan para el hecho. Negativo si ya paso.</summary>
    public int DiasRestantes => (int)(FechaReferencia.Date - DateTime.Today).TotalDays;

    /// <summary>Prioridad: lo vencido es critico, lo proximo es advertencia.</summary>
    public bool EsCritico => DiasRestantes < 0;

    public bool EsAdvertencia => DiasRestantes is >= 0 and <= 15;

    public string PlazoTexto => DiasRestantes switch
    {
        < 0 => "Vencido hace " + Math.Abs(DiasRestantes) + " dias",
        0 => "Es hoy",
        1 => "Manana",
        _ => "En " + DiasRestantes + " dias"
    };

    public string TipoTexto => Tipo switch
    {
        TipoAviso.VencimientoContrato => "Contrato",
        TipoAviso.VencimientoDocumento => "Documento",
        TipoAviso.Cumpleanos => "Cumpleanos",
        TipoAviso.AniversarioLaboral => "Aniversario",
        TipoAviso.FinPeriodoPrueba => "Periodo de prueba",
        _ => "Aviso"
    };
}

/// <summary>Resultado de una corrida del motor.</summary>
/// <param name="Generados">Avisos nuevos insertados en esta corrida.</param>
/// <param name="YaExistian">Avisos que el motor detecto pero ya estaban.</param>
/// <param name="Pendientes">Total de avisos pendientes tras la corrida.</param>
public sealed record ResultadoMotor(int Generados, int YaExistian, int Pendientes);

/// <summary>
/// Motor de alertas. Calcula los avisos de la empresa activa y los guarda una
/// sola vez: correrlo dos veces no duplica nada.
/// </summary>
public interface IServicioAlertas
{
    /// <summary>Recalcula los avisos de la empresa activa.</summary>
    Task<ResultadoMotor> GenerarAsync(CancellationToken cancelacion = default);

    /// <summary>Avisos pendientes, del mas urgente al menos urgente.</summary>
    Task<IReadOnlyList<LineaAviso>> ObtenerPendientesAsync(CancellationToken cancelacion = default);

    /// <summary>Marca un aviso como resuelto.</summary>
    Task ResolverAsync(int avisoId, CancellationToken cancelacion = default);
}
