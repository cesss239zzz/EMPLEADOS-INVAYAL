using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>
/// Base de todos los ViewModel. Concentra el patron obligatorio de la regla 4 de
/// CLAUDE.md: try / catch / finally completo, marca de ocupado, registro de la
/// excepcion y mensaje comprensible en espanol para el usuario. Ningun comando
/// de la aplicacion deberia escribir ese bloque a mano.
/// </summary>
public abstract partial class VistaModeloBase : ObservableObject
{
    protected VistaModeloBase(ILogger registro, IServicioDialogo dialogo)
    {
        Registro = registro;
        Dialogo = dialogo;
        Titulo = string.Empty;
    }

    protected ILogger Registro { get; }

    protected IServicioDialogo Dialogo { get; }

    /// <summary>Verdadero mientras hay una operacion en curso. Bloquea la doble pulsacion.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoEstaOcupado))]
    public partial bool EstaOcupado { get; set; }

    /// <summary>Complemento de <see cref="EstaOcupado"/>, para enlazar IsEnabled en XAML.</summary>
    public bool NoEstaOcupado => !EstaOcupado;

    /// <summary>Titulo de la pantalla.</summary>
    [ObservableProperty]
    public partial string Titulo { get; set; }

    /// <summary>
    /// Carga los datos de la pantalla. Se sobreescribe en cada ViewModel que necesite
    /// leer de la base. Por omision no hace nada.
    ///
    /// Esta es la UNICA puerta por la que un ViewModel consulta datos. Ningun
    /// constructor toca la base de datos y la aplicacion no precarga nada al arrancar:
    /// cada pantalla trae lo suyo cuando se abre (CLAUDE.md, regla 13).
    /// </summary>
    protected virtual Task CargarDatosAsync() => Task.CompletedTask;

    /// <summary>
    /// Lo invoca la pagina desde OnAppearing. Recarga en cada aparicion, para que al
    /// volver de otra pantalla no se vean datos viejos. La reentrada la corta
    /// <see cref="EstaOcupado"/>.
    /// </summary>
    [RelayCommand]
    private Task AparecerAsync()
        => EjecutarSeguroAsync(
            CargarDatosAsync,
            "carga de datos de " + GetType().Name,
            "No se pudieron cargar los datos de esta pantalla. El detalle quedo en el archivo de registro.");

    /// <summary>
    /// Ejecuta una operacion asincrona con la proteccion completa que exige CLAUDE.md.
    /// </summary>
    /// <param name="operacion">Trabajo a realizar.</param>
    /// <param name="descripcion">Que se estaba haciendo. Va al registro, no a la pantalla.</param>
    /// <param name="mensajeUsuario">Mensaje en espanol que se muestra si algo falla.</param>
    protected async Task EjecutarSeguroAsync(
        Func<Task> operacion,
        string descripcion,
        string mensajeUsuario)
    {
        if (EstaOcupado)
        {
            Registro.LogDebug("Se ignoro una segunda pulsacion durante: {Descripcion}", descripcion);
            return;
        }

        EnHiloUi(() => EstaOcupado = true);

        try
        {
            await operacion().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Registro.LogError(ex, "Fallo durante: {Descripcion}", descripcion);

            // Al usuario nunca se le muestra ex.Message crudo (CLAUDE.md, regla 4).
            await Dialogo.AvisarAsync("No se pudo completar la operacion", mensajeUsuario)
                .ConfigureAwait(true);
        }
        finally
        {
            EnHiloUi(() => EstaOcupado = false);
        }
    }

    /// <summary>
    /// Ejecuta la accion en el hilo de interfaz. Modificar una propiedad enlazada o una
    /// ObservableCollection desde un hilo secundario cierra la aplicacion en Windows
    /// de inmediato (CLAUDE.md, regla 5).
    /// </summary>
    protected static void EnHiloUi(Action accion)
    {
        if (MainThread.IsMainThread)
        {
            accion();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(accion);
        }
    }
}
