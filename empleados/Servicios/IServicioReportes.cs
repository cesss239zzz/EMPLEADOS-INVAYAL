namespace empleados.Servicios;

/// <summary>Formato de salida de una exportacion.</summary>
public enum FormatoReporte
{
    Excel = 1,
    Pdf = 2
}

/// <summary>
/// Generacion de reportes de la empresa activa: listado de colaboradores en PDF
/// y Excel, ficha en PDF y constancia de trabajo en PDF.
///
/// La constancia se entrega sin codigo QR en esta etapa: el QR verificable
/// necesita un paquete que todavia no esta autorizado (CLAUDE.md prohibe agregar
/// paquetes fuera de la lista sin permiso). Todo lo demas queda operativo.
/// </summary>
public interface IServicioReportes
{
    /// <summary>Exporta el directorio de colaboradores al formato pedido. Devuelve la ruta del archivo.</summary>
    Task<string> ExportarColaboradoresAsync(
        FiltroColaboradores filtro,
        FormatoReporte formato,
        CancellationToken cancelacion = default);

    /// <summary>Genera el PDF de la ficha de un colaborador. Devuelve la ruta del archivo.</summary>
    Task<string> ExportarFichaPdfAsync(int colaboradorId, CancellationToken cancelacion = default);

    /// <summary>Genera la constancia de trabajo en PDF. Devuelve la ruta del archivo.</summary>
    Task<string> GenerarConstanciaPdfAsync(int colaboradorId, CancellationToken cancelacion = default);
}
