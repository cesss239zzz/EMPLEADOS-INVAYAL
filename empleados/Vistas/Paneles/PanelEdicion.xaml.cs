namespace empleados.Vistas.Paneles;

/// <summary>
/// Formulario de alta y edicion de colaboradores. Solo constructor y
/// InitializeComponent: toda la logica vive en VistaModeloPrincipal, del que
/// hereda el BindingContext (CLAUDE.md, regla 11).
/// </summary>
public partial class PanelEdicion : ContentView
{
    public PanelEdicion() => InitializeComponent();
}
