using empleados.Datos.Entidades;

namespace empleados.Servicios;

/// <summary>Completitud del expediente. Tiñe el borde izquierdo de la fila.</summary>
public enum EstadoExpediente
{
    Completo = 1,
    Parcial = 2,
    Incompleto = 3
}

/// <summary>
/// Una fila de la tabla de colaboradores. Trae solo lo que la tabla dibuja: no
/// se arrastra la entidad entera ni sus relaciones (CLAUDE.md, regla 13).
/// </summary>
/// <param name="Id">Identificador del colaborador.</param>
/// <param name="Codigo">Codigo de expediente.</param>
/// <param name="NombreCompleto">Nombre armado.</param>
/// <param name="Identidad">Numero de identidad.</param>
/// <param name="Puesto">Puesto actual.</param>
/// <param name="Departamento">Departamento actual.</param>
/// <param name="Sucursal">Sucursal a la que pertenece.</param>
/// <param name="FechaIngreso">Fecha de ingreso.</param>
/// <param name="Estado">Situacion laboral.</param>
/// <param name="Expediente">Completitud documental.</param>
/// <param name="Salario">Nulo cuando el perfil no tiene permiso de verlo.</param>
public sealed record FilaColaborador(
    int Id,
    string Codigo,
    string NombreCompleto,
    string? Identidad,
    string? Puesto,
    string? Departamento,
    string? Sucursal,
    DateTime FechaIngreso,
    EstadoColaborador Estado,
    EstadoExpediente Expediente,
    decimal? Salario,
    bool PuedeVerSalario)
{
    /// <summary>Lo que se muestra cuando un campo opcional no tiene dato (CR-04).</summary>
    public const string SinDato = "Sin registrar";

    /// <summary>Fecha en formato hondureno.</summary>
    public string FechaIngresoTexto => FechaIngreso.ToString("dd/MM/yyyy");

    public string IdentidadTexto => Mostrar(Identidad);
    public string PuestoTexto => Mostrar(Puesto);
    public string DepartamentoTexto => Mostrar(Departamento);
    public string SucursalTexto => Mostrar(Sucursal);

    /// <summary>
    /// Importe en lempiras. Distingue tres cosas que no son lo mismo: que el
    /// perfil no pueda verlo, que no se haya capturado y que valga algo.
    /// </summary>
    public string SalarioTexto => !PuedeVerSalario ? "———"
        : Salario is null ? SinDato
        : Salario.Value.ToString("C");

    private static string Mostrar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? SinDato : valor;

    public string EstadoTexto => Estado switch
    {
        EstadoColaborador.Activo => "Activo",
        EstadoColaborador.Suspendido => "Suspendido",
        EstadoColaborador.Inactivo => "Inactivo",
        _ => "?"
    };

    public string ExpedienteTexto => Expediente switch
    {
        EstadoExpediente.Completo => "Completo",
        EstadoExpediente.Parcial => "Parcial",
        _ => "Incompleto"
    };

    /// <summary>
    /// Iniciales para el avatar del directorio. No hay fotografias en el
    /// expediente todavia, asi que la pastilla lleva las dos iniciales.
    /// </summary>
    public string Iniciales
    {
        get
        {
            var partes = NombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0)
            {
                return "?";
            }

            var primera = partes[0][..1];
            var segunda = partes.Length > 2 ? partes[2][..1] : partes.Length > 1 ? partes[1][..1] : string.Empty;
            return (primera + segunda).ToUpperInvariant();
        }
    }
}

/// <summary>
/// Criterios de busqueda. Todos opcionales; los nulos no restringen.
/// Se traducen a SQL, nunca se aplican en memoria (CLAUDE.md, regla 13).
/// </summary>
/// <param name="Texto">Busca en nombre, apellidos, codigo e identidad.</param>
/// <param name="DepartamentoId">Restringe a un departamento.</param>
/// <param name="SucursalId">Restringe a una sucursal.</param>
/// <param name="Estado">Restringe a una situacion laboral.</param>
/// <param name="Tope">
/// Cuantas filas como maximo. Lo usa el directorio rapido del resumen, que solo
/// pinta las primeras: el recorte viaja a SQL como LIMIT, no se recorta la lista
/// en memoria (CLAUDE.md, regla 13).
/// </param>
public sealed record FiltroColaboradores(
    string? Texto = null,
    int? DepartamentoId = null,
    int? SucursalId = null,
    EstadoColaborador? Estado = null,
    int? Tope = null)
{
    /// <summary>
    /// Verdadero si no hay ningun criterio activo. El tope no cuenta: recorta
    /// cuantas filas se piden, no cuales.
    /// </summary>
    public bool EstaVacio =>
        string.IsNullOrWhiteSpace(Texto)
        && DepartamentoId is null
        && SucursalId is null
        && Estado is null;
}

/// <summary>Una opcion de un desplegable de filtro.</summary>
/// <param name="Id">Identificador, o cero para la opcion "todos".</param>
/// <param name="Nombre">Texto que ve el usuario.</param>
public sealed record OpcionFiltro(int Id, string Nombre)
{
    /// <summary>Verdadero en la opcion que no restringe nada.</summary>
    public bool EsTodos => Id == 0;
}

