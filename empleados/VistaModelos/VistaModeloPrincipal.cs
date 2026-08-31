using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Datos;
using empleados.Datos.Entidades;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>
/// Secciones del menu lateral. El orden es el del menu y el de las maquetas.
/// </summary>
public enum Seccion
{
    Resumen,
    Colaboradores,
    Documentos,
    Historial,
    Alertas,
    Reportes,
    Configuracion
}

/// <summary>
/// Pestanas de la ficha del colaborador.
///
/// La maqueta dibuja "Historial Salarial" y "Evaluaciones" como pestanas
/// propias. Ninguna de las dos tiene respaldo en el modelo de datos: los
/// cambios de salario ya viven dentro del historial laboral y de evaluaciones
/// no hay tabla. Inventar campos esta prohibido (CLAUDE.md), asi que en su
/// lugar van Contratos y Contactos, que si existen.
/// </summary>
public enum Pestana
{
    General,
    Historial,
    Contratos,
    Contactos,
    Novedades
}

/// <summary>
/// Contenedor de la aplicacion: menu lateral, barra superior y area de
/// contenido. Alberga las tres pantallas de las maquetas —resumen, directorio
/// de colaboradores y ficha— y el panel de alertas.
/// </summary>
public sealed partial class VistaModeloPrincipal : VistaModeloBase
{
    /// <summary>
    /// Espera entre la ultima tecla y la consulta. Sin esto, escribir "Delmy"
    /// lanzaria cinco consultas y la ultima podria llegar antes que otra.
    /// </summary>
    private static readonly TimeSpan EsperaAntesDeBuscar = TimeSpan.FromMilliseconds(250);

    /// <summary>Cuantas filas trae el directorio rapido del resumen.</summary>
    private const int FilasDelDirectorio = 6;

    private readonly IServicioColaboradores _colaboradores;
    private readonly IServicioFicha _ficha;
    private readonly IServicioResumen _servicioResumen;
    private readonly IServicioAlertas _alertas;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly IServicioNavegacion _navegacion;

    /// <summary>Cancela la busqueda pendiente cuando el usuario sigue escribiendo.</summary>
    private CancellationTokenSource? _cancelacionBusqueda;

    /// <summary>
    /// Verdadero mientras se rellenan los desplegables. Impide que asignar la
    /// opcion "todos" por omision dispare una recarga en cadena.
    /// </summary>
    private bool _rellenandoFiltros;

    /// <summary>
    /// Empresa para la que ya corrio el motor de alertas en esta sesion. El
    /// motor es idempotente, pero escribe: correrlo en cada aparicion de la
    /// pantalla seria trabajo de base de datos regalado.
    /// </summary>
    private int _empresaConMotorCorrido;

    /// <summary>Cuantos colaboradores tiene la empresa sin filtrar.</summary>
    private int _totalSinFiltrar;

    public VistaModeloPrincipal(
        IServicioColaboradores colaboradores,
        IServicioFicha ficha,
        IServicioResumen resumen,
        IServicioAlertas alertas,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        IServicioNavegacion navegacion,
        IServicioReportes reportes,
        IServicioNovedades novedades,
        IServicioDocumentos documentos,
        IServicioCatalogos catalogos,
        IServicioRespaldos respaldos,
        IServicioArchivos archivos,
        ILogger<VistaModeloPrincipal> registro,
        IServicioDialogo dialogo)
        : base(registro, dialogo)
    {
        _colaboradores = colaboradores;
        _ficha = ficha;
        _servicioResumen = resumen;
        _alertas = alertas;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _navegacion = navegacion;
        _reportes = reportes;
        _novedades = novedades;
        _documentos = documentos;
        _catalogos = catalogos;
        _respaldos = respaldos;
        _archivos = archivos;

        // El constructor solo asigna dependencias y valores fijos. Nada de
        // consultas: eso vive en CargarDatosAsync (CLAUDE.md, regla 13).
        Titulo = "RH Manager";
        EmpresaActiva = string.Empty;
        ColorEmpresa = "#0453CD";
        Usuario = string.Empty;
        PerfilTexto = string.Empty;
        InicialesUsuario = string.Empty;
        Busqueda = string.Empty;
        ResumenConteo = string.Empty;
        Resumen = ResumenGeneral.Vacio;

        Estados =
        [
            new OpcionFiltro(0, "Todos los estados"),
            new OpcionFiltro((int)EstadoColaborador.Activo, "Activo"),
            new OpcionFiltro((int)EstadoColaborador.Suspendido, "Suspendido"),
            new OpcionFiltro((int)EstadoColaborador.Inactivo, "Inactivo")
        ];

        // Deja los formularios de captura en un estado valido de partida: ningun
        // campo enlazado en null ni fecha fuera del rango del DatePicker.
        InicializarCamposEdicion();
        InicializarCamposNovedad();
        InicializarCamposDocumento();
        InicializarCamposCatalogo();
        InicializarCamposRespaldo();
    }

