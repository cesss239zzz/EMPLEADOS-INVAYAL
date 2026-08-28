namespace empleados.Vistas.Paneles;

/// <summary>
/// Codigo subyacente sin logica: solo el constructor (CLAUDE.md, regla 11).
///
/// Los paneles no fijan BindingContext: lo heredan de PaginaPrincipal, que es
/// quien recibe el ViewModel del contenedor de dependencias. Asi hay un solo
/// ViewModel para toda la ventana y los paneles no se resuelven por DI.
/// </summary>
public partial class PanelResumen : ContentView
{
    public PanelResumen() => InitializeComponent();
}
