namespace empleados.Datos.Entidades;

/// <summary>Raiz de toda entidad persistida.</summary>
public abstract class EntidadBase
{
    /// <summary>Clave primaria autoincremental.</summary>
    public int Id { get; set; }

    /// <summary>Marca de creacion del registro.</summary>
    public DateTime FechaCreacion { get; set; }
}

/// <summary>
/// Entidad que pertenece a una empresa. Toda clase que herede de aca recibe
/// automaticamente el filtro global de consulta por <see cref="EmpresaId"/>
/// (CLAUDE.md, regla 9). Nunca se filtra a mano en un repositorio.
/// </summary>
public abstract class EntidadEmpresa : EntidadBase
{
    /// <summary>Empresa dueña del registro. Es la frontera de aislamiento.</summary>
    public int EmpresaId { get; set; }
}