    // ─── Limites de los campos de fecha ─────────────────────────────────────
    //
    // Los tres los consume CampoFecha para acotar lo que se puede escribir y el
    // rango del desplegable de año. Estan aca y no incrustados en el XAML porque
    // "hoy" no es una constante y hornearla en la vista la dejaria vencida al dia
    // siguiente de compilar.

    /// <summary>Primera fecha razonable del sistema.</summary>
    public DateTime LimiteFechaMinima { get; } = new(1900, 1, 1);

    /// <summary>Hoy. Tope de lo que ya ocurrio: nacimiento, ingreso, un hecho registrado.</summary>
    public DateTime LimiteFechaHoy => DateTime.Today;

    /// <summary>Tope de lo que esta por ocurrir: vencimientos y vacaciones programadas.</summary>
    public DateTime LimiteFechaFutura => DateTime.Today.AddYears(30);

    // ─── Colecciones ────────────────────────────────────────────────────────

    /// <summary>Filas de la tabla de colaboradores.</summary>
    public ObservableCollection<FilaColaborador> Colaboradores { get; } = [];

    /// <summary>Las primeras filas, para el directorio rapido del resumen.</summary>
    public ObservableCollection<FilaColaborador> Directorio { get; } = [];

    /// <summary>Avisos pendientes, del mas urgente al menos urgente.</summary>
    public ObservableCollection<LineaAviso> Avisos { get; } = [];

    public ObservableCollection<OpcionFiltro> Departamentos { get; } = [];

    public ObservableCollection<OpcionFiltro> Sucursales { get; } = [];

    public IReadOnlyList<OpcionFiltro> Estados { get; }

    // ─── Identidad de la sesion ─────────────────────────────────────────────

    [ObservableProperty]
    public partial string EmpresaActiva { get; set; }

    [ObservableProperty]
    public partial string ColorEmpresa { get; set; }

    [ObservableProperty]
    public partial string Usuario { get; set; }

    [ObservableProperty]
    public partial string PerfilTexto { get; set; }

    /// <summary>Iniciales del usuario, para la pastilla de la barra superior.</summary>
    [ObservableProperty]
    public partial string InicialesUsuario { get; set; }

    // ─── Resumen ────────────────────────────────────────────────────────────

    /// <summary>Las cuatro metricas de la pantalla de resumen.</summary>
    [ObservableProperty]
    public partial ResumenGeneral Resumen { get; set; }

    // ─── Busqueda y filtros ─────────────────────────────────────────────────

    /// <summary>Texto del buscador de la barra superior.</summary>
    [ObservableProperty]
    public partial string Busqueda { get; set; }

    [ObservableProperty]
    public partial OpcionFiltro? DepartamentoSeleccionado { get; set; }

    [ObservableProperty]
    public partial OpcionFiltro? SucursalSeleccionada { get; set; }

    [ObservableProperty]
    public partial OpcionFiltro? EstadoSeleccionado { get; set; }

    /// <summary>Texto de la cabecera del panel: "3 de 9 registros".</summary>
    [ObservableProperty]
    public partial string ResumenConteo { get; set; }

    /// <summary>Verdadero cuando el filtro no devolvio ninguna fila.</summary>
    [ObservableProperty]
    public partial bool SinResultados { get; set; }

    /// <summary>Verdadero si hay algun criterio de busqueda puesto.</summary>
    [ObservableProperty]
    public partial bool HayFiltros { get; set; }

    /// <summary>Verdadero cuando el directorio rapido del resumen quedo vacio.</summary>
    [ObservableProperty]
    public partial bool SinResultadosDirectorio { get; set; }

