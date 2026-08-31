namespace empleados.Servicios;

/// <summary>
/// Catalogos que el usuario administra desde Configuracion. Cada uno es una
/// tabla propia, pero todos comparten la misma forma —nombre, activo, en uso—
/// asi que se manejan con un unico servicio en vez de cinco casi identicos.
///
/// Los valores se persisten explicitos: agregar un catalogo nuevo no debe correr
/// la numeracion de los ya guardados en preferencias de pantalla.
/// </summary>
public enum TipoCatalogo
{
    Departamento = 1,
    Puesto = 2,
    Sucursal = 3,
    TipoDocumento = 4,
    TipoContrato = 5
}

/// <summary>Una fila de la tabla de un catalogo.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Nombre">Texto que ve el usuario.</param>
/// <param name="Activo">Si sigue disponible para nuevas selecciones.</param>
/// <param name="EnUso">Cuantos registros lo usan. Cero permite eliminarlo.</param>
/// <param name="Detalle">Linea secundaria: el departamento del puesto, el codigo de la sucursal, la vigencia del tipo de documento.</param>
public sealed record FilaCatalogo(
    int Id,
    string Nombre,
    bool Activo,
    int EnUso,
    string Detalle)
{
    /// <summary>Se puede borrar de verdad solo si nadie lo usa.</summary>
    public bool SePuedeEliminar => EnUso == 0;

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";

    public string UsoTexto => EnUso switch
    {
        0 => "Sin uso",
        1 => "1 registro",
        _ => EnUso + " registros"
    };

    /// <summary>Texto del boton que alterna activo e inactivo.</summary>
    public string AccionActivarTexto => Activo ? "Desactivar" : "Activar";
}

/// <summary>
/// Datos editables de un valor de catalogo. Lleva los campos de los cinco
/// catalogos; cada pantalla muestra los que le tocan y el servicio ignora el
/// resto segun el <see cref="Tipo"/>.
/// </summary>
public sealed class DatosCatalogo
{
    public TipoCatalogo Tipo { get; set; }

    /// <summary>Cero en el alta; el identificador del valor en la edicion.</summary>
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    // ─── Solo sucursal ──────────────────────────────────────────────────────
    public string Codigo { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;

    // ─── Solo puesto ────────────────────────────────────────────────────────

    /// <summary>Departamento al que pertenece el puesto. Opcional (CR-05).</summary>
    public int? DepartamentoId { get; set; }

    // ─── Solo tipo de documento y tipo de contrato ──────────────────────────
    public bool RequiereVencimiento { get; set; }

    /// <summary>Vigencia en meses del tipo de documento. Nula si no vence.</summary>
    public int? MesesVigencia { get; set; }

    /// <summary>Dias de anticipacion con que se avisa el vencimiento.</summary>
    public int DiasAvisoAnticipado { get; set; } = 30;

    /// <summary>Escalones de recordatorio separados por coma: "30,15,5" (CR-10).</summary>
    public string EscalaAviso { get; set; } = "30,15,5";

    public bool EsAlta => Id == 0;
}

/// <summary>
/// Administracion de los catalogos de la empresa activa.
///
/// Los desplegables del sistema leen SIEMPRE de aqui, nunca de una lista fija
/// en codigo (solicitud de cambios, CR-06). Los catalogos son por empresa: el
/// aislamiento lo pone el filtro global, no un Where a mano.
/// </summary>
public interface IServicioCatalogos
{
    /// <summary>
    /// Valores de un catalogo, en orden alfabetico y con la cuenta de uso.
    /// </summary>
    /// <param name="tipo">Catalogo que se consulta.</param>
    /// <param name="busqueda">Filtro por nombre. Vacio trae todo.</param>
    /// <param name="incluirInactivos">Si trae tambien los desactivados.</param>
    Task<IReadOnlyList<FilaCatalogo>> ObtenerAsync(
        TipoCatalogo tipo,
        string? busqueda = null,
        bool incluirInactivos = true,
        CancellationToken cancelacion = default);

    /// <summary>Datos de un valor para abrir el formulario de edicion.</summary>
    Task<DatosCatalogo?> ObtenerParaEdicionAsync(
        TipoCatalogo tipo, int id, CancellationToken cancelacion = default);

    /// <summary>Crea o modifica un valor de catalogo.</summary>
    Task<ResultadoGuardado> GuardarAsync(DatosCatalogo datos, CancellationToken cancelacion = default);

    /// <summary>
    /// Elimina un valor. Falla de forma controlada si algun registro lo usa:
    /// en ese caso el usuario debe desactivarlo, no borrarlo.
    /// </summary>
    Task<ResultadoGuardado> EliminarAsync(
        TipoCatalogo tipo, int id, CancellationToken cancelacion = default);

    /// <summary>Activa o desactiva un valor sin borrarlo.</summary>
    Task<ResultadoGuardado> CambiarActivoAsync(
        TipoCatalogo tipo, int id, bool activo, CancellationToken cancelacion = default);

    /// <summary>Departamentos activos, para el desplegable del formulario de puesto.</summary>
    Task<IReadOnlyList<OpcionCatalogo>> ObtenerDepartamentosActivosAsync(
        CancellationToken cancelacion = default);
}
