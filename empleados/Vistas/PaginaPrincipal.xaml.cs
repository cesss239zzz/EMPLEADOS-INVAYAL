using empleados.VistaModelos;

namespace empleados.Vistas;

/// <inheritdoc cref="PaginaArranque" />
public partial class PaginaPrincipal : ContentPage
{
    private readonly VistaModeloPrincipal _vistaModelo;

    public PaginaPrincipal(VistaModeloPrincipal vistaModelo)
    {
        InitializeComponent();

        _vistaModelo = vistaModelo;
        BindingContext = vistaModelo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vistaModelo.AparecerCommand.Execute(null);
    }
}