    /// <summary>Verdadero cuando no hay ningun aviso pendiente.</summary>
    [ObservableProperty]
    public partial bool SinAvisos { get; set; }

    /// <summary>Avisos pendientes. Es el numero del globo de la campana.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAvisosPendientes))]
    public partial int AvisosPendientes { get; set; }

    public bool HayAvisosPendientes => AvisosPendientes > 0;

    // ─── Seccion activa ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsResumen))]
    [NotifyPropertyChangedFor(nameof(EsColaboradores))]
    [NotifyPropertyChangedFor(nameof(EsDocumentos))]
    [NotifyPropertyChangedFor(nameof(EsHistorial))]
    [NotifyPropertyChangedFor(nameof(EsAlertas))]
    [NotifyPropertyChangedFor(nameof(EsReportes))]
    [NotifyPropertyChangedFor(nameof(EsConfiguracion))]
    [NotifyPropertyChangedFor(nameof(PanelResumen))]
    [NotifyPropertyChangedFor(nameof(PanelColaboradores))]
    [NotifyPropertyChangedFor(nameof(PanelAlertas))]
    [NotifyPropertyChangedFor(nameof(PanelCatalogos))]
    [NotifyPropertyChangedFor(nameof(PanelRespaldos))]
    [NotifyPropertyChangedFor(nameof(PanelPendiente))]
    [NotifyPropertyChangedFor(nameof(TituloSeccion))]
    [NotifyPropertyChangedFor(nameof(SubtituloSeccion))]
    public partial Seccion SeccionActiva { get; set; }

    // Resalte del menu lateral. Sigue a la seccion aunque haya una ficha
    // abierta: la ficha es parte de Colaboradores, no una seccion aparte.
    public bool EsResumen => SeccionActiva == Seccion.Resumen;
    public bool EsColaboradores => SeccionActiva == Seccion.Colaboradores;
    public bool EsDocumentos => SeccionActiva == Seccion.Documentos;
    public bool EsHistorial => SeccionActiva == Seccion.Historial;
    public bool EsAlertas => SeccionActiva == Seccion.Alertas;
    public bool EsReportes => SeccionActiva == Seccion.Reportes;
    public bool EsConfiguracion => SeccionActiva == Seccion.Configuracion;

    /// <summary>Titulo grande del area de contenido.</summary>
    public string TituloSeccion => SeccionActiva switch
    {
        Seccion.Resumen => "Resumen General",
        Seccion.Colaboradores => "Colaboradores",
        Seccion.Documentos => "Documentos",
        Seccion.Historial => "Historial",
        Seccion.Alertas => "Alertas",
        Seccion.Reportes => "Reportes",
        _ => "Configuración"
    };

    /// <summary>Linea de apoyo bajo el titulo.</summary>
    public string SubtituloSeccion => SeccionActiva switch
    {
        Seccion.Resumen => "Vista consolidada de estado administrativo y alertas operativas.",
        Seccion.Colaboradores => "Expedientes del personal de la empresa activa.",
        Seccion.Alertas => "Vencimientos y efemérides que exigen atención.",
        Seccion.Documentos => "Archivo digitalizado del expediente.",
        Seccion.Historial => "Movimientos laborales registrados.",
        Seccion.Reportes => "Constancias, planillas y listados.",
        _ => "Catálogos y parámetros de la empresa activa."
    };

    // ─── Ficha ──────────────────────────────────────────────────────────────

    /// <summary>Expediente abierto, o nulo si no se esta viendo ninguno.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelResumen))]
    [NotifyPropertyChangedFor(nameof(PanelColaboradores))]
    [NotifyPropertyChangedFor(nameof(PanelAlertas))]
    [NotifyPropertyChangedFor(nameof(PanelCatalogos))]
    [NotifyPropertyChangedFor(nameof(PanelRespaldos))]
    [NotifyPropertyChangedFor(nameof(PanelPendiente))]
    [NotifyPropertyChangedFor(nameof(PanelFicha))]
    public partial DetalleColaborador? Ficha { get; set; }

