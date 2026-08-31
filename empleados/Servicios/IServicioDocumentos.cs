using empleados.Datos.Entidades;

namespace empleados.Servicios;

/// <summary>Datos de un documento al adjuntarlo o al editarlo.</summary>
public sealed class DatosDocumento
{
    /// <summary>Cero al adjuntar; el identificador del documento al editar.</summary>
    public int Id { get; set; }

    public int ColaboradorId { get; set; }
    public int TipoDocumentoId { get; set; }

    /// <summary>
    /// Ruta del archivo elegido. Vacía al editar sin reemplazar el archivo: en
    /// ese caso solo se actualizan tipo, fechas y descripción.
    /// </summary>
    public string RutaOrigen { get; set; } = string.Empty;

    public DateTime FechaEmision { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public string? Descripcion { get; set; }

    public bool EsAlta => Id == 0;
    public bool ReemplazaArchivo => !string.IsNullOrWhiteSpace(RutaOrigen);
}

/// <summary>Reglas de lo que se acepta subir. Están acá y no repartidas por la interfaz.</summary>
public static class LimitesDocumento
{
    /// <summary>
    /// Tope por archivo. Los escaneos de un expediente son de cientos de kilobytes;
    /// diez megas deja margen de sobra y evita que un video por error infle la base.
    /// </summary>
    public const long MaximoBytes = 10L * 1024 * 1024;

    public static string MaximoTexto => "10 MB";

    /// <summary>Extensiones admitidas: lo que de verdad se archiva en un expediente.</summary>
    public static readonly IReadOnlyList<string> Extensiones =
        [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff"];

    public static string ExtensionesTexto => "PDF, JPG, PNG, WEBP, BMP y TIFF";

    public static bool EsImagen(string extension) => extension.ToLowerInvariant()
        is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp";

    public static bool EsPdf(string extension) => extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Un documento del expediente, sin sus bytes.</summary>
public sealed record FilaDocumento(
    int Id,
    int ColaboradorId,
    string Tipo,
    string NombreArchivo,
    string Extension,
    string? Descripcion,
    DateTime FechaEmision,
    DateTime? FechaVencimiento,
    long TamanoBytes,
    int DiasAviso)
{
    public string EmisionTexto => FechaEmision.ToString("dd/MM/yyyy");

    public string VencimientoTexto => FechaVencimiento is null
        ? "No vence"
        : FechaVencimiento.Value.ToString("dd/MM/yyyy");

    public string DescripcionTexto => string.IsNullOrWhiteSpace(Descripcion)
        ? "Sin descripción"
        : Descripcion;

    public int? DiasParaVencer => FechaVencimiento is null
        ? null
        : (int)(FechaVencimiento.Value.Date - DateTime.Today).TotalDays;

    public bool EstaVencido => DiasParaVencer is < 0;

    /// <summary>La ventana de aviso la define el tipo de documento, no el código (CR-10).</summary>
    public bool PorVencer => DiasParaVencer is { } dias && dias >= 0 && dias <= DiasAviso;

    public SituacionDocumento Situacion => EstaVencido ? SituacionDocumento.Vencido
        : PorVencer ? SituacionDocumento.PorVencer
        : SituacionDocumento.Vigente;

    public string PlazoTexto => DiasParaVencer switch
    {
        null => "Sin vencimiento",
        < 0 => "Vencido hace " + Math.Abs(DiasParaVencer.Value) + " día(s)",
        0 => "Vence hoy",
        1 => "Vence mañana",
        var d => "Vence en " + d + " día(s)"
    };

    public string TamanoTexto => TamanoBytes switch
    {
        < 1024 => TamanoBytes + " B",
        < 1024 * 1024 => (TamanoBytes / 1024d).ToString("0.#") + " KB",
        _ => (TamanoBytes / (1024d * 1024d)).ToString("0.#") + " MB"
    };

    public bool EsImagen => LimitesDocumento.EsImagen(Extension);
    public bool EsPdf => LimitesDocumento.EsPdf(Extension);

    /// <summary>Verdadero cuando se puede mostrar dentro de la aplicación.</summary>
    public bool TieneVistaPrevia => EsImagen || EsPdf;
}

/// <summary>Un renglón del historial de un documento.</summary>
public sealed record LineaHistorialDocumento(
    string NombreDocumento,
    AccionDocumento Accion,
    DateTime Fecha,
    string NombreUsuario,
    string Detalle)
{
    public string FechaTexto => Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string AccionTexto => Accion switch
    {
        AccionDocumento.Subida => "Subido",
        AccionDocumento.Edicion => "Editado",
        AccionDocumento.Reemplazo => "Archivo reemplazado",
        AccionDocumento.Eliminacion => "Eliminado",
        AccionDocumento.Descarga => "Descargado",
        _ => "Movimiento"
    };
}

/// <summary>Vigencia de un tipo de documento, para proponer el vencimiento.</summary>
/// <param name="Id">Identificador del tipo.</param>
/// <param name="Nombre">Nombre que ve el usuario.</param>
/// <param name="Vence">Si este tipo caduca.</param>
/// <param name="MesesVigencia">Meses de vigencia, o nulo si es variable.</param>
public sealed record VigenciaTipoDocumento(int Id, string Nombre, bool Vence, int? MesesVigencia);

/// <summary>
/// Documentos del expediente (solicitud de cambios, CR-09 y CR-10).
///
/// El archivo se guarda DENTRO de la base, no como ruta a una carpeta del
/// equipo. Desde el expediente se puede ver, descargar, editar y eliminar, y
/// cada una de esas acciones queda registrada.
/// </summary>
public interface IServicioDocumentos
{
    /// <summary>Tipos de documento activos, con su vigencia para calcular el vencimiento.</summary>
    Task<IReadOnlyList<VigenciaTipoDocumento>> ObtenerTiposAsync(CancellationToken cancelacion = default);

    /// <summary>Documentos de un colaborador, sin traer los bytes.</summary>
    Task<IReadOnlyList<FilaDocumento>> ObtenerDeColaboradorAsync(
        int colaboradorId, CancellationToken cancelacion = default);

    /// <summary>Un documento por identificador, sin sus bytes.</summary>
    Task<FilaDocumento?> ObtenerAsync(int documentoId, CancellationToken cancelacion = default);

    /// <summary>
    /// Los bytes de un documento. Es la única puerta por la que salen, y deja
    /// rastro cuando el motivo es una descarga.
    /// </summary>
    Task<byte[]?> ObtenerContenidoAsync(
        int documentoId, bool registrarDescarga = false, CancellationToken cancelacion = default);

    /// <summary>Adjunta un documento nuevo o actualiza uno existente.</summary>
    Task<ResultadoGuardado> GuardarAsync(DatosDocumento datos, CancellationToken cancelacion = default);

    /// <summary>Elimina un documento y sus bytes. Deja constancia en el historial.</summary>
    Task<ResultadoGuardado> EliminarAsync(int documentoId, CancellationToken cancelacion = default);

    /// <summary>Historial de acciones sobre los documentos de un colaborador.</summary>
    Task<IReadOnlyList<LineaHistorialDocumento>> ObtenerHistorialAsync(
        int colaboradorId, CancellationToken cancelacion = default);
}
