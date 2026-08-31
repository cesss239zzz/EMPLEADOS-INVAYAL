namespace empleados.Vistas.Paneles;

/// <summary>
/// Formulario de novedad: registra una incidencia o programa vacaciones. Solo
/// constructor y InitializeComponent; la logica vive en VistaModeloPrincipal,
/// del que hereda el BindingContext (CLAUDE.md, regla 11).
/// </summary>
public partial class PanelNovedad : ContentView
{
    public PanelNovedad() => InitializeComponent();
}
