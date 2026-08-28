namespace empleados.Servicios;

/// <summary>
/// Las cuatro metricas de la pantalla de resumen, ya calculadas. Cada una sale
/// de un COUNT en la base: no se trae ninguna tabla a memoria para contarla
/// (CLAUDE.md, regla 13).
/// </summary>
/// <param name="ColaboradoresActivos">Colaboradores en situacion activa.</param>
/// <param name="ColaboradoresRegistrados">Expedientes abiertos, cualquiera sea su situacion.</param>
/// <param name="CumpleanosDelMes">Cumpleanos que caen en el mes en curso.</param>
/// <param name="ProximoCumpleanos">Cuando cae el siguiente, o vacio si ya pasaron todos.</param>
/// <param name="DocumentosPorVencer">Documentos que vencen dentro de la ventana de aviso.</param>
/// <param name="ContratosPorVencer">Contratos vigentes que terminan dentro de la ventana.</param>
public sealed record ResumenGeneral(
    int ColaboradoresActivos,
    int ColaboradoresRegistrados,
    int CumpleanosDelMes,
    string ProximoCumpleanos,
    int DocumentosPorVencer,
    int ContratosPorVencer)
{
    /// <summary>Ventana de aviso de las dos tarjetas de vencimiento.</summary>
    public const int DiasDeVentana = 30;

    /// <summary>Pie de la tarjeta de colaboradores.</summary>
    public string DetalleColaboradores =>
        ColaboradoresActivos == ColaboradoresRegistrados
            ? "Todos los expedientes activos"
            : ColaboradoresActivos + " activos de " + ColaboradoresRegistrados + " expedientes";

    /// <summary>Pie de la tarjeta de cumpleanos.</summary>
    public string DetalleCumpleanos => string.IsNullOrEmpty(ProximoCumpleanos)
        ? "Ninguno pendiente este mes"
        : "Proximo: " + ProximoCumpleanos;

    /// <summary>Verdadero cuando la tarjeta de documentos debe encenderse en ambar.</summary>
    public bool HayDocumentosPorVencer => DocumentosPorVencer > 0;

    /// <summary>Verdadero cuando la tarjeta de contratos debe encenderse en rojo.</summary>
    public bool HayContratosPorVencer => ContratosPorVencer > 0;

    /// <summary>Pie de la tarjeta de contratos.</summary>
    public string DetalleContratos => HayContratosPorVencer
        ? "Requiere accion inmediata"
        : "Sin vencimientos proximos";

    /// <summary>Pie de la tarjeta de documentos.</summary>
    public string DetalleDocumentos => "(Proximos " + DiasDeVentana + " dias)";

    /// <summary>Resumen vacio, para la pantalla antes de la primera consulta.</summary>
    public static ResumenGeneral Vacio { get; } = new(0, 0, 0, string.Empty, 0, 0);
}

/// <summary>Metricas de la pantalla de resumen para la empresa activa.</summary>
public interface IServicioResumen
{
    /// <summary>
    /// Calcula las metricas de la empresa activa. El aislamiento lo pone el
    /// filtro global: aca no hay ningun Where por EmpresaId.
    /// </summary>
    Task<ResumenGeneral> ObtenerAsync(CancellationToken cancelacion = default);
}
