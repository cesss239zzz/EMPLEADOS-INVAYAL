using empleados.VistaModelos;

namespace empleados.Vistas;

/// <summary>
/// Codigo subyacente sin logica: constructor, BindingContext y el disparo de la
/// verificacion inicial (CLAUDE.md, regla 11).
/// </summary>
public partial class PaginaArranque : ContentPage
{
    private readonly VistaModeloArranque _vistaModelo;
    private bool _yaSeVerifico;

    public PaginaArranque(VistaModeloArranque vistaModelo)
    {
        InitializeComponent();

        _vistaModelo = vistaModelo;
        BindingContext = vistaModelo;

        Loaded += AlCargarLaPagina;
    }

    /// <summary>
    /// La verificacion NO se dispara desde OnAppearing. OnAppearing de la primera
    /// pagina corre dentro de la misma cadena sincronica que monta la vista nativa
    /// del Shell, y navegar ahi lanza "Pending Navigations still processing": la
    /// ventana nunca llega a crearse. Loaded corre con el arbol visual ya adjunto,
    /// y el retraso corto deja que Shell termine su navegacion inicial.
    /// </summary>
    private void AlCargarLaPagina(object? sender, EventArgs argumentos)
    {
        Loaded -= AlCargarLaPagina;

        if (_yaSeVerifico)
        {
            return;
        }

        _yaSeVerifico = true;

        Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(400),
            () => _vistaModelo.VerificarCommand.Execute(null));
    }
}
