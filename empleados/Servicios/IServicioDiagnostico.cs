namespace empleados.Servicios;

/// <summary>
/// Verifica que la infraestructura este lista antes de mostrar la pantalla de acceso.
/// Regla 8 de CLAUDE.md: un fallo de infraestructura se muestra, no se lanza.
/// </summary>
public interface IServicioDiagnostico
{
    /// <summary>Comprueba configuracion, servidor MySQL y existencia de la base.</summary>
    Task<ResultadoDiagnostico> VerificarAsync(CancellationToken cancelacion = default);
}
