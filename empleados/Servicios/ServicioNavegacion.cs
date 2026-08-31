using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc cref="IServicioNavegacion" />
public sealed class ServicioNavegacion : IServicioNavegacion
{
    private readonly ILogger<ServicioNavegacion> _registro;

    public ServicioNavegacion(ILogger<ServicioNavegacion> registro)
    {
        _registro = registro;
    }

    /// <inheritdoc />
    public Task IrAsync(string ruta)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = Shell.Current;
            if (shell is null)
            {
                _registro.LogWarning("Se pidió navegar a {Ruta} pero todavía no hay Shell.", ruta);
                return;
            }

            _registro.LogInformation("Navegando a {Ruta}", ruta);

            try
            {
                await shell.GoToAsync(ruta);
            }
            catch (InvalidOperationException ex)
            {
                // Shell rechaza una navegacion mientras tiene otra en curso. Un solo
                // reintento corto resuelve la carrera del arranque; si vuelve a fallar
                // la excepcion sube al comando, que la registra y avisa al usuario.
                _registro.LogWarning(ex, "Shell ocupado al navegar a {Ruta}. Se reintenta una vez.", ruta);

                await Task.Delay(250);
                await shell.GoToAsync(ruta);
            }
        });
}
