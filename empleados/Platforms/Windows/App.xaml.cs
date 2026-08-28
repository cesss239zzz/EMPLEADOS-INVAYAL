using empleados.Servicios;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace empleados.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // Tercer enganche global (CLAUDE.md, regla 7). Se conecta aqui, y no en
            // MauiProgram, porque este constructor corre antes de que exista el
            // contenedor de dependencias: es el unico punto que atrapa un fallo
            // ocurrido durante el arranque de la propia interfaz.
            this.UnhandledException += AlOcurrirExcepcionNoControlada;
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        /// <summary>
        /// Marca la excepcion como manejada para que WinUI no termine el proceso, y la
        /// deriva al manejador global, que la registra y muestra una ventana controlada.
        /// </summary>
        private static void AlOcurrirExcepcionNoControlada(
            object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs argumentos)
        {
            argumentos.Handled = true;
            ManejadorExcepcionesGlobales.Notificar(
                "Microsoft.UI.Xaml.Application.UnhandledException",
                argumentos.Exception);
        }
    }
}
