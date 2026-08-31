using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>Pantalla de acceso. Verifica credenciales con BCrypt.</summary>
public sealed partial class VistaModeloAcceso : VistaModeloBase
{
    private readonly IServicioAutenticacion _autenticacion;
    private readonly IServicioNavegacion _navegacion;

    public VistaModeloAcceso(
        IServicioAutenticacion autenticacion,
        IServicioNavegacion navegacion,
        ILogger<VistaModeloAcceso> registro,
        IServicioDialogo dialogo)
        : base(registro, dialogo)
    {
        _autenticacion = autenticacion;
        _navegacion = navegacion;

        Titulo = "Acceso";
        NombreUsuario = string.Empty;
        Contrasena = string.Empty;
        Mensaje = string.Empty;
        ContrasenaOculta = true;
    }

    [ObservableProperty]
    public partial string NombreUsuario { get; set; }

    /// <summary>
    /// Solo vive en memoria mientras dura el intento. No se registra, no se
    /// guarda y se borra apenas se usa (CLAUDE.md, regla 12).
    /// </summary>
    [ObservableProperty]
    public partial string Contrasena { get; set; }

    /// <summary>
    /// Enmascara la contrasena. Se puede destapar para comprobar lo que se
    /// escribio: en un teclado ajeno, o con una contrasena larga, escribir a
    /// ciegas es la causa mas comun de un acceso fallido.
    ///
    /// Destaparla solo la muestra en pantalla. No cambia nada de lo que se
    /// guarda ni de lo que se registra: la contrasena sigue sin tocar el archivo
    /// de registro (CLAUDE.md, regla 12).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoVerContrasena))]
    public partial bool ContrasenaOculta { get; set; }

    /// <summary>Texto del boton que destapa o vuelve a tapar la contrasena.</summary>
    public string TextoVerContrasena => ContrasenaOculta ? "Ver" : "Ocultar";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje))]
    public partial string Mensaje { get; set; }

    public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

    /// <summary>Al abrir la pantalla se limpia lo que hubiera quedado escrito.</summary>
    protected override Task CargarDatosAsync()
    {
        Contrasena = string.Empty;
        Mensaje = string.Empty;

        // Vuelve a taparse en cada aparicion: dejarla destapada de una sesion
        // para la siguiente seria una sorpresa desagradable delante de otro.
        ContrasenaOculta = true;
        return Task.CompletedTask;
    }

    /// <summary>Destapa o vuelve a tapar la contrasena escrita.</summary>
    [RelayCommand]
    private void AlternarVerContrasena() => ContrasenaOculta = !ContrasenaOculta;

    [RelayCommand]
    private Task AccederAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                EnHiloUi(() => Mensaje = string.Empty);

                var intento = await _autenticacion
                    .AutenticarAsync(NombreUsuario, Contrasena)
                    .ConfigureAwait(true);

                // La contrasena se descarta apenas se verifico, salga bien o mal.
                EnHiloUi(() => Contrasena = string.Empty);

                if (!intento.EsCorrecto)
                {
                    EnHiloUi(() => Mensaje = intento.Mensaje);
                    return;
                }

                await _navegacion.IrAsync(RutasNavegacion.Empresas).ConfigureAwait(true);
            },
            "intento de acceso",
            "No se pudo verificar el acceso. El detalle quedó en el archivo de registro.");
}
