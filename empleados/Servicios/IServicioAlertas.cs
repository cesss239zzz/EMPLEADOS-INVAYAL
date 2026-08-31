using empleados.Datos.Entidades;

namespace empleados.Servicios;

/// <summary>
/// Gravedad de un aviso. Decide el color del filo izquierdo y del icono en el
/// panel "Alertas Recientes": rojo lo vencido, ambar lo que esta por vencer,
/// azul lo meramente informativo.
/// </summary>
public enum NivelAviso
{
    Informativo = 0,
    Advertencia = 1,
    Critico = 2
}

/// <summary>Un aviso listo para el panel de pendientes.</summary>
public sealed record LineaAviso(
    int Id,
    TipoAviso Tipo,
    EstadoAviso Estado,
    string Titulo,
    string Descripcion,
    DateTime FechaReferencia,
    string Colaborador,
    DateTime FechaGeneracion)
{
    public string FechaTexto => FechaReferencia.ToString("dd/MM/yyyy");

    /// <summary>Dias que faltan para el hecho. Negativo si ya paso.</summary>
    public int DiasRestantes => (int)(FechaReferencia.Date - DateTime.Today).TotalDays;

    /// <summary>Prioridad: lo vencido es critico, lo proximo es advertencia.</summary>
    public bool EsCritico => DiasRestantes < 0;

    public bool EsAdvertencia => DiasRestantes is >= 0 and <= 15;

    public string PlazoTexto => DiasRestantes switch
    {
        < 0 => "Vencido hace " + Math.Abs(DiasRestantes) + " días",
        0 => "Es hoy",
        1 => "Mañana",
        _ => "En " + DiasRestantes + " días"
    };

    public string TipoTexto => Tipo switch
    {
        TipoAviso.VencimientoContrato => "Contrato",
        TipoAviso.VencimientoDocumento => "Documento",
        TipoAviso.Cumpleanos => "Cumpleaños",
        TipoAviso.AniversarioLaboral => "Aniversario",
        TipoAviso.FinPeriodoPrueba => "Periodo de prueba",
        _ => "Aviso"
    };

    /// <summary>Gravedad, para que la vista elija color sin repetir la regla.</summary>
    public NivelAviso Nivel => EsCritico ? NivelAviso.Critico
        : EsAdvertencia ? NivelAviso.Advertencia
        : NivelAviso.Informativo;

    /// <summary>
    /// Cuando se genero el aviso, contado desde ahora: "Hace 2 horas",
    /// "Ayer, 14:30". Es el pie de cada tarjeta del panel de alertas.
    ///
    /// El motor guarda la marca en UTC y SQLite la devuelve sin zona, asi que
    /// aca se le pone la que le corresponde y se pasa a hora local antes de
    /// comparar. Sin eso, en Honduras (UTC-6) todo aviso recien generado
    /// pareceria estar seis horas en el futuro.
    /// </summary>
    public string MomentoTexto
    {
        get
        {
            var generado = DateTime.SpecifyKind(FechaGeneracion, DateTimeKind.Utc).ToLocalTime();
            var ahora = DateTime.Now;
            var transcurrido = ahora - generado;

            if (transcurrido < TimeSpan.Zero)
            {
                return generado.ToString("dd/MM/yyyy HH:mm");
            }

            if (transcurrido < TimeSpan.FromMinutes(1))
            {
                return "Recien";
            }

            if (transcurrido < TimeSpan.FromHours(1))
            {
                var minutos = (int)transcurrido.TotalMinutes;
                return minutos == 1 ? "Hace 1 minuto" : "Hace " + minutos + " minutos";
            }

            if (generado.Date == ahora.Date)
            {
                return "Hoy, " + generado.ToString("HH:mm");
            }

            if (generado.Date == ahora.Date.AddDays(-1))
            {
                return "Ayer, " + generado.ToString("HH:mm");
            }

            var dias = (int)(ahora.Date - generado.Date).TotalDays;
            return dias <= 7 ? "Hace " + dias + " días" : generado.ToString("dd/MM/yyyy");
        }
    }
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

    /// <summary>
    /// Cuantos avisos pendientes hay. Es un COUNT, no una lista: lo consume el
    /// globo del menu lateral, que debe reflejar el numero real aunque el
    /// usuario nunca abra la pantalla de Alertas (solicitud de cambios, CR-10).
    /// </summary>
    Task<int> ContarPendientesAsync(CancellationToken cancelacion = default);

    /// <summary>Marca un aviso como resuelto.</summary>
    Task ResolverAsync(int avisoId, CancellationToken cancelacion = default);
}
