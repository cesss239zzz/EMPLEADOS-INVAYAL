namespace empleados.Configuracion;

/// <summary>
/// Opciones generales de la aplicacion, leidas de appsettings.json.
/// Se registra como singleton en MauiProgram.
/// </summary>
public sealed class OpcionesSigem
{
    /// <summary>Nombre de la seccion en appsettings.json.</summary>
    public const string Seccion = "Sigem";

    /// <summary>Segundos que se espera al servidor MySQL antes de darlo por inaccesible.</summary>
    public int SegundosEsperaConexion { get; set; } = 5;

    /// <summary>Nivel minimo que se escribe al archivo de registro.</summary>
    public string NivelRegistroMinimo { get; set; } = "Information";

    /// <summary>Dias que se conservan los archivos de registro diarios.</summary>
    public int DiasRetencionRegistros { get; set; } = 14;

    /// <summary>Cadena de conexion a MySQL. Se asigna aparte, desde ConnectionStrings:Sigem.</summary>
    public string CadenaConexion { get; set; } = string.Empty;

    /// <summary>Ruta del archivo appsettings.json que se cargo, para mostrarla en diagnostico.</summary>
    public string RutaArchivoConfiguracion { get; set; } = string.Empty;
}
