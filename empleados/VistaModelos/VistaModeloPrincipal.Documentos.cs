using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>
/// Parte de documentos del contenedor principal (solicitud de cambios, CR-09 y CR-10).
///
/// El archivo se guarda dentro de la base, no como ruta a una carpeta. Desde el
/// expediente se puede ver su contenido, descargarlo, corregir sus datos,
/// reemplazarlo y eliminarlo, y todo eso queda registrado.
/// </summary>
public sealed partial class VistaModeloPrincipal
{
    private const string TextoSinArchivo = "Ningún archivo seleccionado";

    private readonly IServicioDocumentos _documentos;
    private readonly IServicioArchivos _archivos;

    /// <summary>Ruta del archivo elegido en el selector, aún sin guardar.</summary>
    private string _rutaArchivoElegido = string.Empty;

    /// <summary>Id del documento en edición; cero mientras es una subida nueva.</summary>
    private int _idDocumentoEnEdicion;

    // ─── Formulario de documento ────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelDocumento))]
    [NotifyPropertyChangedFor(nameof(PanelNovedad))]
    [NotifyPropertyChangedFor(nameof(PanelEdicion))]
    [NotifyPropertyChangedFor(nameof(PanelFicha))]
    [NotifyPropertyChangedFor(nameof(PanelResumen))]
    [NotifyPropertyChangedFor(nameof(PanelColaboradores))]
    [NotifyPropertyChangedFor(nameof(PanelAlertas))]
    [NotifyPropertyChangedFor(nameof(PanelCatalogos))]
    [NotifyPropertyChangedFor(nameof(PanelRespaldos))]
    [NotifyPropertyChangedFor(nameof(PanelPendiente))]
    public partial bool ModoDocumento { get; set; }

