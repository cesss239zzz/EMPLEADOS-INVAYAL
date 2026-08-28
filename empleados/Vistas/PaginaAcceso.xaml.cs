using empleados.VistaModelos;

namespace empleados.Vistas;

/// <inheritdoc cref="PaginaArranque" />
public partial class PaginaAcceso : ContentPage
{
    private readonly VistaModeloAcceso _vistaModelo;

    public PaginaAcceso(VistaModeloAcceso vistaModelo)
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
