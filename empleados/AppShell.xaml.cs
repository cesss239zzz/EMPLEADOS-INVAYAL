using empleados.Servicios;
using empleados.Vistas;
using Microsoft.Extensions.DependencyInjection;

namespace empleados;

/// <summary>
/// Contenedor de navegacion. Arma los ShellContent con fabricas que resuelven cada
/// pagina del contenedor de dependencias, de forma perezosa: la pagina se construye
/// cuando se navega a ella, no al arrancar.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell(IServiceProvider proveedor)
    {
        InitializeComponent();

        Items.Add(CrearContenido(RutasNavegacion.Arranque, () => proveedor.GetRequiredService<PaginaArranque>()));
        Items.Add(CrearContenido(RutasNavegacion.Diagnostico, () => proveedor.GetRequiredService<PaginaDiagnostico>()));
        Items.Add(CrearContenido(RutasNavegacion.Acceso, () => proveedor.GetRequiredService<PaginaAcceso>()));
        Items.Add(CrearContenido(RutasNavegacion.Empresas, () => proveedor.GetRequiredService<PaginaSelectorEmpresa>()));
        Items.Add(CrearContenido(RutasNavegacion.Inicio, () => proveedor.GetRequiredService<PaginaPrincipal>()));
    }

    /// <summary>Crea un ShellContent de primer nivel a partir de una fabrica de pagina.</summary>
    /// <param name="rutaAbsoluta">Ruta con el prefijo //, tal como se declara en RutasNavegacion.</param>
    /// <param name="fabrica">Resuelve la pagina desde el contenedor.</param>
    private static ShellContent CrearContenido(string rutaAbsoluta, Func<object> fabrica) => new()
    {
        Route = rutaAbsoluta.TrimStart('/'),
        ContentTemplate = new DataTemplate(fabrica)
    };
}