    [ObservableProperty] public partial string TituloDocumento { get; set; }
    [ObservableProperty] public partial string SubtituloDocumento { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayErrorDocumento))]
    public partial string ErrorDocumento { get; set; }

    public bool HayErrorDocumento => !string.IsNullOrEmpty(ErrorDocumento);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DocumentoVence))]
    [NotifyPropertyChangedFor(nameof(AyudaVencimiento))]
    public partial VigenciaTipoDocumento? TipoDocumentoSeleccionado { get; set; }

    /// <summary>
    /// Lo decide el TIPO de documento, no el usuario: un tipo marcado "sin
    /// vencimiento" deshabilita el campo, tal como pide CR-10.
    /// </summary>
    public bool DocumentoVence => TipoDocumentoSeleccionado?.Vence == true;

    public string AyudaVencimiento => TipoDocumentoSeleccionado switch
    {
        null => string.Empty,
        { Vence: false } => "Este tipo de documento no vence, así que no se pide vencimiento.",
        { MesesVigencia: null or 0 } =>
            "Este tipo vence, pero su vigencia no es fija: escriba el vencimiento a mano.",
        { MesesVigencia: var m } =>
            "Vigencia de " + DescribirMeses(m!.Value) + ": el vencimiento se calcula solo desde la emisión."
    };

    [ObservableProperty] public partial DateTime FechaEmisionDocumento { get; set; }
    [ObservableProperty] public partial DateTime FechaVencimientoDocumento { get; set; }
    [ObservableProperty] public partial string DescripcionDocumento { get; set; }
    [ObservableProperty] public partial string ArchivoElegidoNombre { get; set; }

    /// <summary>Texto de ayuda con los límites de subida (CR-09).</summary>
    public string LimitesDocumentoTexto =>
        "Se aceptan " + LimitesDocumento.ExtensionesTexto
        + ", hasta " + LimitesDocumento.MaximoTexto + " por archivo.";

    public ObservableCollection<VigenciaTipoDocumento> TiposDocumentoDoc { get; } = [];

    // ─── Vista previa ───────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelVistaPrevia))]
    [NotifyPropertyChangedFor(nameof(PanelDocumento))]
    [NotifyPropertyChangedFor(nameof(PanelNovedad))]
    [NotifyPropertyChangedFor(nameof(PanelEdicion))]
    [NotifyPropertyChangedFor(nameof(PanelFicha))]
    [NotifyPropertyChangedFor(nameof(PanelResumen))]
    [NotifyPropertyChangedFor(nameof(PanelColaboradores))]
    [NotifyPropertyChangedFor(nameof(PanelAlertas))]
    [NotifyPropertyChangedFor(nameof(PanelCatalogos))]
    [NotifyPropertyChangedFor(nameof(PanelRespaldos))]
    [NotifyPropertyChangedFor(nameof(PanelPendiente))]
    public partial bool ModoVistaPrevia { get; set; }

    public bool PanelVistaPrevia => ModoVistaPrevia;

    [ObservableProperty] public partial string TituloVistaPrevia { get; set; }
    [ObservableProperty] public partial string SubtituloVistaPrevia { get; set; }

    /// <summary>Imagen a mostrar, cuando el documento es una fotografía o escaneo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayImagen))]
    public partial ImageSource? ImagenVistaPrevia { get; set; }

    public bool HayImagen => ImagenVistaPrevia is not null;

    /// <summary>
    /// Ruta temporal del PDF que muestra el WebView. WebView2 sabe dibujar un
    /// PDF, pero necesita un archivo en disco: los bytes viven en la base, así
    /// que se vuelcan a la carpeta temporal del usuario solo para verlos.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayPdf))]
    public partial string RutaPdfVistaPrevia { get; set; }

    public bool HayPdf => !string.IsNullOrEmpty(RutaPdfVistaPrevia);

    /// <summary>Documento que se está viendo, para descargarlo o editarlo desde ahí.</summary>
    private FilaDocumento? _documentoEnVista;

    private void InicializarCamposDocumento()
    {
        TituloDocumento = string.Empty;
        SubtituloDocumento = string.Empty;
        ErrorDocumento = string.Empty;
        FechaEmisionDocumento = DateTime.Today;
        FechaVencimientoDocumento = DateTime.Today.AddYears(1);
        DescripcionDocumento = string.Empty;
        ArchivoElegidoNombre = TextoSinArchivo;

        ModoVistaPrevia = false;
        TituloVistaPrevia = string.Empty;
        SubtituloVistaPrevia = string.Empty;
        ImagenVistaPrevia = null;
        RutaPdfVistaPrevia = string.Empty;
    }

    private static string DescribirMeses(int meses) => meses switch
    {
        12 => "1 año",
        var m when m % 12 == 0 => (m / 12) + " años",
        1 => "1 mes",
        var m => m + " meses"
    };

    // ─── CR-10: el vencimiento se propone solo ──────────────────────────────

    /// <summary>
    /// Al elegir el tipo o cambiar la emisión, se propone el vencimiento según
    /// la vigencia configurada. Es una PROPUESTA: el usuario puede corregirla.
    /// </summary>
    partial void OnTipoDocumentoSeleccionadoChanged(VigenciaTipoDocumento? value) => ProponerVencimiento();

    partial void OnFechaEmisionDocumentoChanged(DateTime value) => ProponerVencimiento();

    private void ProponerVencimiento()
    {
        if (TipoDocumentoSeleccionado is not { Vence: true, MesesVigencia: > 0 } tipo)
        {
            return;
        }

        FechaVencimientoDocumento = FechaEmisionDocumento.AddMonths(tipo.MesesVigencia!.Value);
    }

    // ─── Comandos: alta y edición ───────────────────────────────────────────

    /// <summary>Abre el formulario para adjuntar un documento nuevo.</summary>
    [RelayCommand]
    private Task AdjuntarDocumentoAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (Ficha is null || !await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                var tipos = await _documentos.ObtenerTiposAsync().ConfigureAwait(true);

                if (tipos.Count == 0)
                {
                    await Dialogo.AvisarAsync(
                        "Sin tipos de documento",
                        "Todavía no hay tipos de documento en esta empresa. Créelos en "
                            + "Configuración → Catálogos antes de adjuntar.").ConfigureAwait(true);
                    return;
                }

                EnHiloUi(() =>
                {
                    RellenarTipos(tipos);
                    _idDocumentoEnEdicion = 0;
                    _rutaArchivoElegido = string.Empty;
                    TipoDocumentoSeleccionado = TiposDocumentoDoc.FirstOrDefault();
                    FechaEmisionDocumento = DateTime.Today;
                    DescripcionDocumento = string.Empty;
                    ArchivoElegidoNombre = TextoSinArchivo;
                    ErrorDocumento = string.Empty;
                    TituloDocumento = "Adjuntar documento";
                    SubtituloDocumento = "Se agrega al expediente de " + Ficha.NombreCompleto + ".";
                    ModoDocumento = true;
                    ProponerVencimiento();
                });
            },
            "apertura del formulario de documento",
            "No se pudo abrir el formulario de documentos. El detalle quedó en el archivo de registro.");

    /// <summary>Abre el formulario con un documento existente, para corregirlo.</summary>
    [RelayCommand]
    private Task EditarDocumentoAsync(FilaDocumento? documento)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (documento is null || !await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                var tipos = await _documentos.ObtenerTiposAsync().ConfigureAwait(true);

                EnHiloUi(() =>
                {
                    RellenarTipos(tipos);
                    _idDocumentoEnEdicion = documento.Id;
                    _rutaArchivoElegido = string.Empty;
                    TipoDocumentoSeleccionado =
                        TiposDocumentoDoc.FirstOrDefault(t => t.Nombre == documento.Tipo)
                        ?? TiposDocumentoDoc.FirstOrDefault();
                    FechaEmisionDocumento = documento.FechaEmision;
                    FechaVencimientoDocumento = documento.FechaVencimiento ?? DateTime.Today.AddYears(1);
                    DescripcionDocumento = documento.Descripcion ?? string.Empty;
                    ArchivoElegidoNombre = documento.NombreArchivo + "  (actual)";
                    ErrorDocumento = string.Empty;
                    TituloDocumento = "Editar documento";
                    SubtituloDocumento =
                        "Corrija los datos o reemplace el archivo. Si no elige uno nuevo, se conserva el actual.";
                    ModoVistaPrevia = false;
                    ModoDocumento = true;
                });
            },
            "apertura de la edición de documento",
            "No se pudo abrir el documento. El detalle quedó en el archivo de registro.");

    private void RellenarTipos(IReadOnlyList<VigenciaTipoDocumento> tipos)
    {
        TiposDocumentoDoc.Clear();
        foreach (var tipo in tipos)
        {
            TiposDocumentoDoc.Add(tipo);
        }
    }

    /// <summary>Abre el selector de archivos del sistema.</summary>
    [RelayCommand]
    private Task ElegirArchivoAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                var resultado = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Elija el documento a adjuntar"
                }).ConfigureAwait(true);

                if (resultado is null)
                {
                    return;
                }

                _rutaArchivoElegido = resultado.FullPath;
                EnHiloUi(() =>
                {
                    ArchivoElegidoNombre = resultado.FileName;
                    ErrorDocumento = string.Empty;
                });
            },
            "selección de archivo de documento",
            "No se pudo abrir el selector de archivos.");

    /// <summary>Valida y guarda el documento; recarga la ficha.</summary>
    [RelayCommand]
    private Task GuardarDocumentoAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (Ficha is null)
                {
                    return;
                }

                if (TipoDocumentoSeleccionado is null)
                {
                    EnHiloUi(() => ErrorDocumento = "Seleccione el tipo de documento.");
                    return;
                }

                var colaboradorId = Ficha.Id;

                var resultado = await _documentos.GuardarAsync(new DatosDocumento
                {
                    Id = _idDocumentoEnEdicion,
                    ColaboradorId = colaboradorId,
                    TipoDocumentoId = TipoDocumentoSeleccionado.Id,
                    RutaOrigen = _rutaArchivoElegido,
                    FechaEmision = FechaEmisionDocumento,
                    FechaVencimiento = DocumentoVence ? FechaVencimientoDocumento : null,
                    Descripcion = DescripcionDocumento
                }).ConfigureAwait(true);

                if (!resultado.Exito)
                {
                    EnHiloUi(() => ErrorDocumento = resultado.Error ?? "No se pudo guardar el documento.");
                    return;
                }

                var eraAlta = _idDocumentoEnEdicion == 0;

                EnHiloUi(() =>
                {
                    ErrorDocumento = string.Empty;
                    ModoDocumento = false;
                });

                // Resumen y Alertas recalculan sus avisos al abrirse.
                await AbrirFichaPorIdAsync(colaboradorId).ConfigureAwait(true);

                await Dialogo.AvisarAsync(
                    eraAlta ? "Documento adjuntado" : "Cambios guardados",
                    eraAlta
                        ? "El documento quedó guardado dentro del expediente."
                        : "Los datos del documento se actualizaron.").ConfigureAwait(true);
            },
            "guardado del documento",
            "No se pudo guardar el documento. El detalle quedó en el archivo de registro.");

    /// <summary>Cierra el formulario de documento sin guardar.</summary>
    [RelayCommand]
    private void CancelarDocumento()
    {
        if (EstaOcupado) return;
        ModoDocumento = false;
        ErrorDocumento = string.Empty;
    }

    // ─── Comandos: ver, descargar, eliminar ─────────────────────────────────

    /// <summary>
    /// Muestra el contenido dentro de la aplicación: la imagen o el PDF, no solo
    /// el nombre del archivo (CR-09).
    /// </summary>
    [RelayCommand]
    private Task VerDocumentoAsync(FilaDocumento? documento)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (documento is null)
                {
                    return;
                }

                var bytes = await _documentos.ObtenerContenidoAsync(documento.Id).ConfigureAwait(true);

                if (bytes is null)
                {
                    await Dialogo.AvisarAsync(
                        "Sin archivo",
                        "Este documento no tiene un archivo guardado.").ConfigureAwait(true);
                    return;
                }

                string? rutaPdf = null;
                ImageSource? imagen = null;

                if (documento.EsPdf)
                {
                    rutaPdf = await VolcarATemporalAsync(documento, bytes).ConfigureAwait(true);
                }
                else if (documento.EsImagen)
                {
                    // El flujo se crea por invocación: ImageSource lo consume
                    // cuando dibuja, no en este momento.
                    imagen = ImageSource.FromStream(() => new MemoryStream(bytes));
                }

                EnHiloUi(() =>
                {
                    _documentoEnVista = documento;
                    ImagenVistaPrevia = imagen;
                    RutaPdfVistaPrevia = rutaPdf ?? string.Empty;
                    TituloVistaPrevia = documento.Tipo;
                    SubtituloVistaPrevia = documento.NombreArchivo + "  ·  " + documento.TamanoTexto
                        + "  ·  " + documento.PlazoTexto;
                    ModoVistaPrevia = true;
                });

                if (rutaPdf is null && imagen is null)
                {
                    await Dialogo.AvisarAsync(
                        "Sin vista previa",
                        "Este tipo de archivo no se puede mostrar dentro de la aplicación. "
                            + "Use Descargar para abrirlo con otro programa.").ConfigureAwait(true);
                }
            },
            "vista previa de documento",
            "No se pudo mostrar el documento. El detalle quedó en el archivo de registro.");

    /// <summary>Cierra la vista previa.</summary>
    [RelayCommand]
    private void CerrarVistaPrevia()
    {
        ModoVistaPrevia = false;
        ImagenVistaPrevia = null;
        RutaPdfVistaPrevia = string.Empty;
        _documentoEnVista = null;
    }

    /// <summary>Descarga el documento donde el usuario elija (CR-09).</summary>
    [RelayCommand]
    private Task DescargarDocumentoAsync(FilaDocumento? documento)
        => EjecutarSeguroAsync(
            async () =>
            {
                var elegido = documento ?? _documentoEnVista;
                if (elegido is null)
                {
                    return;
                }

                var destino = await _archivos
                    .PedirDondeGuardarAsync(elegido.NombreArchivo, elegido.Extension)
                    .ConfigureAwait(true);

                if (string.IsNullOrEmpty(destino))
                {
                    return;
                }

                var bytes = await _documentos
                    .ObtenerContenidoAsync(elegido.Id, registrarDescarga: true)
                    .ConfigureAwait(true);

                if (bytes is null)
                {
                    await Dialogo.AvisarAsync(
                        "Sin archivo",
                        "Este documento no tiene un archivo guardado.").ConfigureAwait(true);
                    return;
                }

                await File.WriteAllBytesAsync(destino, bytes).ConfigureAwait(true);

                await Dialogo.AvisarAsync(
                    "Documento descargado",
                    "La copia quedó en:" + Environment.NewLine + Environment.NewLine + destino)
                    .ConfigureAwait(true);
            },
            "descarga de documento",
            "No se pudo descargar el documento. El detalle quedó en el archivo de registro.");

    /// <summary>Elimina un documento del expediente, con confirmación (CR-09).</summary>
    [RelayCommand]
    private Task EliminarDocumentoAsync(FilaDocumento? documento)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (documento is null || !await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                var confirmar = await Dialogo.ConfirmarAsync(
                    "Eliminar documento",
                    "¿Eliminar «" + documento.NombreArchivo + "» del expediente?"
                        + Environment.NewLine + Environment.NewLine
                        + "El archivo se borra del sistema y no se puede recuperar, salvo desde un "
                        + "respaldo anterior.",
                    "Eliminar", "Cancelar").ConfigureAwait(true);

                if (!confirmar)
                {
                    return;
                }

                var resultado = await _documentos.EliminarAsync(documento.Id).ConfigureAwait(true);

                if (!resultado.Exito)
                {
                    await Dialogo.AvisarAsync(
                        "No se pudo eliminar",
                        resultado.Error ?? "No se pudo eliminar el documento.").ConfigureAwait(true);
                    return;
                }

                EnHiloUi(CerrarVistaPrevia);

                await AbrirFichaPorIdAsync(documento.ColaboradorId).ConfigureAwait(true);
            },
            "eliminación de documento",
            "No se pudo eliminar el documento. El detalle quedó en el archivo de registro.");

    /// <summary>
    /// Vuelca los bytes a un archivo temporal para que el WebView pueda dibujar
    /// el PDF. Se reutiliza el mismo nombre por documento: no tiene sentido
    /// sembrar la carpeta temporal con una copia por cada vez que se mira.
    /// </summary>
    private async Task<string?> VolcarATemporalAsync(FilaDocumento documento, byte[] bytes)
    {
        try
        {
            var carpeta = Path.Combine(Path.GetTempPath(), "RH Manager", "vista-previa");
            Directory.CreateDirectory(carpeta);

            var ruta = Path.Combine(carpeta, "doc-" + documento.Id + documento.Extension);
            await File.WriteAllBytesAsync(ruta, bytes).ConfigureAwait(true);
            return ruta;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Registro.LogError(ex, "No se pudo preparar la vista previa del documento {Id}.", documento.Id);
            return null;
        }
    }
}