    // Que panel se ve. Uno solo a la vez. Los formularios de captura (edicion y
    // novedad) tapan a todos, incluida la ficha; la ficha, a su vez, tapa a los
    // demas. Edicion y novedad nunca coexisten: se abren desde la ficha.
    public bool PanelDocumento => ModoDocumento && !ModoVistaPrevia;
    public bool PanelNovedad => ModoNovedad && !ModoDocumento && !ModoVistaPrevia;
    public bool PanelEdicion => ModoEdicion && !ModoNovedad && !ModoDocumento && !ModoVistaPrevia;
    public bool PanelFicha => !ModoVistaPrevia && !ModoEdicion && !ModoNovedad && !ModoDocumento && Ficha is not null;
    public bool PanelResumen => !ModoVistaPrevia && !ModoEdicion && !ModoNovedad && !ModoDocumento && Ficha is null && SeccionActiva == Seccion.Resumen;
    public bool PanelColaboradores => !ModoVistaPrevia && !ModoEdicion && !ModoNovedad && !ModoDocumento && Ficha is null && SeccionActiva == Seccion.Colaboradores;
    public bool PanelAlertas => !ModoVistaPrevia && !ModoEdicion && !ModoNovedad && !ModoDocumento && Ficha is null && SeccionActiva == Seccion.Alertas;

    /// <summary>Configuración: catálogos (CR-06) o respaldos (CR-12), nunca los dos.</summary>
    public bool PanelCatalogos => !ModoVistaPrevia && !ModoEdicion && !ModoNovedad && !ModoDocumento && Ficha is null
        && SeccionActiva == Seccion.Configuracion
        && SubseccionActiva == SubseccionConfiguracion.Catalogos;

    public bool PanelRespaldos => !ModoVistaPrevia && !ModoEdicion && !ModoNovedad && !ModoDocumento && Ficha is null
        && SeccionActiva == Seccion.Configuracion
        && SubseccionActiva == SubseccionConfiguracion.Respaldos;

