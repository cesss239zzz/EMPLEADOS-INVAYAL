using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>Selector de empresa. Sin empresa activa no se entra al sistema.</summary>
public sealed partial class VistaModeloSelectorEmpresa : VistaModeloBase
{
    private readonly IServicioEmpresas _empresas;
    private readonly IServicioNavegacion _navegacion;
    private readonly SesionUsuario _sesion;

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
    }

    /// <summary>Tarjetas del selector. Se toca siempre desde el hilo de interfaz.</summary>
    public ObservableCollection<TarjetaEmpresa> Empresas { get; } = new();

    [ObservableProperty]
    public partial string Bienvenida { get; set; }

    [ObservableProperty]
    public partial bool SinEmpresas { get; set; }

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
        Datos.Entidades.PerfilUsuario.SuperAdministrador => "Super Administrador",
        Datos.Entidades.PerfilUsuario.Administrador => "Administrador",
        Datos.Entidades.PerfilUsuario.SupervisorSucursal => "Supervisor de sucursal",
        Datos.Entidades.PerfilUsuario.Consulta => "Consulta",
        _ => string.Empty
    };

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
            "activacion de la empresa " + (empresa?.NombreCorto ?? "desconocida"),
            "No se pudo abrir esa empresa. El detalle quedo en el archivo de registro.");
}
