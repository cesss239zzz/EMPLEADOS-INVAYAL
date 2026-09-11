using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Datos.Entidades;
using empleados.Servicios;

namespace empleados.VistaModelos;

/// <summary>
/// Parte de novedades del contenedor principal: registrar una incidencia y
/// programar vacaciones para el expediente abierto. Comparte el panel-que-tapa
/// con la edicion; ambos se abren desde la ficha.
/// </summary>
public sealed partial class VistaModeloPrincipal
{
    private readonly IServicioNovedades _novedades;

    // ─── Estado del formulario de novedad ───────────────────────────────────

    /// <summary>Verdadero mientras el formulario de novedad esta abierto.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelNovedad))]
    [NotifyPropertyChangedFor(nameof(PanelEdicion))]
    [NotifyPropertyChangedFor(nameof(PanelFicha))]
    [NotifyPropertyChangedFor(nameof(PanelResumen))]
    [NotifyPropertyChangedFor(nameof(PanelColaboradores))]
    [NotifyPropertyChangedFor(nameof(PanelAlertas))]
    [NotifyPropertyChangedFor(nameof(PanelPendiente))]
    public partial bool ModoNovedad { get; set; }

    /// <summary>Verdadero si el formulario captura una incidencia; falso si vacaciones.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsVacaciones))]
    public partial bool EsIncidencia { get; set; }

    public bool EsVacaciones => !EsIncidencia;

