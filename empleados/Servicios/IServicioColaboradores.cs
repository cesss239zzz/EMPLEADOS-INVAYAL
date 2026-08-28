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
    string Identidad,
    string Puesto,
    string Departamento,
    string Sucursal,
    DateTime FechaIngreso,
    EstadoColaborador Estado,
    EstadoExpediente Expediente,
    decimal? Salario)
{
    /// <summary>Fecha en formato hondureno.</summary>
    public string FechaIngresoTexto => FechaIngreso.ToString("dd/MM/yyyy");

    /// <summary>Importe en lempiras, o guiones si el perfil no puede verlo.</summary>
    public string SalarioTexto => Salario is null ? "———" : Salario.Value.ToString("C");

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

/// <summary>Consulta de colaboradores de la empresa activa.</summary>
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
}
