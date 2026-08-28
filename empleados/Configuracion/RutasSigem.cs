namespace empleados.Configuracion;

/// <summary>
/// Rutas fijas de la aplicacion en disco. Todo lo que RH Manager escribe vive bajo
/// %LOCALAPPDATA%\RH Manager, nunca junto al ejecutable: la carpeta de instalacion
/// puede ser de solo lectura y eso cerraria la aplicacion al abrir el registro.
/// </summary>
public static class RutasSigem
{
    /// <summary>Carpeta raiz de datos de la aplicacion para el usuario actual.</summary>
    public static string CarpetaDatos { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RH Manager");

    /// <summary>Carpeta donde Serilog escribe los archivos diarios.</summary>
    public static string CarpetaRegistros { get; } = Path.Combine(CarpetaDatos, "registros");

    /// <summary>
    /// Base SQLite de la demostracion. Vive en la carpeta de datos de la
    /// aplicacion, no junto al ejecutable: la carpeta de instalacion puede ser
    /// de solo lectura y ahi la base no se podria escribir.
    /// </summary>
    public static string ArchivoBaseDatos { get; } = Path.Combine(CarpetaDatos, "sigem.db");

    /// <summary>Cadena de conexion SQLite por omision, si appsettings.json no trae una.</summary>
    public static string CadenaConexionPorOmision => "Data Source=" + ArchivoBaseDatos;

    /// <summary>Plantilla del archivo de registro diario.</summary>
    public static string ArchivoRegistro { get; } = Path.Combine(CarpetaRegistros, "sigem-.log");

    /// <summary>
    /// Registro de emergencia. Se usa cuando falla algo antes de que exista
    /// el contenedor de dependencias y por lo tanto no hay ILogger disponible.
    /// </summary>
    public static string ArchivoRegistroEmergencia { get; } = Path.Combine(CarpetaRegistros, "arranque-critico.log");

    /// <summary>Crea las carpetas si no existen. Seguro de llamar varias veces.</summary>
    public static void Asegurar()
    {
        Directory.CreateDirectory(CarpetaDatos);
        Directory.CreateDirectory(CarpetaRegistros);
    }
}
