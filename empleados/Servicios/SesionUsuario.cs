using empleados.Datos.Entidades;

namespace empleados.Servicios;

/// <summary>
/// Sesion viva del usuario autenticado. Singleton (CLAUDE.md, regla 1).
/// No guarda ni la contrasena ni su hash: una vez verificada, no hace falta.
/// </summary>
public sealed class SesionUsuario
{
    public int UsuarioId { get; private set; }
    public string NombreUsuario { get; private set; } = string.Empty;
    public string NombreCompleto { get; private set; } = string.Empty;
    public PerfilUsuario Perfil { get; private set; }

    /// <summary>Sucursal a la que se limita un Supervisor de sucursal.</summary>
    public int? SucursalId { get; private set; }

    /// <summary>Marcado cuando el usuario todavia usa la contrasena inicial.</summary>
    public bool DebeCambiarContrasena { get; private set; }

    public bool EstaAutenticado => UsuarioId > 0;

    /// <summary>
    /// El salario es dato sensible: solo lo ve Administrador o superior.
    /// Se consulta desde la vista para ocultar la columna, y desde el servicio
    /// para no traerlo siquiera de la base.
    /// </summary>
    public bool PuedeVerSalarios =>
        EstaAutenticado && Perfil is (PerfilUsuario.SuperAdministrador or PerfilUsuario.Administrador);

    /// <summary>Solo el SuperAdministrador cambia de empresa libremente.</summary>
    public bool PuedeElegirEmpresa => EstaAutenticado && Perfil is PerfilUsuario.SuperAdministrador;

    /// <summary>
    /// El alta y la edicion de expedientes quedan para Administrador o superior.
    /// Los perfiles de Supervisor y Consulta trabajan en modo lectura.
    /// </summary>
    public bool PuedeCapturar =>
        EstaAutenticado && Perfil is (PerfilUsuario.SuperAdministrador or PerfilUsuario.Administrador);

    public void Iniciar(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        UsuarioId = usuario.Id;
        NombreUsuario = usuario.NombreUsuario;
        NombreCompleto = usuario.NombreCompleto;
        Perfil = usuario.Perfil;
        SucursalId = usuario.SucursalId;
        DebeCambiarContrasena = usuario.DebeCambiarContrasena;
    }

    public void Cerrar()
    {
        UsuarioId = 0;
        NombreUsuario = string.Empty;
        NombreCompleto = string.Empty;
        Perfil = default;
        SucursalId = null;
        DebeCambiarContrasena = false;
    }
}
