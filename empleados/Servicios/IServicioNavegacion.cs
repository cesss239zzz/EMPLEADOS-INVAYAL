namespace empleados.Servicios;

/// <summary>
/// Navegacion entre pantallas. Envuelve a Shell para que los ViewModel no dependan
/// de la interfaz (CLAUDE.md, regla 11).
/// </summary>
public interface IServicioNavegacion
{
    /// <summary>Navega a una ruta declarada en <see cref="RutasNavegacion"/>.</summary>
    Task IrAsync(string ruta);
}

/// <summary>
/// Rutas de navegacion. Todas son ShellContent de primer nivel declarados en
/// AppShell, por eso llevan el prefijo // y no se pasan por Routing.RegisterRoute:
/// registrar una ruta que ya es ShellContent lanza excepcion. Las paginas de
/// detalle que lleguen en etapas siguientes si se registran ahi.
/// </summary>
public static class RutasNavegacion
{
    /// <summary>Verificacion de infraestructura al arrancar.</summary>
    public const string Arranque = "//arranque";

    /// <summary>Diagnostico de infraestructura.</summary>
    public const string Diagnostico = "//diagnostico";

    /// <summary>Acceso con usuario y contrasena.</summary>
    public const string Acceso = "//acceso";

    /// <summary>Selector de empresa. Sin empresa activa no se entra al sistema.</summary>
    public const string Empresas = "//empresas";

    /// <summary>Pantalla principal. La reemplaza el Shell completo en E4.</summary>
    public const string Inicio = "//inicio";
}
