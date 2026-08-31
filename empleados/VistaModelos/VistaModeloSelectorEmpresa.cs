using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Datos.Entidades;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>
/// Selector de empresa. Sin empresa activa no se entra al sistema.
///
/// Es tambien la pantalla de alta: RH Manager se instala vacio (CR-01), asi que
/// lo primero que ve el administrador la primera vez es la invitacion a crear su
/// primera empresa, no un error.
/// </summary>
public sealed partial class VistaModeloSelectorEmpresa : VistaModeloBase
{
    private readonly IServicioEmpresas _empresas;
    private readonly IServicioNavegacion _navegacion;
    private readonly SesionUsuario _sesion;

    /// <summary>Id de la empresa en edicion; cero mientras es un alta.</summary>
    private int _idEnEdicion;

    public VistaModeloSelectorEmpresa(
        IServicioEmpresas empresas,
        IServicioNavegacion navegacion,
        SesionUsuario sesion,
        ILogger<VistaModeloSelectorEmpresa> registro,
        IServicioDialogo dialogo)
        : base(registro, dialogo)
    {
        _empresas = empresas;
        _navegacion = navegacion;
        _sesion = sesion;

        Titulo = "Seleccione la empresa";
        Bienvenida = string.Empty;

        // El constructor no consulta la base: solo deja los campos enlazados en
        // un valor valido (CLAUDE.md, regla 13).
        TituloFormulario = string.Empty;
        ErrorFormulario = string.Empty;
        NombreEmpresa = string.Empty;
        NombreCortoEmpresa = string.Empty;
        RtnEmpresa = string.Empty;
        DireccionEmpresa = string.Empty;
        TelefonoEmpresa = string.Empty;
        CorreoEmpresa = string.Empty;
        ColorEmpresa = ColoresSugeridos[0];
    }

    /// <summary>Tarjetas del selector. Se toca siempre desde el hilo de interfaz.</summary>
    public ObservableCollection<TarjetaEmpresa> Empresas { get; } = new();

    [ObservableProperty]
    public partial string Bienvenida { get; set; }

    /// <summary>Verdadero cuando no hay ninguna empresa que mostrar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayEmpresas))]
    public partial bool SinEmpresas { get; set; }

    public bool HayEmpresas => !SinEmpresas;

    /// <summary>Solo el SuperAdministrador administra empresas.</summary>
    public bool PuedeAdministrarEmpresas =>
        _sesion.Perfil == PerfilUsuario.SuperAdministrador;

    /// <summary>Complemento del anterior, para enlazar IsVisible sin convertidor.</summary>
    public bool NoPuedeAdministrarEmpresas => !PuedeAdministrarEmpresas;

    // ─── Formulario de alta y edicion ───────────────────────────────────────

    /// <summary>Verdadero mientras el formulario de empresa esta abierto.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListaVisible))]
    public partial bool ModoFormulario { get; set; }

    /// <summary>La lista y el formulario se turnan: nunca se ven los dos.</summary>
    public bool ListaVisible => !ModoFormulario;

