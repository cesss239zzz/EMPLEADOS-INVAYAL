using empleados.VistaModelos;

namespace empleados.Vistas;

/// <inheritdoc cref="PaginaArranque" />
public partial class PaginaDiagnostico : ContentPage
{
    private readonly VistaModeloDiagnostico _vistaModelo;

    public PaginaDiagnostico(VistaModeloDiagnostico vistaModelo)
    {
        InitializeComponent();

        _vistaModelo = vistaModelo;
        BindingContext = vistaModelo;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // La pantalla pide sus datos al abrirse. Nada se precarga antes.
        _vistaModelo.AparecerCommand.Execute(null);
    }
}
