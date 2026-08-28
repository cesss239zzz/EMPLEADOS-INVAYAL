namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioDialogo : IServicioDialogo
{
    /// <inheritdoc />
    public Task AvisarAsync(string titulo, string mensaje, string boton = "Entendido")
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var pagina = ObtenerPaginaActual();
            if (pagina is null)
            {
                // Sin ventana todavia: al menos que quede constancia en disco.
                RegistroEmergencia.Escribir("Aviso sin ventana disponible: " + titulo + " / " + mensaje);
                return;
            }

            await pagina.DisplayAlertAsync(titulo, mensaje, boton);
        });

    /// <inheritdoc />
    public Task<bool> ConfirmarAsync(string titulo, string mensaje, string aceptar = "Si", string cancelar = "No")
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var pagina = ObtenerPaginaActual();
            if (pagina is null)
            {
                return false;
            }

            return await pagina.DisplayAlertAsync(titulo, mensaje, aceptar, cancelar);
        });

    /// <summary>Pagina sobre la que se puede desplegar un dialogo, o nula si aun no hay ventana.</summary>
    private static Page? ObtenerPaginaActual()
    {
        if (Shell.Current is not null)
        {
            return Shell.Current;
        }

        return Application.Current?.Windows.FirstOrDefault()?.Page;
    }
}