/// <summary>Contenido de los desplegables de filtro para la empresa activa.</summary>
/// <param name="Departamentos">Departamentos, con "Todos" al inicio.</param>
/// <param name="Sucursales">Sucursales, con "Todas" al inicio.</param>
/// <param name="TotalColaboradores">Cuantos hay sin filtrar.</param>
public sealed record OpcionesFiltro(
    IReadOnlyList<OpcionFiltro> Departamentos,
    IReadOnlyList<OpcionFiltro> Sucursales,
    int TotalColaboradores);

/// <summary>
/// Una opcion de un desplegable del formulario de captura. A diferencia de
/// <see cref="OpcionFiltro"/> nunca lleva la fila "todos": en el alta hay que
/// elegir una sucursal, un departamento y un puesto reales.
/// </summary>
/// <param name="Id">Identificador del catalogo.</param>
/// <param name="Nombre">Texto que ve el usuario.</param>
/// <param name="DepartamentoId">
/// Solo lo llevan los puestos: sirve para acotar el desplegable de puestos al
/// departamento elegido. Nulo en sucursales y departamentos.
/// </param>
public sealed record OpcionCatalogo(int Id, string Nombre, int? DepartamentoId = null);

/// <summary>
/// Catalogos que alimentan los desplegables del formulario de captura, ya
/// filtrados por la empresa activa. Son listas de catalogo (pocas filas), no
/// tablas de negocio: traerlas enteras no contradice la regla 13.
/// </summary>
public sealed record CatalogosEdicion(
    IReadOnlyList<OpcionCatalogo> Sucursales,
    IReadOnlyList<OpcionCatalogo> Departamentos,
    IReadOnlyList<OpcionCatalogo> Puestos);

/// <summary>
/// Datos editables de un colaborador. Viaja del servicio al formulario cuando
/// se abre para editar, y del formulario al servicio cuando se guarda. Cubre
/// solo la cabecera del expediente; contactos, contratos y documentos son de
/// una sub-etapa posterior.
/// </summary>
public sealed class DatosEdicionColaborador
{
    /// <summary>Cero en el alta; el identificador del colaborador en la edicion.</summary>
    public int Id { get; set; }

    // Obligatorios (CR-04).
    public string Codigo { get; set; } = string.Empty;
    public string PrimerNombre { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    public DateTime FechaIngreso { get; set; }

    // Opcionales: nulo significa "todavia sin capturar".
    public string? Identidad { get; set; }
    public string? SegundoNombre { get; set; }
    public string? SegundoApellido { get; set; }

    public Sexo? Sexo { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public EstadoColaborador Estado { get; set; } = EstadoColaborador.Activo;

    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Direccion { get; set; }

    /// <summary>Importe en decimal, jamas en double ni float (CLAUDE.md, regla 10).</summary>
    public decimal? SalarioBase { get; set; }

    public int? SucursalId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }

    public bool EsAlta => Id == 0;
}

/// <summary>
/// Resultado de guardar un colaborador. Separa el fallo de negocio previsible
/// —una identidad repetida, un catalogo sin elegir— del fallo de infraestructura,
/// que sube como excepcion y lo maneja el bloque protegido del ViewModel.
/// </summary>
/// <param name="Exito">Verdadero si se guardo.</param>
/// <param name="Id">Identificador del colaborador guardado.</param>
/// <param name="Error">Mensaje en espanol cuando <paramref name="Exito"/> es falso.</param>
public sealed record ResultadoGuardado(bool Exito, int Id, string? Error)
{
    public static ResultadoGuardado Ok(int id) => new(true, id, null);
    public static ResultadoGuardado Falla(string mensaje) => new(false, 0, mensaje);
}

/// <summary>Consulta y captura de colaboradores de la empresa activa.</summary>
public interface IServicioColaboradores
{
    /// <summary>
    /// Colaboradores de la empresa activa que cumplen el filtro. El aislamiento
    /// entre empresas lo pone el filtro global: aca no hay ningun Where por
    /// EmpresaId.
    /// </summary>
    Task<IReadOnlyList<FilaColaborador>> ObtenerAsync(
        FiltroColaboradores filtro,
        CancellationToken cancelacion = default);

    /// <summary>Opciones de los desplegables y total sin filtrar.</summary>
    Task<OpcionesFiltro> ObtenerOpcionesAsync(CancellationToken cancelacion = default);

    /// <summary>Catalogos para los desplegables del formulario de captura.</summary>
    Task<CatalogosEdicion> ObtenerCatalogosEdicionAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Datos editables del colaborador, o nulo si no existe en la empresa activa.
    /// </summary>
    Task<DatosEdicionColaborador?> ObtenerParaEdicionAsync(
        int colaboradorId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Da de alta un colaborador nuevo (Id cero) o actualiza uno existente. La
    /// empresa se asigna desde el contexto activo, nunca desde el formulario.
    /// Devuelve un fallo controlado si la identidad o el codigo ya existen.
    /// </summary>
    Task<ResultadoGuardado> GuardarAsync(
        DatosEdicionColaborador datos,
        CancellationToken cancelacion = default);
}
