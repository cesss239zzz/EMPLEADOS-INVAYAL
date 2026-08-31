using CommunityToolkit.Mvvm.Input;
using empleados.Servicios;

namespace empleados.VistaModelos;

/// <summary>
/// Parte de reportes del contenedor principal: exportar el directorio a PDF o
/// Excel, la ficha a PDF y la constancia de trabajo en PDF. El archivo se genera
/// en la carpeta de exportaciones y se abre con la aplicacion del sistema.
///
/// La constancia sale sin codigo QR en esta etapa (el QR necesita un paquete no
/// autorizado todavia); el resto del reporte queda completo.
/// </summary>
public sealed partial class VistaModeloPrincipal
{
    private readonly IServicioReportes _reportes;

    /// <summary>Exporta el directorio de colaboradores. Pregunta el formato.</summary>
    [RelayCommand]
    private Task ExportarDirectorioAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                var enExcel = await Dialogo.ConfirmarAsync(
                    "Exportar directorio",
                    "Elija el formato del reporte.",
                    "Excel", "PDF").ConfigureAwait(true);

                var formato = enExcel ? FormatoReporte.Excel : FormatoReporte.Pdf;

                var ruta = await _reportes
                    .ExportarColaboradoresAsync(ArmarFiltro(), formato)
                    .ConfigureAwait(true);

                await AbrirArchivoAsync(ruta).ConfigureAwait(true);
            },
            "exportación del directorio de colaboradores",
            "No se pudo generar el reporte. El detalle quedó en el archivo de registro.");

    /// <summary>Exporta a PDF la ficha del expediente abierto.</summary>
    [RelayCommand]
    private Task ExportarFichaAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (Ficha is null)
                {
                    return;
                }

                var ruta = await _reportes.ExportarFichaPdfAsync(Ficha.Id).ConfigureAwait(true);
                await AbrirArchivoAsync(ruta).ConfigureAwait(true);
            },
            "exportación de la ficha en PDF",
            "No se pudo generar la ficha en PDF. El detalle quedó en el archivo de registro.");

    /// <summary>Genera la constancia de trabajo en PDF del expediente abierto.</summary>
    [RelayCommand]
    private Task GenerarConstanciaAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (Ficha is null)
                {
                    return;
                }

                var ruta = await _reportes.GenerarConstanciaPdfAsync(Ficha.Id).ConfigureAwait(true);
                await AbrirArchivoAsync(ruta).ConfigureAwait(true);
            },
            "generación de la constancia de trabajo",
            "No se pudo generar la constancia. El detalle quedó en el archivo de registro.");

    /// <summary>Abre el archivo generado con la aplicacion predeterminada del sistema.</summary>
    private static Task AbrirArchivoAsync(string ruta)
        => Launcher.Default.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(ruta)
        });
}
