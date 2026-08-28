namespace empleados.Datos.Entidades;

/// <summary>
/// Empresa del grupo. Es la unidad de aislamiento: no hereda de EntidadEmpresa
/// porque ella misma ES el inquilino, y por eso no lleva filtro global.
/// </summary>
public class Empresa : EntidadBase
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Nombre corto para la barra superior y los selectores.</summary>
    public string NombreCorto { get; set; } = string.Empty;

    /// <summary>Registro Tributario Nacional.</summary>
    public string Rtn { get; set; } = string.Empty;

    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;

    /// <summary>
    /// Color de identidad, en formato #RRGGBB. Tiñe la barra superior para que
    /// se vea de un vistazo en que empresa se esta trabajando.
    /// </summary>
    public string ColorPrimario { get; set; } = "#0F3D6E";

    public bool Activa { get; set; } = true;

    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
}

/// <summary>
/// Usuario del sistema. No hereda de EntidadEmpresa: el SuperAdministrador
/// atraviesa empresas, y el acceso se modela con <see cref="UsuarioEmpresa"/>.
/// </summary>
public class Usuario : EntidadBase
{
    public string NombreUsuario { get; set; } = string.Empty;

    /// <summary>Hash BCrypt con factor 11. Nunca la contrasena (CLAUDE.md, regla 12).</summary>
    public string HashContrasena { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public PerfilUsuario Perfil { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>Obliga a cambiar la contrasena en el primer acceso.</summary>
    public bool DebeCambiarContrasena { get; set; }

    /// <summary>Intentos fallidos consecutivos. Se reinicia al entrar bien.</summary>
    public int IntentosFallidos { get; set; }

    /// <summary>Si tiene valor y esta en el futuro, el acceso esta bloqueado.</summary>
    public DateTime? BloqueadoHasta { get; set; }

    public DateTime? UltimoAcceso { get; set; }

    /// <summary>Sucursal a la que se limita un Supervisor de sucursal. Nula para los demas.</summary>
    public int? SucursalId { get; set; }

    public ICollection<UsuarioEmpresa> Empresas { get; set; } = new List<UsuarioEmpresa>();
}

/// <summary>Empresas a las que un usuario tiene acceso.</summary>
public class UsuarioEmpresa : EntidadBase
{
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
}

/// <summary>Sucursal o centro de trabajo de una empresa.</summary>
public class Sucursal : EntidadEmpresa
{
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;

    public Empresa? Empresa { get; set; }
}

/// <summary>Departamento o area organizativa.</summary>
public class Departamento : EntidadEmpresa
{
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

/// <summary>Puesto de trabajo. Pertenece a un departamento.</summary>
public class Puesto : EntidadEmpresa
{
    public string Nombre { get; set; } = string.Empty;
    public int DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Modalidad contractual: indefinido, temporal, por obra.</summary>
public class TipoContrato : EntidadEmpresa
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Si es verdadero, el contrato exige fecha de vencimiento.</summary>
    public bool RequiereVencimiento { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>Clase de documento del expediente: identidad, RTN, titulo, carnet.</summary>
public class TipoDocumento : EntidadEmpresa
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Si vence, el motor de alertas vigila su fecha de vencimiento.</summary>
    public bool RequiereVencimiento { get; set; }

    /// <summary>Dias de anticipacion con que se avisa antes del vencimiento.</summary>
    public int DiasAvisoAnticipado { get; set; } = 30;

    public bool Activo { get; set; } = true;
}
