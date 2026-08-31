namespace empleados.Servicios;

/// <summary>Empresa tal como se muestra en el selector. Solo lo que la tarjeta necesita.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Nombre">Razon social completa.</param>
/// <param name="NombreCorto">Nombre para la barra superior.</param>
/// <param name="Rtn">Registro Tributario Nacional.</param>
/// <param name="ColorPrimario">Color de identidad, formato #RRGGBB.</param>
/// <param name="Colaboradores">Cuantas personas activas tiene.</param>
/// <param name="Sucursales">Cuantas sucursales tiene.</param>
public sealed record TarjetaEmpresa(
    int Id,
    string Nombre,
    string NombreCorto,
    string Rtn,
    string ColorPrimario,
    int Colaboradores,
    int Sucursales);

/// <summary>
/// Datos editables de una empresa. Viaja del formulario al servicio al crear o
/// al modificar. <see cref="Id"/> vale cero en el alta.
/// </summary>
public sealed class DatosEmpresa
{
    /// <summary>Cero en el alta; el identificador de la empresa en la edicion.</summary>
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string NombreCorto { get; set; } = string.Empty;
    public string Rtn { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string ColorPrimario { get; set; } = "#0F3D6E";

    public bool EsAlta => Id == 0;
}

/// <summary>
/// Cuenta de lo que se perderia al eliminar una empresa. Se le muestra al
/// usuario ANTES de pedirle la confirmacion escrita, porque un borrado a ciegas
/// de un expediente laboral no tiene vuelta atras.
/// </summary>
public sealed record ResumenBorradoEmpresa(
    string Nombre,
    int Sucursales,
    int Colaboradores,
    int Documentos,
    int Contratos)
{
    /// <summary>Verdadero cuando la empresa no arrastra ningun dato de negocio.</summary>
    public bool EstaVacia => Sucursales == 0 && Colaboradores == 0 && Documentos == 0 && Contratos == 0;
}

/// <summary>Alta, consulta, modificacion y eliminacion de empresas.</summary>
public interface IServicioEmpresas
{
    /// <summary>
    /// Empresas visibles para el usuario de la sesion. Se consulta al abrir el
    /// selector, nunca al arrancar (CLAUDE.md, regla 13).
    /// </summary>
    Task<IReadOnlyList<TarjetaEmpresa>> ObtenerDisponiblesAsync(CancellationToken cancelacion = default);

    /// <summary>Fija la empresa activa y deja el filtro global apuntando a ella.</summary>
    Task ActivarAsync(int empresaId, CancellationToken cancelacion = default);

    /// <summary>Datos de una empresa para abrir el formulario de edicion.</summary>
    Task<DatosEmpresa?> ObtenerParaEdicionAsync(int empresaId, CancellationToken cancelacion = default);

    /// <summary>Crea o modifica una empresa segun venga o no con identificador.</summary>
    Task<ResultadoGuardado> GuardarAsync(DatosEmpresa datos, CancellationToken cancelacion = default);

    /// <summary>
    /// Cuenta lo que arrastraria el borrado de una empresa, para advertirlo antes
    /// de ejecutarlo. No modifica nada.
    /// </summary>
    Task<ResumenBorradoEmpresa?> ResumirBorradoAsync(int empresaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Elimina una empresa y todo lo que cuelga de ella: sucursales, catalogos,
    /// colaboradores, contratos, documentos y avisos. Es un borrado definitivo.
    ///
    /// Se puede eliminar la ultima empresa y dejar el sistema en cero: es un
    /// requisito del cliente (solicitud de cambios, CR-02), no un descuido.
    /// Quien llama debe haber confirmado por escrito con el usuario.
    /// </summary>
    Task<ResultadoGuardado> EliminarAsync(int empresaId, CancellationToken cancelacion = default);
}
