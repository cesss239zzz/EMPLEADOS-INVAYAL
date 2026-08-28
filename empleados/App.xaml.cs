using Microsoft.Extensions.DependencyInjection;

namespace empleados;

public partial class App : Application
{
    private readonly IServiceProvider _proveedor;

    public App(IServiceProvider proveedor)
    {
        InitializeComponent();
        _proveedor = proveedor;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var ventana = new Window(_proveedor.GetRequiredService<AppShell>())
        {
            Title = "RH Manager — Sistema de Expediente Digital del Personal",
            Width = 1360,
            Height = 860,
            MinimumWidth = 1024,
            MinimumHeight = 700
        };

        return ventana;
    }
}