    [ObservableProperty] public partial string TituloNovedad { get; set; }
    [ObservableProperty] public partial string SubtituloNovedad { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayErrorNovedad))]
    public partial string ErrorNovedad { get; set; }

    public bool HayErrorNovedad => !string.IsNullOrEmpty(ErrorNovedad);

    // Campos de incidencia
    [ObservableProperty] public partial OpcionEnum? TipoIncidenciaSeleccionado { get; set; }
    [ObservableProperty] public partial DateTime FechaIncidencia { get; set; }
    [ObservableProperty] public partial string TituloIncidencia { get; set; }
    [ObservableProperty] public partial string DescripcionIncidencia { get; set; }

    // Campos de vacaciones
    [ObservableProperty] public partial DateTime FechaInicioVacaciones { get; set; }
    [ObservableProperty] public partial DateTime FechaFinVacaciones { get; set; }
    [ObservableProperty] public partial string ObservacionVacaciones { get; set; }

    /// <summary>Dias calendario del periodo elegido, recalculado al mover las fechas.</summary>
    [ObservableProperty] public partial string DiasVacacionesTexto { get; set; }

    public IReadOnlyList<OpcionEnum> TiposIncidencia { get; } =
    [
        new OpcionEnum((int)TipoIncidencia.Amonestacion, "Amonestacion"),
        new OpcionEnum((int)TipoIncidencia.Memorando, "Memorando"),
        new OpcionEnum((int)TipoIncidencia.Felicitacion, "Felicitacion"),
        new OpcionEnum((int)TipoIncidencia.Permiso, "Permiso"),
        new OpcionEnum((int)TipoIncidencia.Ausencia, "Ausencia"),
        new OpcionEnum((int)TipoIncidencia.Tardanza, "Tardanza")
    ];

    /// <summary>Valores fijos de partida. Se llama desde el constructor.</summary>
    private void InicializarCamposNovedad()
    {
        TituloNovedad = string.Empty;
        SubtituloNovedad = string.Empty;
        ErrorNovedad = string.Empty;

        TipoIncidenciaSeleccionado = TiposIncidencia[1];
        FechaIncidencia = DateTime.Today;
        TituloIncidencia = string.Empty;
        DescripcionIncidencia = string.Empty;

        FechaInicioVacaciones = DateTime.Today;
        FechaFinVacaciones = DateTime.Today;
        ObservacionVacaciones = string.Empty;
        DiasVacacionesTexto = "1 día";
    }

    partial void OnFechaInicioVacacionesChanged(DateTime value) => RecalcularDiasVacaciones();

    partial void OnFechaFinVacacionesChanged(DateTime value) => RecalcularDiasVacaciones();

    private void RecalcularDiasVacaciones()
    {
        var dias = (FechaFinVacaciones.Date - FechaInicioVacaciones.Date).Days + 1;
        DiasVacacionesTexto = dias < 1 ? "———" : dias + (dias == 1 ? " día" : " días");
    }

    // ─── Comandos ───────────────────────────────────────────────────────────

    /// <summary>Abre el formulario para registrar una incidencia del expediente abierto.</summary>
    [RelayCommand]
    private Task AbrirIncidenciaAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (Ficha is null)
                {
                    return;
                }

                if (!await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                EnHiloUi(() =>
                {
                    EsIncidencia = true;
                    TipoIncidenciaSeleccionado = TiposIncidencia[1];
                    FechaIncidencia = DateTime.Today;
                    TituloIncidencia = string.Empty;
                    DescripcionIncidencia = string.Empty;
                    ErrorNovedad = string.Empty;
                    TituloNovedad = "Registrar incidencia";
                    SubtituloNovedad = "Nueva incidencia para " + Ficha.NombreCompleto + ".";
                    ModoNovedad = true;
                });
            },
            "apertura del registro de incidencia",
            "No se pudo abrir el formulario de incidencia. El detalle quedó en el archivo de registro.");

    /// <summary>Abre el formulario para programar vacaciones del expediente abierto.</summary>
    [RelayCommand]
    private Task AbrirVacacionesAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (Ficha is null)
                {
                    return;
                }

                if (!await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                EnHiloUi(() =>
                {
                    EsIncidencia = false;
                    FechaInicioVacaciones = DateTime.Today;
                    FechaFinVacaciones = DateTime.Today;
                    ObservacionVacaciones = string.Empty;
                    RecalcularDiasVacaciones();
                    ErrorNovedad = string.Empty;
                    TituloNovedad = "Programar vacaciones";
                    SubtituloNovedad = "Nuevo periodo de vacaciones para " + Ficha.NombreCompleto + ".";
                    ModoNovedad = true;
                });
            },
            "apertura de la programación de vacaciones",
            "No se pudo abrir el formulario de vacaciones. El detalle quedó en el archivo de registro.");

    /// <summary>Valida y guarda la novedad; recarga la ficha en la pestana de novedades.</summary>
    [RelayCommand]
    private Task GuardarNovedadAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (Ficha is null)
                {
                    return;
                }

                var colaboradorId = Ficha.Id;
                ResultadoGuardado resultado;

                if (EsIncidencia)
                {
                    if (string.IsNullOrWhiteSpace(TituloIncidencia))
                    {
                        EnHiloUi(() => ErrorNovedad = "El título de la incidencia es obligatorio.");
                        return;
                    }

                    resultado = await _novedades.RegistrarIncidenciaAsync(new DatosIncidencia
                    {
                        ColaboradorId = colaboradorId,
                        Tipo = TipoIncidenciaSeleccionado is { } t ? (TipoIncidencia)t.Valor : TipoIncidencia.Memorando,
                        Fecha = FechaIncidencia,
                        Titulo = TituloIncidencia,
                        Descripcion = DescripcionIncidencia
                    }).ConfigureAwait(true);
                }
                else
                {
                    if (FechaFinVacaciones.Date < FechaInicioVacaciones.Date)
                    {
                        EnHiloUi(() => ErrorNovedad = "La fecha de fin no puede ser anterior a la de inicio.");
                        return;
                    }

                    resultado = await _novedades.ProgramarVacacionesAsync(new DatosVacaciones
                    {
                        ColaboradorId = colaboradorId,
                        FechaInicio = FechaInicioVacaciones,
                        FechaFin = FechaFinVacaciones,
                        Observacion = ObservacionVacaciones
                    }).ConfigureAwait(true);
                }

                if (!resultado.Exito)
                {
                    EnHiloUi(() => ErrorNovedad = resultado.Error ?? "No se pudo guardar la novedad.");
                    return;
                }

                var eraIncidencia = EsIncidencia;

                EnHiloUi(() =>
                {
                    ErrorNovedad = string.Empty;
                    ModoNovedad = false;
                });

                // Se recarga la ficha para que la novedad recien guardada aparezca.
                await AbrirFichaPorIdAsync(colaboradorId).ConfigureAwait(true);
                EnHiloUi(() => PestanaActiva = Pestana.Novedades);

                await Dialogo.AvisarAsync(
                    eraIncidencia ? "Incidencia registrada" : "Vacaciones programadas",
                    eraIncidencia
                        ? "La incidencia quedó guardada en el expediente."
                        : "El periodo de vacaciones quedó guardado en el expediente.").ConfigureAwait(true);
            },
            "guardado de la novedad",
            "No se pudo guardar la novedad. El detalle quedó en el archivo de registro.");

    /// <summary>Cierra el formulario de novedad sin guardar.</summary>
    [RelayCommand]
    private void CancelarNovedad()
    {
        if (EstaOcupado) return;
        ModoNovedad = false;
        ErrorNovedad = string.Empty;
    }
}