    public bool PanelPendiente => !ModoVistaPrevia && !ModoEdicion && !ModoNovedad && !ModoDocumento && Ficha is null
        && SeccionActiva is Seccion.Documentos or Seccion.Historial or Seccion.Reportes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsPestanaGeneral))]
    [NotifyPropertyChangedFor(nameof(EsPestanaHistorial))]
    [NotifyPropertyChangedFor(nameof(EsPestanaContratos))]
    [NotifyPropertyChangedFor(nameof(EsPestanaContactos))]
    [NotifyPropertyChangedFor(nameof(EsPestanaNovedades))]
    public partial Pestana PestanaActiva { get; set; }

    public bool EsPestanaGeneral => PestanaActiva == Pestana.General;
    public bool EsPestanaHistorial => PestanaActiva == Pestana.Historial;
    public bool EsPestanaContratos => PestanaActiva == Pestana.Contratos;
    public bool EsPestanaContactos => PestanaActiva == Pestana.Contactos;
    public bool EsPestanaNovedades => PestanaActiva == Pestana.Novedades;

    /// <summary>Cuantos documentos tiene el expediente abierto.</summary>
    public string ConteoDocumentos => Ficha is null
        ? string.Empty
        : "Ver todos los documentos (" + Ficha.Documentos.Count + ")";

    /// <summary>Contacto de emergencia principal, el que la ficha destaca.</summary>
    public LineaContacto? ContactoPrincipal => Ficha?.Contactos
        .OrderByDescending(c => c.EsPrincipal)
        .FirstOrDefault();

    public bool HayContactoPrincipal => ContactoPrincipal is not null;

    public bool SinContactoPrincipal => ContactoPrincipal is null;

    /// <summary>"Maria Flores (Esposa)", como en la maqueta.</summary>
    public string ContactoPrincipalTexto => ContactoPrincipal is null
        ? string.Empty
        : string.IsNullOrWhiteSpace(ContactoPrincipal.Parentesco)
            ? ContactoPrincipal.Nombre
            : ContactoPrincipal.Nombre + " (" + ContactoPrincipal.Parentesco + ")";

    /// <summary>Verdadero si el expediente abierto no tiene documentos.</summary>
    public bool SinDocumentos => Ficha is not null && Ficha.Documentos.Count == 0;

    /// <summary>Verdadero si el expediente abierto no tiene incidencias.</summary>
    public bool SinIncidencias => Ficha is not null && Ficha.Incidencias.Count == 0;

    /// <summary>Verdadero si el expediente abierto no tiene vacaciones programadas.</summary>
    public bool SinVacaciones => Ficha is not null && Ficha.Vacaciones.Count == 0;

    // ─── Carga ──────────────────────────────────────────────────────────────

    protected override async Task CargarDatosAsync()
    {
        EnHiloUi(() =>
        {
            EmpresaActiva = _contextoEmpresa.NombreEmpresaActiva;
            ColorEmpresa = _contextoEmpresa.ColorEmpresaActiva;
            Usuario = _sesion.NombreCompleto;
            PerfilTexto = DescribirPerfil();
            InicialesUsuario = CalcularIniciales(_sesion.NombreCompleto);
        });

        await CargarSeccionAsync().ConfigureAwait(true);
    }

    /// <summary>Cada seccion trae lo suyo cuando se abre (CLAUDE.md, regla 13).</summary>
    private async Task CargarSeccionAsync(CancellationToken cancelacion = default)
    {
        switch (SeccionActiva)
        {
            case Seccion.Resumen:
                await CorrerMotorSiHaceFaltaAsync(cancelacion).ConfigureAwait(true);
                await CargarResumenAsync(cancelacion).ConfigureAwait(true);
                break;

            case Seccion.Colaboradores:
                await CargarColaboradoresAsync(cancelacion).ConfigureAwait(true);
                break;

            case Seccion.Alertas:
                await CorrerMotorSiHaceFaltaAsync(cancelacion).ConfigureAwait(true);
                await CargarAvisosAsync(cancelacion).ConfigureAwait(true);
                break;

            case Seccion.Configuracion:
                // Configuración tiene dos mitades y solo se consulta la abierta.
                if (SubseccionActiva == SubseccionConfiguracion.Respaldos)
                {
                    await CargarRespaldosAsync(cancelacion).ConfigureAwait(true);
                }
                else
                {
                    await CargarCatalogoAsync(cancelacion).ConfigureAwait(true);
                }

                break;

            default:
                // Las secciones todavia no construidas no consultan nada.
                break;
        }

        // El globo del menu lateral se refresca en toda carga, no solo al abrir
        // Alertas: si el numero solo se actualizara ahi, el contador mentiría
        // mientras el usuario trabaja en cualquier otra pantalla (CR-10).
        await ActualizarContadorAvisosAsync(cancelacion).ConfigureAwait(true);
    }

    /// <summary>Refresca el globo de avisos del menú lateral. Es un COUNT, nada más.</summary>
    private async Task ActualizarContadorAvisosAsync(CancellationToken cancelacion)
    {
        try
        {
            var pendientes = await _alertas.ContarPendientesAsync(cancelacion).ConfigureAwait(true);
            EnHiloUi(() => AvisosPendientes = pendientes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Que el contador no se pueda leer no puede tumbar la pantalla que
            // el usuario acaba de abrir.
            Registro.LogWarning(ex, "No se pudo actualizar el contador de avisos.");
        }
    }

    /// <summary>Metricas, directorio rapido y avisos de la pantalla de resumen.</summary>
    private async Task CargarResumenAsync(CancellationToken cancelacion)
    {
        var metricas = await _servicioResumen.ObtenerAsync(cancelacion).ConfigureAwait(true);

        var directorio = await _colaboradores
            .ObtenerAsync(new FiltroColaboradores(Tope: FilasDelDirectorio), cancelacion)
            .ConfigureAwait(true);

        cancelacion.ThrowIfCancellationRequested();

        EnHiloUi(() =>
        {
            Resumen = metricas;

            Directorio.Clear();
            foreach (var fila in directorio)
            {
                Directorio.Add(fila);
            }

            SinResultadosDirectorio = Directorio.Count == 0;
        });

        await CargarAvisosAsync(cancelacion).ConfigureAwait(true);
    }

    /// <summary>Tabla de colaboradores con sus desplegables de filtro.</summary>
    private async Task CargarColaboradoresAsync(CancellationToken cancelacion)
    {
        if (Departamentos.Count == 0)
        {
            await CargarOpcionesDeFiltroAsync(cancelacion).ConfigureAwait(true);
        }

        await ConsultarAsync(cancelacion).ConfigureAwait(true);
    }

    /// <summary>Avisos pendientes de la empresa activa.</summary>
    private async Task CargarAvisosAsync(CancellationToken cancelacion)
    {
        var avisos = await _alertas.ObtenerPendientesAsync(cancelacion).ConfigureAwait(true);

        cancelacion.ThrowIfCancellationRequested();

        // Lo urgente primero: primero lo vencido, despues lo que esta por
        // vencer, y dentro de cada grupo por fecha.
        var ordenados = avisos
            .OrderByDescending(a => a.Nivel)
            .ThenBy(a => a.FechaReferencia)
            .ToList();

        EnHiloUi(() =>
        {
            Avisos.Clear();
            foreach (var aviso in ordenados)
            {
                Avisos.Add(aviso);
            }

            AvisosPendientes = Avisos.Count;
            SinAvisos = Avisos.Count == 0;
        });
    }

    /// <summary>
    /// El motor calcula los avisos de la empresa activa. Es idempotente, pero
    /// escribe: se corre una vez por empresa y por sesion, no en cada aparicion.
    /// </summary>
    private async Task CorrerMotorSiHaceFaltaAsync(CancellationToken cancelacion)
    {
        if (_empresaConMotorCorrido == _contextoEmpresa.EmpresaActivaId)
        {
            return;
        }

        var resultado = await _alertas.GenerarAsync(cancelacion).ConfigureAwait(true);
        _empresaConMotorCorrido = _contextoEmpresa.EmpresaActivaId;

        Registro.LogInformation(
            "Motor de alertas corrido al abrir la pantalla: {Generados} nuevos, {Pendientes} pendientes.",
            resultado.Generados, resultado.Pendientes);
    }

    /// <summary>Rellena los desplegables con los catalogos de la empresa activa.</summary>
    private async Task CargarOpcionesDeFiltroAsync(CancellationToken cancelacion)
    {
        var opciones = await _colaboradores.ObtenerOpcionesAsync(cancelacion).ConfigureAwait(true);

        EnHiloUi(() =>
        {
            _rellenandoFiltros = true;
            try
            {
                Departamentos.Clear();
                foreach (var opcion in opciones.Departamentos)
                {
                    Departamentos.Add(opcion);
                }

                Sucursales.Clear();
                foreach (var opcion in opciones.Sucursales)
                {
                    Sucursales.Add(opcion);
                }

                DepartamentoSeleccionado = Departamentos.FirstOrDefault();
                SucursalSeleccionada = Sucursales.FirstOrDefault();
                EstadoSeleccionado = Estados[0];

                _totalSinFiltrar = opciones.TotalColaboradores;
            }
            finally
            {
                _rellenandoFiltros = false;
            }
        });
    }

    /// <summary>Ejecuta la consulta de colaboradores con los criterios actuales.</summary>
    private async Task ConsultarAsync(CancellationToken cancelacion)
    {
        var filtro = ArmarFiltro();

        var filas = await _colaboradores.ObtenerAsync(filtro, cancelacion).ConfigureAwait(true);

        cancelacion.ThrowIfCancellationRequested();

        EnHiloUi(() =>
        {
            Colaboradores.Clear();
            foreach (var fila in filas)
            {
                Colaboradores.Add(fila);
            }

            HayFiltros = !filtro.EstaVacio;
            SinResultados = Colaboradores.Count == 0;

            ResumenConteo = HayFiltros
                ? Colaboradores.Count + " de " + _totalSinFiltrar + " registros"
                : Colaboradores.Count + " registros";
        });
    }

    private FiltroColaboradores ArmarFiltro() => new(
        string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
        DepartamentoSeleccionado is { EsTodos: false } departamento ? departamento.Id : null,
        SucursalSeleccionada is { EsTodos: false } sucursal ? sucursal.Id : null,
        EstadoSeleccionado is { EsTodos: false } estado ? (EstadoColaborador)estado.Id : null);

    // ─── Reaccion a los criterios ───────────────────────────────────────────

    /// <summary>
    /// Escribir en el buscador reprograma la consulta, no la dispara. Ademas
    /// lleva a la seccion de colaboradores: el buscador de la barra superior
    /// busca personas, asi que el resultado tiene que quedar a la vista.
    /// </summary>
    partial void OnBusquedaChanged(string value)
    {
        if (_rellenandoFiltros)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(value) && SeccionActiva != Seccion.Colaboradores)
        {
            Ficha = null;
            SeccionActiva = Seccion.Colaboradores;
        }

        ProgramarBusqueda();
    }

    partial void OnDepartamentoSeleccionadoChanged(OpcionFiltro? value) => ConsultarSiCorresponde();

    partial void OnSucursalSeleccionadaChanged(OpcionFiltro? value) => ConsultarSiCorresponde();

    partial void OnEstadoSeleccionadoChanged(OpcionFiltro? value) => ConsultarSiCorresponde();

    /// <summary>Los desplegables consultan de inmediato: son un solo clic.</summary>
    private void ConsultarSiCorresponde()
    {
        if (_rellenandoFiltros || SeccionActiva != Seccion.Colaboradores)
        {
            return;
        }

        ProgramarBusqueda(TimeSpan.Zero);
    }

    /// <summary>
    /// Cancela la consulta pendiente y programa otra. Devuelve enseguida: la
    /// espera y la consulta corren aparte, con su propio try/catch completo.
    /// </summary>
    private void ProgramarBusqueda(TimeSpan? espera = null)
    {
        var anterior = _cancelacionBusqueda;
        var nueva = new CancellationTokenSource();
        _cancelacionBusqueda = nueva;

        anterior?.Cancel();
        anterior?.Dispose();

        // Descartar la tarea es seguro porque el metodo captura todo lo que
        // pueda lanzar: nunca se convierte en una excepcion no observada
        // (CLAUDE.md, regla 3).
        _ = EsperarYConsultarAsync(espera ?? EsperaAntesDeBuscar, nueva.Token);
    }

    private async Task EsperarYConsultarAsync(TimeSpan espera, CancellationToken cancelacion)
    {
        try
        {
            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, cancelacion).ConfigureAwait(true);
            }

            if (Departamentos.Count == 0)
            {
                await CargarOpcionesDeFiltroAsync(cancelacion).ConfigureAwait(true);
            }

            await ConsultarAsync(cancelacion).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // El usuario siguio escribiendo. Es el camino normal, no un fallo.
        }
        catch (Exception ex)
        {
            Registro.LogError(ex, "Falló la búsqueda de colaboradores.");

            await Dialogo.AvisarAsync(
                "No se pudo completar la búsqueda",
                "RH Manager no logróconsultar los colaboradores. El detalle quedó en el archivo de registro.")
                .ConfigureAwait(true);
        }
    }

    // ─── Ficha del colaborador ──────────────────────────────────────────────

    /// <summary>Abre el expediente. El detalle se consulta aca, no antes.</summary>
    [RelayCommand]
    private Task AbrirFichaAsync(FilaColaborador? fila)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (fila is null)
                {
                    return;
                }

                var detalle = await _ficha.ObtenerAsync(fila.Id).ConfigureAwait(true);

                if (detalle is null)
                {
                    await Dialogo.AvisarAsync(
                        "Expediente no disponible",
                        "Ese colaborador ya no está disponible en la empresa activa.").ConfigureAwait(true);
                    return;
                }

                EnHiloUi(() =>
                {
                    PestanaActiva = Pestana.General;
                    Ficha = detalle;

                    NotificarCamposDeLaFicha();
                });
            },
            "apertura del expediente de " + (fila?.NombreCompleto ?? "desconocido"),
            "No se pudo abrir el expediente. El detalle quedó en el archivo de registro.");

    /// <summary>Cierra la ficha y vuelve al directorio.</summary>
    [RelayCommand]
    private void CerrarFicha()
    {
        Ficha = null;
        NotificarCamposDeLaFicha();
    }

    /// <summary>
    /// Las propiedades derivadas de la ficha no las cubre NotifyPropertyChangedFor
    /// porque son varias y cambian juntas: se avisan de una vez al abrir y al
    /// cerrar el expediente.
    /// </summary>
    private void NotificarCamposDeLaFicha()
    {
        OnPropertyChanged(nameof(ConteoDocumentos));
        OnPropertyChanged(nameof(ContactoPrincipal));
        OnPropertyChanged(nameof(ContactoPrincipalTexto));
        OnPropertyChanged(nameof(HayContactoPrincipal));
        OnPropertyChanged(nameof(SinContactoPrincipal));
        OnPropertyChanged(nameof(SinDocumentos));
        OnPropertyChanged(nameof(SinIncidencias));
        OnPropertyChanged(nameof(SinVacaciones));
    }

    /// <summary>Cambia de pestana dentro de la ficha.</summary>
    [RelayCommand]
    private void IrAPestana(string? nombre)
    {
        if (Enum.TryParse<Pestana>(nombre, ignoreCase: true, out var pestana))
        {
            PestanaActiva = pestana;
        }
    }

    // ─── Navegacion interna ─────────────────────────────────────────────────

    /// <summary>Cambia de seccion desde el menu lateral o desde un enlace.</summary>
    [RelayCommand]
    private Task IrASeccionAsync(string? nombre)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (!Enum.TryParse<Seccion>(nombre, ignoreCase: true, out var seccion))
                {
                    Registro.LogWarning("Se pidió una sección desconocida: {Nombre}", nombre ?? "(nula)");
                    return;
                }

                EnHiloUi(() =>
                {
                    CerrarFicha();
                    SeccionActiva = seccion;
                });

                await CargarSeccionAsync().ConfigureAwait(true);
            },
            "cambio a la sección " + (nombre ?? "desconocida"),
            "No se pudo abrir esa sección. El detalle quedó en el archivo de registro.");

    /// <summary>Deja los criterios como al abrir la pantalla.</summary>
    [RelayCommand]
    private Task LimpiarFiltrosAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                EnHiloUi(() =>
                {
                    _rellenandoFiltros = true;
                    try
                    {
                        Busqueda = string.Empty;
                        DepartamentoSeleccionado = Departamentos.FirstOrDefault();
                        SucursalSeleccionada = Sucursales.FirstOrDefault();
                        EstadoSeleccionado = Estados[0];
                    }
                    finally
                    {
                        _rellenandoFiltros = false;
                    }
                });

                await ConsultarAsync(CancellationToken.None).ConfigureAwait(true);
            },
            "limpieza de filtros",
            "No se pudieron limpiar los filtros.");

    /// <summary>Marca un aviso como atendido y lo saca de la lista.</summary>
    [RelayCommand]
    private Task ResolverAvisoAsync(LineaAviso? aviso)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (aviso is null)
                {
                    return;
                }

                await _alertas.ResolverAsync(aviso.Id).ConfigureAwait(true);
                await CargarAvisosAsync(CancellationToken.None).ConfigureAwait(true);
            },
            "resolución del aviso " + (aviso?.Id.ToString() ?? "desconocido"),
            "No se pudo marcar el aviso como atendido.");

    // ─── Sesion ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task CambiarEmpresaAsync()
        => EjecutarSeguroAsync(
            () => _navegacion.IrAsync(RutasNavegacion.Empresas),
            "cambio de empresa",
            "No se pudo abrir el selector de empresas.");

    [RelayCommand]
    private Task CerrarSesionAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                _sesion.Cerrar();
                _contextoEmpresa.Limpiar();
                _empresaConMotorCorrido = 0;
                Registro.LogInformation("Sesión cerrada.");
                await _navegacion.IrAsync(RutasNavegacion.Acceso).ConfigureAwait(true);
            },
            "cierre de sesión",
            "No se pudo cerrar la sesión.");

    // ─── Auxiliares ─────────────────────────────────────────────────────────

    private string DescribirPerfil() => _sesion.Perfil switch
    {
        PerfilUsuario.SuperAdministrador => "Super Administrador",
        PerfilUsuario.Administrador => "Administrador",
        PerfilUsuario.SupervisorSucursal => "Supervisor de sucursal",
        PerfilUsuario.Consulta => "Consulta",
        _ => string.Empty
    };

    /// <summary>Dos iniciales para la pastilla del usuario.</summary>
    private static string CalcularIniciales(string nombre)
    {
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0)
        {
            return "?";
        }

        var primera = partes[0][..1];
        var segunda = partes.Length > 1 ? partes[^1][..1] : string.Empty;
        return (primera + segunda).ToUpperInvariant();
    }
}
