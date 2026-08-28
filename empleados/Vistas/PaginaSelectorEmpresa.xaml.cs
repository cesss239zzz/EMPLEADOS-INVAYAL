using empleados.VistaModelos;

namespace empleados.Vistas;

/// <inheritdoc cref="PaginaArranque" />
public partial class PaginaSelectorEmpresa : ContentPage
{
    private readonly VistaModeloSelectorEmpresa _vistaModelo;

    public PaginaSelectorEmpresa(VistaModeloSelectorEmpresa vistaModelo)
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
