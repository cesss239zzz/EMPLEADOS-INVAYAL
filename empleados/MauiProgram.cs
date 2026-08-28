using System.Globalization;
using empleados.Configuracion;
using empleados.Datos;
using empleados.Servicios;
using Microsoft.EntityFrameworkCore;
using empleados.VistaModelos;
using empleados.Vistas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace empleados;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // La cultura se fija antes que nada: cualquier fecha o importe que se formatee
        // durante el arranque ya debe salir en formato hondureno.
        FijarCulturaHondurena();

        RutasSigem.Asegurar();

        var configuracion = CargarConfiguracion(out var rutaArchivoConfiguracion);
        var opciones = LeerOpciones(configuracion, rutaArchivoConfiguracion);

        ConfigurarSerilog(opciones);

        Log.Information("=== RH Manager inicia. Version {Version} ===", AppInfo.Current.VersionString);
        Log.Information("Configuracion leida de {Archivo}", rutaArchivoConfiguracion);
        Log.Information("Registros en {Carpeta}", RutasSigem.CarpetaRegistros);

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Configuration.AddConfiguration(configuracion);

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);
#if DEBUG
        builder.Logging.AddDebug();
#endif

        RegistrarDependencias(builder.Services, opciones);

        var aplicacion = builder.Build();

        // Los enganches se activan apenas existe el contenedor, antes de que se
        // muestre la primera ventana (CLAUDE.md, regla 7).
        aplicacion.Services
            .GetRequiredService<ManejadorExcepcionesGlobales>()
            .Enganchar();

        return aplicacion;
    }

    /// <summary>
    /// Cultura es-HN: fechas dd/MM/yyyy y moneda "L. #,##0.00". Se aplica a los hilos
    /// que se creen despues, que es lo que importa para las tareas asincronas.
    /// </summary>
    private static void FijarCulturaHondurena()
    {
        var cultura = new CultureInfo("es-HN");

        cultura.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
        cultura.DateTimeFormat.LongDatePattern = "dddd, dd 'de' MMMM 'de' yyyy";
        cultura.NumberFormat.CurrencySymbol = "L.";
        cultura.NumberFormat.CurrencyPositivePattern = 2;   // L. 1,234.56
        cultura.NumberFormat.CurrencyNegativePattern = 12;  // L. -1,234.56
        cultura.NumberFormat.CurrencyDecimalDigits = 2;

        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;
    }

    /// <summary>
    /// Lee appsettings.json de junto al ejecutable, para que se pueda corregir la
    /// cadena de conexion en la maquina del cliente sin recompilar. El archivo
    /// appsettings.Local.json, si existe, tiene prioridad y no se versiona.
    /// </summary>
    private static IConfigurationRoot CargarConfiguracion(out string rutaArchivo)
    {
        var carpeta = AppContext.BaseDirectory;
        rutaArchivo = Path.Combine(carpeta, "appsettings.json");

        return new ConfigurationBuilder()
            .SetBasePath(carpeta)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .Build();
    }

    private static OpcionesSigem LeerOpciones(IConfiguration configuracion, string rutaArchivo)
    {
        var opciones = configuracion.GetSection(OpcionesSigem.Seccion).Get<OpcionesSigem>()
            ?? new OpcionesSigem();

        var cadena = configuracion.GetConnectionString("Sigem");

        // Sin cadena configurada se usa la base SQLite de la carpeta de datos.
        // Asi la aplicacion arranca en una maquina nueva sin tocar nada.
        opciones.CadenaConexion = string.IsNullOrWhiteSpace(cadena)
            ? RutasSigem.CadenaConexionPorOmision
            : cadena;

        opciones.RutaArchivoConfiguracion = rutaArchivo;

        return opciones;
    }

    /// <summary>Serilog a archivo diario. Es el registro que se revisa cuando algo falla.</summary>
    private static void ConfigurarSerilog(OpcionesSigem opciones)
    {
        if (!Enum.TryParse<LogEventLevel>(opciones.NivelRegistroMinimo, ignoreCase: true, out var nivel))
        {
            nivel = LogEventLevel.Information;
        }

        // Un registro que falla en silencio es peor que no tenerlo: si el propio Serilog
        // no puede abrir su archivo, lo dice en el registro de emergencia. Sin esto, la
        // aplicacion corre sin dejar rastro y no hay forma de saberlo.
        Serilog.Debugging.SelfLog.Enable(mensaje =>
            RegistroEmergencia.Escribir("Serilog no pudo escribir: " + mensaje));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(nivel)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: RutasSigem.ArchivoRegistro,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Math.Max(opciones.DiasRetencionRegistros, 1),
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// Registro de dependencias. Toda pagina, ViewModel y servicio pasa por aqui:
    /// resolver una pagina sin registrar cierra la aplicacion al navegar, sin
    /// excepcion visible (CLAUDE.md, regla 1).
    /// </summary>
    private static void RegistrarDependencias(IServiceCollection servicios, OpcionesSigem opciones)
    {
        // Estado y configuracion: una sola instancia para toda la aplicacion.
        servicios.AddSingleton(opciones);
        servicios.AddSingleton<EstadoAplicacion>();

        // Empresa activa: singleton, porque de el depende el filtro global
        // (CLAUDE.md, reglas 1 y 9).
        servicios.AddSingleton<IContextoEmpresa, ContextoEmpresa>();

        // El contexto de datos SIEMPRE por fabrica, nunca inyectado directo:
        // MAUI no tiene ambito por peticion y DbContext no es seguro entre
        // hilos (CLAUDE.md, regla 2).
        servicios.AddDbContextFactory<ContextoSigem>(constructor =>
            constructor.UseSqlite(opciones.CadenaConexion));

        // Servicios de plataforma, sin estado.
        servicios.AddSingleton<IServicioDialogo, ServicioDialogo>();
        servicios.AddSingleton<IServicioNavegacion, ServicioNavegacion>();
        servicios.AddSingleton<ManejadorExcepcionesGlobales>();

        // Sesion del usuario autenticado: singleton (CLAUDE.md, regla 1).
        servicios.AddSingleton<SesionUsuario>();

        // Servicios de datos.
        servicios.AddTransient<IServicioDiagnostico, ServicioDiagnostico>();
        servicios.AddTransient<IServicioAutenticacion, ServicioAutenticacion>();
        servicios.AddTransient<IServicioEmpresas, ServicioEmpresas>();
        servicios.AddTransient<IServicioColaboradores, ServicioColaboradores>();
        servicios.AddTransient<IServicioFicha, ServicioFicha>();
        servicios.AddTransient<IServicioResumen, ServicioResumen>();
        servicios.AddTransient<IServicioAlertas, ServicioAlertas>();

        // Contenedor de navegacion.
        servicios.AddSingleton<AppShell>();

        // ViewModels.
        servicios.AddTransient<VistaModeloArranque>();
        servicios.AddTransient<VistaModeloDiagnostico>();
        servicios.AddTransient<VistaModeloAcceso>();
        servicios.AddTransient<VistaModeloSelectorEmpresa>();
        servicios.AddTransient<VistaModeloPrincipal>();

        // Paginas.
        servicios.AddTransient<PaginaArranque>();
        servicios.AddTransient<PaginaDiagnostico>();
        servicios.AddTransient<PaginaAcceso>();
        servicios.AddTransient<PaginaSelectorEmpresa>();
        servicios.AddTransient<PaginaPrincipal>();
    }
}
