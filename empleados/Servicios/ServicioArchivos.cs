using empleados.Configuracion;

namespace empleados.Servicios;

/// <summary>
/// Diálogo de "guardar como" del sistema.
///
/// MAUI trae selector para ABRIR archivos pero no para guardarlos, y CR-09 pide
/// que en la descarga «el usuario elige dónde guardar la copia». Por eso esto
/// baja a la API de Windows. Va detrás de una interfaz para que los ViewModel no
/// dependan de la plataforma (CLAUDE.md, regla 11).
/// </summary>
public interface IServicioArchivos
{
    /// <summary>
    /// Pregunta dónde guardar. Devuelve la ruta elegida, o nulo si el usuario
    /// canceló.
    /// </summary>
    Task<string?> PedirDondeGuardarAsync(
        string nombreSugerido, string extension, CancellationToken cancelacion = default);
}

/// <inheritdoc />
public sealed class ServicioArchivos : IServicioArchivos
{
    /// <inheritdoc />
    public async Task<string?> PedirDondeGuardarAsync(
        string nombreSugerido, string extension, CancellationToken cancelacion = default)
    {
#if WINDOWS
        var ventana = Microsoft.Maui.Controls.Application.Current?.Windows
            .FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

        if (ventana is not null)
        {
            var selector = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(nombreSugerido)
            };

            // Sin el identificador de la ventana, el selector lanza en una
            // aplicación de escritorio: no sabe de quién colgar el diálogo.
            var identificador = WinRT.Interop.WindowNative.GetWindowHandle(ventana);
            WinRT.Interop.InitializeWithWindow.Initialize(selector, identificador);

            var tipo = string.IsNullOrWhiteSpace(extension) ? ".dat" : extension;
            selector.FileTypeChoices.Add(DescribirTipo(tipo), new List<string> { tipo });

            var archivo = await selector.PickSaveFileAsync();
            return archivo?.Path;
        }
#endif

        // Sin diálogo del sistema disponible, la copia cae en la carpeta de
        // exportaciones, que ya existe y el usuario conoce. Nunca se pierde.
        await Task.CompletedTask.ConfigureAwait(false);

        Directory.CreateDirectory(RutasRhManager.CarpetaExportaciones);
        return Path.Combine(RutasRhManager.CarpetaExportaciones, Sanear(nombreSugerido));
    }

    private static string DescribirTipo(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "Documento PDF",
        ".jpg" or ".jpeg" => "Imagen JPEG",
        ".png" => "Imagen PNG",
        ".webp" => "Imagen WebP",
        ".bmp" => "Imagen BMP",
        ".tif" or ".tiff" => "Imagen TIFF",
        _ => "Archivo"
    };

    /// <summary>Quita de un nombre lo que Windows no admite en un archivo.</summary>
    private static string Sanear(string nombre)
    {
        var limpio = new string(nombre
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)
            .ToArray());

        return string.IsNullOrWhiteSpace(limpio) ? "documento" : limpio;
    }
}