    [ObservableProperty]
    public partial string TituloFormulario { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayErrorFormulario))]
    public partial string ErrorFormulario { get; set; }

    public bool HayErrorFormulario => !string.IsNullOrEmpty(ErrorFormulario);

    [ObservableProperty] public partial string NombreEmpresa { get; set; }
    [ObservableProperty] public partial string NombreCortoEmpresa { get; set; }
    [ObservableProperty] public partial string RtnEmpresa { get; set; }
    [ObservableProperty] public partial string DireccionEmpresa { get; set; }
    [ObservableProperty] public partial string TelefonoEmpresa { get; set; }
    [ObservableProperty] public partial string CorreoEmpresa { get; set; }
    [ObservableProperty] public partial string ColorEmpresa { get; set; }

    /// <summary>
    /// Paleta de identidad para la barra superior. Es una lista corta y fija a
    /// proposito: el color solo tiene que distinguir empresas de un vistazo, y un
    /// selector libre de color en MAUI es un control que no vale la pena aqui.
    /// </summary>
    public IReadOnlyList<string> ColoresSugeridos { get; } =
        ["#0F3D6E", "#C0362C", "#1F6F8B", "#2E6B3E", "#6B4E9B", "#B5761F"];

    protected override async Task CargarDatosAsync()
    {
        var disponibles = await _empresas.ObtenerDisponiblesAsync().ConfigureAwait(true);

        // Modificar una ObservableCollection enlazada desde un hilo secundario
        // cierra la aplicacion en Windows (CLAUDE.md, regla 5).
        EnHiloUi(() =>
        {
            Empresas.Clear();
            foreach (var empresa in disponibles)
            {
                Empresas.Add(empresa);
            }

            SinEmpresas = Empresas.Count == 0;
            Bienvenida = _sesion.EstaAutenticado
                ? _sesion.NombreCompleto + " — " + DescribirPerfil()
                : string.Empty;
        });
    }

    private string DescribirPerfil() => _sesion.Perfil switch
    {
        PerfilUsuario.SuperAdministrador => "Super Administrador",
        PerfilUsuario.Administrador => "Administrador",
        PerfilUsuario.SupervisorSucursal => "Supervisor de sucursal",
        PerfilUsuario.Consulta => "Consulta",
        _ => string.Empty
    };

    // ─── Comandos ───────────────────────────────────────────────────────────

    [RelayCommand]
    private Task SeleccionarAsync(TarjetaEmpresa? empresa)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (empresa is null)
                {
                    return;
                }

                await _empresas.ActivarAsync(empresa.Id).ConfigureAwait(true);
                await _navegacion.IrAsync(RutasNavegacion.Inicio).ConfigureAwait(true);
            },
            "activación de la empresa " + (empresa?.NombreCorto ?? "desconocida"),
            "No se pudo abrir esa empresa. El detalle quedó en el archivo de registro.");

    /// <summary>Abre el formulario en blanco para registrar una empresa nueva.</summary>
    [RelayCommand]
    private Task NuevaEmpresaAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (!await ExigirPermisoAsync().ConfigureAwait(true))
                {
                    return;
                }

                EnHiloUi(() =>
                {
                    _idEnEdicion = 0;
                    LimpiarFormulario();
                    TituloFormulario = "Nueva empresa";
                    ErrorFormulario = string.Empty;
                    ModoFormulario = true;
                });
            },
            "apertura del alta de empresa",
            "No se pudo abrir el formulario de empresa. El detalle quedó en el archivo de registro.");

    /// <summary>Abre el formulario con los datos de una empresa existente.</summary>
    [RelayCommand]
    private Task EditarEmpresaAsync(TarjetaEmpresa? empresa)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (empresa is null)
                {
                    return;
                }

                if (!await ExigirPermisoAsync().ConfigureAwait(true))
                {
                    return;
                }

                var datos = await _empresas.ObtenerParaEdicionAsync(empresa.Id).ConfigureAwait(true);
                if (datos is null)
                {
                    await Dialogo.AvisarAsync(
                        "Empresa no disponible",
                        "Esa empresa ya no está disponible.").ConfigureAwait(true);
                    return;
                }

                EnHiloUi(() =>
                {
                    _idEnEdicion = datos.Id;
                    NombreEmpresa = datos.Nombre;
                    NombreCortoEmpresa = datos.NombreCorto;
                    RtnEmpresa = datos.Rtn;
                    DireccionEmpresa = datos.Direccion;
                    TelefonoEmpresa = datos.Telefono;
                    CorreoEmpresa = datos.Correo;
                    ColorEmpresa = datos.ColorPrimario;
                    TituloFormulario = "Editar empresa";
                    ErrorFormulario = string.Empty;
                    ModoFormulario = true;
                });
            },
            "apertura de la edición de la empresa " + (empresa?.NombreCorto ?? "desconocida"),
            "No se pudo abrir el formulario de empresa. El detalle quedó en el archivo de registro.");

    /// <summary>Guarda el formulario y vuelve a la lista.</summary>
    [RelayCommand]
    private Task GuardarEmpresaAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                var datos = new DatosEmpresa
                {
                    Id = _idEnEdicion,
                    Nombre = NombreEmpresa,
                    NombreCorto = NombreCortoEmpresa,
                    Rtn = RtnEmpresa,
                    Direccion = DireccionEmpresa,
                    Telefono = TelefonoEmpresa,
                    Correo = CorreoEmpresa,
                    ColorPrimario = ColorEmpresa
                };

                var eraAlta = datos.EsAlta;
                var resultado = await _empresas.GuardarAsync(datos).ConfigureAwait(true);

                if (!resultado.Exito)
                {
                    EnHiloUi(() => ErrorFormulario = resultado.Error ?? "No se pudo guardar la empresa.");
                    return;
                }

                EnHiloUi(() =>
                {
                    ErrorFormulario = string.Empty;
                    ModoFormulario = false;
                });

                await CargarDatosAsync().ConfigureAwait(true);

                await Dialogo.AvisarAsync(
                    eraAlta ? "Empresa registrada" : "Cambios guardados",
                    eraAlta
                        ? "La empresa quedó registrada. Ya puede entrar a ella y cargar sus catálogos."
                        : "Los datos de la empresa se guardaron correctamente.").ConfigureAwait(true);
            },
            "guardado de la empresa",
            "No se pudo guardar la empresa. El detalle quedó en el archivo de registro.");

    /// <summary>Cierra el formulario sin guardar.</summary>
    [RelayCommand]
    private void CancelarFormularioEmpresa()
    {
        ModoFormulario = false;
        ErrorFormulario = string.Empty;
    }

    /// <summary>Elige el color de identidad de la empresa.</summary>
    [RelayCommand]
    private void ElegirColor(string? color)
    {
        if (!string.IsNullOrWhiteSpace(color))
        {
            ColorEmpresa = color;
        }
    }

    /// <summary>
    /// Elimina una empresa definitivamente. Antes le dice al usuario exactamente
    /// que se lleva por delante y le pide que escriba el nombre corto: es un
    /// borrado sin vuelta atras y pulsar "Aceptar" por inercia es demasiado facil.
    /// </summary>
    [RelayCommand]
    private Task EliminarAsync(TarjetaEmpresa? empresa)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (empresa is null)
                {
                    return;
                }

                if (!await ExigirPermisoAsync().ConfigureAwait(true))
                {
                    return;
                }

                var resumen = await _empresas.ResumirBorradoAsync(empresa.Id).ConfigureAwait(true);
                if (resumen is null)
                {
                    await Dialogo.AvisarAsync(
                        "Empresa no disponible",
                        "Esa empresa ya no existe.").ConfigureAwait(true);
                    return;
                }

                var escrito = await Dialogo.PedirTextoAsync(
                    "Eliminar \"" + empresa.NombreCorto + "\"",
                    DescribirBorrado(resumen, empresa.NombreCorto),
                    empresa.NombreCorto,
                    "Eliminar definitivamente",
                    "Cancelar").ConfigureAwait(true);

                if (escrito is null)
                {
                    return;
                }

                if (!string.Equals(escrito.Trim(), empresa.NombreCorto, StringComparison.OrdinalIgnoreCase))
                {
                    await Dialogo.AvisarAsync(
                        "No se eliminó nada",
                        "El texto que escribió no coincide con \"" + empresa.NombreCorto
                            + "\". La empresa quedó intacta.").ConfigureAwait(true);
                    return;
                }

                var resultado = await _empresas.EliminarAsync(empresa.Id).ConfigureAwait(true);

                if (!resultado.Exito)
                {
                    await Dialogo.AvisarAsync(
                        "No se pudo eliminar",
                        resultado.Error ?? "No se pudo eliminar la empresa.").ConfigureAwait(true);
                    return;
                }

                await CargarDatosAsync().ConfigureAwait(true);

                await Dialogo.AvisarAsync(
                    "Empresa eliminada",
                    "\"" + empresa.Nombre + "\" y todos sus datos se eliminaron del sistema.")
                    .ConfigureAwait(true);
            },
            "eliminación de la empresa " + (empresa?.NombreCorto ?? "desconocida"),
            "No se pudo eliminar la empresa. El detalle quedó en el archivo de registro.");

    /// <summary>Texto de la advertencia previa al borrado, con la cuenta real.</summary>
    private static string DescribirBorrado(ResumenBorradoEmpresa resumen, string nombreCorto)
    {
        var arrastre = resumen.EstaVacia
            ? "Esta empresa no tiene datos cargados todavía."
            : "Se eliminaran también, sin posibilidad de recuperarlos:"
                + Environment.NewLine
                + "  • " + resumen.Sucursales + " sucursal(es)" + Environment.NewLine
                + "  • " + resumen.Colaboradores + " colaborador(es) con su historial" + Environment.NewLine
                + "  • " + resumen.Contratos + " contrato(s)" + Environment.NewLine
                + "  • " + resumen.Documentos + " documento(s) adjuntos, incluidos sus archivos";

        return arrastre
            + Environment.NewLine + Environment.NewLine
            + "Para confirmar, escriba el nombre corto de la empresa: " + nombreCorto;
    }

    private void LimpiarFormulario()
    {
        NombreEmpresa = string.Empty;
        NombreCortoEmpresa = string.Empty;
        RtnEmpresa = string.Empty;
        DireccionEmpresa = string.Empty;
        TelefonoEmpresa = string.Empty;
        CorreoEmpresa = string.Empty;
        ColorEmpresa = ColoresSugeridos[0];
    }

    /// <summary>Avisa y devuelve falso si el perfil no administra empresas.</summary>
    private async Task<bool> ExigirPermisoAsync()
    {
        if (PuedeAdministrarEmpresas)
        {
            return true;
        }

        Registro.LogWarning("El perfil {Perfil} intentó administrar empresas sin permiso.", _sesion.Perfil);
        await Dialogo.AvisarAsync(
            "Sin permiso",
            "Solo el Super Administrador puede crear, editar o eliminar empresas.").ConfigureAwait(true);
        return false;
    }
}
