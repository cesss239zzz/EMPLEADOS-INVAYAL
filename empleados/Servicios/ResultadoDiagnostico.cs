namespace empleados.Servicios;

/// <summary>Clasificacion del estado de la infraestructura al arrancar.</summary>
public enum EstadoDiagnostico
{
    /// <summary>Todo correcto: la base existe, abre y tiene las migraciones aplicadas.</summary>
    Correcto,
    /// <summary>No se pudo determinar donde vive la base de datos.</summary>
    SinConfiguracion,
    /// <summary>La carpeta de datos no existe o no se puede escribir en ella.</summary>
    CarpetaNoEscribible,
    /// <summary>El archivo de base existe pero no se puede abrir.</summary>
    BaseInaccesible,
    /// <summary>La base abre pero las migraciones no se pudieron aplicar.</summary>
    MigracionFallida,
    /// <summary>Cualquier otro fallo al verificar la infraestructura.</summary>
    ErrorDesconocido
}

/// <summary>
/// Resultado de la verificacion de infraestructura. Nunca se le muestra al usuario la
/// excepcion cruda: <see cref="DetalleTecnico"/> es opcional y se despliega aparte.
/// </summary>
/// <param name="Estado">Clasificacion del problema.</param>
/// <param name="Titulo">Titulo corto en espanol para el usuario.</param>
/// <param name="Explicacion">Que esta pasando, en lenguaje comprensible.</param>
/// <param name="ComoResolverlo">Pasos concretos para que el usuario lo arregle.</param>
/// <param name="DetalleTecnico">Traza para el tecnico. Puede ser nula.</param>
public sealed record ResultadoDiagnostico(
    EstadoDiagnostico Estado,
    string Titulo,
    string Explicacion,
    string ComoResolverlo,
    string? DetalleTecnico = null)
{
    /// <summary>Verdadero si la aplicacion puede continuar hacia la pantalla de acceso.</summary>
    public bool EsCorrecto => Estado == EstadoDiagnostico.Correcto;
}
