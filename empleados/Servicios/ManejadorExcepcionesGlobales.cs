using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <summary>
/// Red de seguridad de la aplicacion (CLAUDE.md, regla 7). Engancha los tres
/// canales por los que una excepcion puede escapar y cerrar el proceso en
/// silencio, deja traza completa en el registro y muestra una ventana de error
/// controlada. La aplicacion nunca se cierra sin decir por que.
/// </summary>
public sealed class ManejadorExcepcionesGlobales
{
    private static ManejadorExcepcionesGlobales? _instancia;

    private readonly ILogger<ManejadorExcepcionesGlobales> _registro;
    private readonly IServicioDialogo _dialogo;

    /// <summary>Impide que una cascada de fallos abra decenas de ventanas de error.</summary>
    private int _mostrandoError;

    public ManejadorExcepcionesGlobales(
        ILogger<ManejadorExcepcionesGlobales> registro,
        IServicioDialogo dialogo)
    {
        _registro = registro;
        _dialogo = dialogo;
    }

    /// <summary>
    /// Engancha los canales administrados. El tercer canal, el de WinUI, se engancha
    /// desde Platforms/Windows/App.xaml.cs porque vive antes que este contenedor.
    /// </summary>
    public void Enganchar()
    {
        _instancia = this;

        AppDomain.CurrentDomain.UnhandledException += (_, argumentos) =>
        {
            var excepcion = argumentos.ExceptionObject as Exception;
            Procesar("AppDomain.CurrentDomain.UnhandledException", excepcion, argumentos.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (_, argumentos) =>
        {
            Procesar("TaskScheduler.UnobservedTaskException", argumentos.Exception, terminando: false);

            // Marcarla como observada evita que el recolector de basura
            // termine el proceso al finalizar la tarea.
            argumentos.SetObserved();
        };

        _registro.LogInformation("Enganches globales de excepciones activos.");
    }

    /// <summary>
    /// Punto de entrada para los enganches de plataforma, que corren antes de que
    /// exista el contenedor. Si todavia no hay instancia, escribe a disco directo.
    /// </summary>
    public static void Notificar(string origen, Exception? excepcion)
    {
        if (_instancia is null)
        {
            RegistroEmergencia.Escribir(origen + " (antes de inicializar el contenedor)", excepcion);
            return;
        }

        _instancia.Procesar(origen, excepcion, terminando: false);
    }

    /// <summary>Registra el fallo y, si se puede, lo muestra en una ventana controlada.</summary>
    private void Procesar(string origen, Exception? excepcion, bool terminando)
    {
        // Primero a disco sin intermediarios: si Serilog es lo que fallo, esto sobrevive.
        RegistroEmergencia.Escribir(origen + (terminando ? " (el proceso va a terminar)" : string.Empty), excepcion);

        try
        {
            _registro.LogCritical(excepcion, "Excepcion no controlada en {Origen}. Terminando: {Terminando}",
                origen, terminando);
        }
        catch (Exception fallo)
        {
            RegistroEmergencia.Escribir("Fallo el propio registro al anotar la excepcion", fallo);
        }

        if (terminando)
        {
            // El entorno ya decidio cerrar. Abrir un dialogo aqui no llega a pintarse.
            return;
        }

        MostrarVentanaError(origen);
    }

    /// <summary>Ventana de error comprensible. Nunca muestra el mensaje crudo de la excepcion.</summary>
    private void MostrarVentanaError(string origen)
    {
        if (Interlocked.Exchange(ref _mostrandoError, 1) == 1)
        {
            return;
        }

        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await _dialogo.AvisarAsync(
                        "Ocurrio un problema inesperado",
                        "RH Manager detecto un error y lo registro para revisarlo. Puede continuar trabajando."
                            + Environment.NewLine + Environment.NewLine
                            + "El detalle quedo guardado en:" + Environment.NewLine
                            + Configuracion.RutasSigem.CarpetaRegistros);
                }
                catch (Exception fallo)
                {
                    RegistroEmergencia.Escribir("Fallo al mostrar la ventana de error controlada", fallo);
                }
                finally
                {
                    Interlocked.Exchange(ref _mostrandoError, 0);
                }
            });
        }
        catch (Exception fallo)
        {
            Interlocked.Exchange(ref _mostrandoError, 0);
            RegistroEmergencia.Escribir("Fallo al despachar la ventana de error desde " + origen, fallo);
        }
    }
}
