namespace empleados.Servicios;

/// <summary>Por que se creo un respaldo. Se ve en el nombre del archivo.</summary>
public enum MotivoRespaldo
{
    /// <summary>El automatico del dia, al abrir la aplicacion.</summary>
    Diario = 1,

    /// <summary>El usuario pulso "Respaldar ahora".</summary>
    Manual = 2,

    /// <summary>
    /// El que se toma solo, justo antes de restaurar otro. Es la red que hace
    /// reversible una restauracion equivocada.
    /// </summary>
    PrevioRestauracion = 3
}

/// <summary>Un respaldo tal como se lista en pantalla.</summary>
/// <param name="Archivo">Ruta absoluta del archivo.</param>
/// <param name="Nombre">Nombre del archivo, para mostrarlo.</param>
/// <param name="Momento">Cuando se tomo.</param>
/// <param name="Motivo">Por que se tomo.</param>
/// <param name="Bytes">Tamano en disco.</param>
public sealed record LineaRespaldo(
    string Archivo,
    string Nombre,
    DateTime Momento,
    MotivoRespaldo Motivo,
    long Bytes)
{
    public string MomentoTexto => Momento.ToString("dd/MM/yyyy HH:mm");

    public string MotivoTexto => Motivo switch
    {
        MotivoRespaldo.Diario => "Automático del día",
        MotivoRespaldo.Manual => "Manual",
        MotivoRespaldo.PrevioRestauracion => "Previo a una restauración",
        _ => "Respaldo"
    };

    /// <summary>Tamano legible: 1.4 MB en vez de 1468006.</summary>
    public string TamanoTexto => Bytes switch
    {
        < 1024 => Bytes + " B",
        < 1024 * 1024 => (Bytes / 1024d).ToString("0.#") + " KB",
        _ => (Bytes / (1024d * 1024d)).ToString("0.#") + " MB"
    };

    /// <summary>Dias que lleva guardado. Sirve para explicar la purga.</summary>
    public int DiasDeAntiguedad => Math.Max(0, (int)(DateTime.Today - Momento.Date).TotalDays);
}

/// <summary>Resultado de crear un respaldo.</summary>
/// <param name="Exito">Verdadero si el archivo quedo escrito.</param>
/// <param name="Archivo">Ruta del respaldo creado, o vacio si fallo.</param>
/// <param name="Error">Mensaje en español cuando no salio bien.</param>
public sealed record ResultadoRespaldo(bool Exito, string Archivo, string? Error)
{
    public static ResultadoRespaldo Ok(string archivo) => new(true, archivo, null);
    public static ResultadoRespaldo Falla(string mensaje) => new(false, string.Empty, mensaje);
}

/// <summary>
/// Respaldo y restauracion de la base local (solicitud de cambios, CR-12).
///
/// Existe porque el borrado de una empresa es fisico y no hay papelera: sin
/// respaldo, una confirmacion escrita de mas se lleva por delante el expediente
/// completo de una empresa y no hay forma de recuperarlo.
/// </summary>
public interface IServicioRespaldos
{
    /// <summary>Crea un respaldo del estado actual de la base.</summary>
    Task<ResultadoRespaldo> RespaldarAsync(
        MotivoRespaldo motivo, CancellationToken cancelacion = default);

    /// <summary>
    /// Crea el respaldo automatico si hoy todavia no se hizo ninguno, y de paso
    /// purga los que pasaron de la ventana de retencion. Es lo que corre al
    /// arrancar la aplicacion.
    /// </summary>
    Task<ResultadoRespaldo?> RespaldarSiTocaAsync(CancellationToken cancelacion = default);

    /// <summary>Respaldos disponibles, del mas reciente al mas antiguo.</summary>
    Task<IReadOnlyList<LineaRespaldo>> ListarAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Reemplaza la base actual por la del respaldo indicado. Antes toma un
    /// respaldo del estado presente, para que la operacion sea reversible.
    ///
    /// La aplicacion DEBE cerrarse despues: el modelo de datos y las conexiones
    /// abiertas quedaron apuntando al archivo anterior.
    /// </summary>
    Task<ResultadoGuardado> RestaurarAsync(
        string archivoRespaldo, CancellationToken cancelacion = default);

    /// <summary>Borra los respaldos que superaron la ventana de retencion.</summary>
    Task<int> PurgarAntiguosAsync(CancellationToken cancelacion = default);

    /// <summary>Dias que se conservan los respaldos.</summary>
    int DiasRetencion { get; }

    /// <summary>Carpeta donde viven, para poder mostrarla y abrirla.</summary>
    string Carpeta { get; }
}
