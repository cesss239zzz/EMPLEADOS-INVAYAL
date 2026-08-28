namespace empleados.Servicios;

/// <summary>
/// Estado vivo de la aplicacion, compartido entre pantallas. Se registra como
/// singleton (CLAUDE.md, regla 1). En etapas posteriores aloja tambien el usuario
/// autenticado y la empresa activa.
/// </summary>
public sealed class EstadoAplicacion
{
    /// <summary>Ultimo resultado de la verificacion de infraestructura.</summary>
    public ResultadoDiagnostico? UltimoDiagnostico { get; set; }
}
