using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Configuracion;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>
/// Pantalla de diagnostico: explica que falla en la infraestructura y como resolverlo.
/// Es la alternativa a que la aplicacion se cierre cuando MySQL no responde.
/// </summary>
public sealed partial class VistaModeloDiagnostico : VistaModeloBase
{
    private readonly EstadoAplicacion _estado;
    private readonly IServicioDiagnostico _diagnostico;
    private readonly IServicioNavegacion _navegacion;
    private readonly OpcionesRhManager _opciones;

    public VistaModeloDiagnostico(
        EstadoAplicacion estado,
        IServicioDiagnostico diagnostico,
        IServicioNavegacion navegacion,
        OpcionesRhManager opciones,
        ILogger<VistaModeloDiagnostico> registro,
        IServicioDialogo dialogo)
        : base(registro, dialogo)
    {
        _estado = estado;
        _diagnostico = diagnostico;
        _navegacion = navegacion;
        _opciones = opciones;

        Titulo = "Diagnostico del sistema";
        Encabezado = "Verificando...";
        Explicacion = string.Empty;
        ComoResolverlo = string.Empty;
        DetalleTecnico = string.Empty;
        CarpetaRegistros = RutasRhManager.CarpetaRegistros;
        ArchivoConfiguracion = opciones.RutaArchivoConfiguracion;
    }

    /// <summary>Titulo del problema detectado.</summary>
    [ObservableProperty]
    public partial string Encabezado { get; set; }

    /// <summary>Que esta pasando, en lenguaje comprensible.</summary>
    [ObservableProperty]
    public partial string Explicacion { get; set; }

    /// <summary>Pasos concretos para resolverlo.</summary>
    [ObservableProperty]
    public partial string ComoResolverlo { get; set; }

    /// <summary>Traza tecnica completa, oculta por omision.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayDetalleTecnico))]
    public partial string DetalleTecnico { get; set; }

    /// <summary>Verdadero si hay traza tecnica que mostrar.</summary>
    public bool HayDetalleTecnico => !string.IsNullOrWhiteSpace(DetalleTecnico);

    /// <summary>Controla si la traza tecnica esta desplegada.</summary>
    [ObservableProperty]
    public partial bool DetalleVisible { get; set; }

    /// <summary>Ruta de la carpeta de registros, para mostrarla y copiarla.</summary>
    [ObservableProperty]
    public partial string CarpetaRegistros { get; set; }

    /// <summary>Ruta del appsettings.json en uso.</summary>
    [ObservableProperty]
    public partial string ArchivoConfiguracion { get; set; }

    /// <summary>Se ejecuta al abrir la pantalla, no antes (CLAUDE.md, regla 13).</summary>
    protected override Task CargarDatosAsync()
    {
        VolcarEnPantalla();
        return Task.CompletedTask;
    }

    /// <summary>Vuelca en la pantalla el ultimo resultado de la verificacion.</summary>
    private void VolcarEnPantalla()
    {
        var resultado = _estado.UltimoDiagnostico;
        if (resultado is null)
        {
            Encabezado = "Sin información de diagnostico";
            Explicacion = "Todavía no se ha ejecutado la verificación de infraestructura.";
            ComoResolverlo = "Pulse Reintentar para verificarla ahora.";
            DetalleTecnico = string.Empty;
            return;
        }

        Encabezado = resultado.Titulo;
        Explicacion = resultado.Explicacion;
        ComoResolverlo = resultado.ComoResolverlo;
        DetalleTecnico = resultado.DetalleTecnico ?? string.Empty;
    }

    /// <summary>Vuelve a verificar la conexion y entra al sistema si ya funciona.</summary>
    [RelayCommand]
    private Task ReintentarAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                EnHiloUi(() => Encabezado = "Verificando de nuevo...");

                var resultado = await _diagnostico.VerificarAsync().ConfigureAwait(true);
                _estado.UltimoDiagnostico = resultado;

                EnHiloUi(VolcarEnPantalla);

                if (resultado.EsCorrecto)
                {
                    await Dialogo.AvisarAsync(
                        "Conexión restablecida",
                        "La base de datos ya responde. RH Manager va a continuar.").ConfigureAwait(true);

                    await _navegacion.IrAsync(RutasNavegacion.Acceso).ConfigureAwait(true);
                }
            },
            "reintento de verificación de infraestructura",
            "No se pudo repetir la verificación. El detalle quedó en el archivo de registro.");

    /// <summary>Abre la carpeta de registros en el Explorador de Windows.</summary>
    [RelayCommand]
    private Task AbrirCarpetaRegistrosAsync()
        => EjecutarSeguroAsync(
            () =>
            {
                RutasRhManager.Asegurar();

                Process.Start(new ProcessStartInfo
                {
                    FileName = RutasRhManager.CarpetaRegistros,
                    UseShellExecute = true
                })?.Dispose();

                return Task.CompletedTask;
            },
            "apertura de la carpeta de registros",
            "No se pudo abrir la carpeta de registros. La ruta aparece en pantalla y puede copiarla a mano.");

    /// <summary>Copia la traza tecnica al portapapeles para pegarla en un reporte.</summary>
    [RelayCommand]
    private Task CopiarDetalleAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                var texto = Encabezado + Environment.NewLine
                    + Explicacion + Environment.NewLine + Environment.NewLine
                    + "Configuración: " + ArchivoConfiguracion + Environment.NewLine
                    + "Registros: " + CarpetaRegistros + Environment.NewLine + Environment.NewLine
                    + DetalleTecnico;

                await Clipboard.Default.SetTextAsync(texto).ConfigureAwait(true);
                await Dialogo.AvisarAsync("Copiado", "El detalle tecnico quedó en el portapapeles.")
                    .ConfigureAwait(true);
            },
            "copia del detalle tecnico",
            "No se pudo copiar el detalle al portapapeles.");

    /// <summary>Muestra u oculta la traza tecnica.</summary>
    [RelayCommand]
    private void AlternarDetalle() => DetalleVisible = !DetalleVisible;

    /// <summary>
    /// Prueba de aceptacion de E1: un fallo dentro de un comando queda registrado y
    /// se le explica al usuario, sin cerrar la aplicacion.
    /// </summary>
    [RelayCommand]
    private Task ProvocarErrorControladoAsync()
        => EjecutarSeguroAsync(
            () => throw new InvalidOperationException(
                "Error de prueba lanzado a propósito desde la pantalla de diagnostico."),
            "prueba de error controlado",
            "Este es el mensaje que veria el usuario ante un fallo. La aplicación sigue funcionando "
                + "y el detalle quedó en el archivo de registro.");

    /// <summary>
    /// Prueba de aceptacion de E1: una excepcion que nadie captura, lanzada en el hilo
    /// de interfaz. La atrapa el enganche global de WinUI, queda registrada y la
    /// aplicacion sobrevive.
    /// </summary>
    [RelayCommand]
    private void ProvocarErrorNoControlado()
    {
        Registro.LogWarning("Se va a lanzar una excepción no controlada a propósito.");

        MainThread.BeginInvokeOnMainThread(static () =>
            throw new ApplicationException(
                "Excepción no controlada lanzada a propósito para probar el enganche global."));
    }
}
