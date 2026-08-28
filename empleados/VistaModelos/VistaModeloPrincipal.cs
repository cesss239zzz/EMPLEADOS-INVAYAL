using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Datos;
using empleados.Datos.Entidades;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>Secciones del menu lateral.</summary>
public enum Seccion
{
    Resumen,
    Colaboradores,
    Contratos,
    Documentos,
    Avisos,
    Catalogos,
    Usuarios,
    Reportes
}

/// <summary>Pestanas de la ficha del colaborador.</summary>
public enum Pestana
{
    General,
    Laboral,
    Documentos,
    Historial,
    Emergencia
}

/// <summary>Un renglon del menu lateral.</summary>
public sealed partial class ItemMenu : ObservableObject
{
    public ItemMenu(Seccion seccion, string titulo, string grupo)
    {
        Seccion = seccion;
        Titulo = titulo;
        Grupo = grupo;
    }

    public Seccion Seccion { get; }
    public string Titulo { get; }
    public string Grupo { get; }

    /// <summary>El item activo lleva borde izquierdo con el color de la empresa.</summary>
    [ObservableProperty]
    public partial bool EsActivo { get; set; }
}

/// <summary>
/// Contenedor de la aplicacion: barra superior, menu lateral y area de contenido,
/// con la tabla de colaboradores y sus filtros.
/// </summary>
public sealed partial class VistaModeloPrincipal : VistaModeloBase
{
    /// <summary>
    /// Espera entre la ultima tecla y la consulta. Sin esto, escribir "Delmy"
    /// lanzaria cinco consultas y la ultima podria llegar antes que otra.
    /// </summary>
    private static readonly TimeSpan EsperaAntesDeBuscar = TimeSpan.FromMilliseconds(250);

    private readonly IServicioColaboradores _colaboradores;
    private readonly IServicioFicha _ficha;
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

    public VistaModeloPrincipal(
        IServicioColaboradores colaboradores,
        IServicioFicha ficha,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        IServicioNavegacion navegacion,
        ILogger<VistaModeloPrincipal> registro,
        IServicioDialogo dialogo)
        : base(registro, dialogo)
    {
        _colaboradores = colaboradores;
        _ficha = ficha;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _navegacion = navegacion;

        Titulo = "RH Manager";
        EmpresaActiva = string.Empty;
        ColorEmpresa = "#0F3D6E";
        Usuario = string.Empty;
        PerfilTexto = string.Empty;
        TituloSeccion = "Resumen";
        Busqueda = string.Empty;
        ResumenConteo = string.Empty;

        Menu =
        [
            new ItemMenu(Seccion.Resumen, "Resumen", "Personal") { EsActivo = true },
            new ItemMenu(Seccion.Colaboradores, "Colaboradores", "Personal"),
            new ItemMenu(Seccion.Contratos, "Contratos", "Archivo"),
            new ItemMenu(Seccion.Documentos, "Documentos", "Archivo"),
            new ItemMenu(Seccion.Avisos, "Avisos", "Archivo"),
            new ItemMenu(Seccion.Catalogos, "Catalogos", "Administracion"),
            new ItemMenu(Seccion.Usuarios, "Usuarios", "Administracion"),
            new ItemMenu(Seccion.Reportes, "Reportes", "Administracion")
        ];

        Estados =
        [
            new OpcionFiltro(0, "Todos los estados"),
            new OpcionFiltro((int)EstadoColaborador.Activo, "Activo"),
            new OpcionFiltro((int)EstadoColaborador.Suspendido, "Suspendido"),
            new OpcionFiltro((int)EstadoColaborador.Inactivo, "Inactivo")
        ];
    }

    public IReadOnlyList<ItemMenu> Menu { get; }

    /// <summary>Filas de la tabla de colaboradores.</summary>
    public ObservableCollection<FilaColaborador> Colaboradores { get; } = [];

    public ObservableCollection<OpcionFiltro> Departamentos { get; } = [];

    public ObservableCollection<OpcionFiltro> Sucursales { get; } = [];

    public IReadOnlyList<OpcionFiltro> Estados { get; }

    [ObservableProperty]
    public partial string EmpresaActiva { get; set; }

    [ObservableProperty]
    public partial string ColorEmpresa { get; set; }

    [ObservableProperty]
    public partial string Usuario { get; set; }

    [ObservableProperty]
    public partial string PerfilTexto { get; set; }

    [ObservableProperty]
    public partial string TituloSeccion { get; set; }

    /// <summary>Texto del buscador de la barra superior.</summary>
    [ObservableProperty]
    public partial string Busqueda { get; set; }

    [ObservableProperty]
    public partial OpcionFiltro? DepartamentoSeleccionado { get; set; }

    [ObservableProperty]
    public partial OpcionFiltro? SucursalSeleccionada { get; set; }

    [ObservableProperty]
    public partial OpcionFiltro? EstadoSeleccionado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsColaboradores))]
    [NotifyPropertyChangedFor(nameof(EsOtraSeccion))]
    public partial Seccion SeccionActiva { get; set; }

    /// <summary>Expediente abierto, o nulo si se esta viendo la tabla.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsColaboradores))]
    [NotifyPropertyChangedFor(nameof(EsOtraSeccion))]
    [NotifyPropertyChangedFor(nameof(HayFicha))]
    public partial DetalleColaborador? Ficha { get; set; }

    public bool HayFicha => Ficha is not null;

    /// <summary>La tabla se ve cuando la seccion es Colaboradores y no hay ficha abierta.</summary>
    public bool EsColaboradores => SeccionActiva == Seccion.Colaboradores && Ficha is null;

    public bool EsOtraSeccion => SeccionActiva != Seccion.Colaboradores && Ficha is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsGeneral))]
    [NotifyPropertyChangedFor(nameof(EsLaboral))]
    [NotifyPropertyChangedFor(nameof(EsDocumentos))]
    [NotifyPropertyChangedFor(nameof(EsHistorial))]
    [NotifyPropertyChangedFor(nameof(EsEmergencia))]
    public partial Pestana PestanaActiva { get; set; }

    public bool EsGeneral => PestanaActiva == Pestana.General;
    public bool EsLaboral => PestanaActiva == Pestana.Laboral;
    public bool EsDocumentos => PestanaActiva == Pestana.Documentos;
    public bool EsHistorial => PestanaActiva == Pestana.Historial;
    public bool EsEmergencia => PestanaActiva == Pestana.Emergencia;

    /// <summary>Texto de la cabecera del panel: "3 de 9 registros".</summary>
    [ObservableProperty]
    public partial string ResumenConteo { get; set; }

    /// <summary>Verdadero cuando el filtro no devolvio ninguna fila.</summary>
    [ObservableProperty]
    public partial bool SinResultados { get; set; }

    /// <summary>Verdadero si hay algun criterio de busqueda puesto.</summary>
    [ObservableProperty]
    public partial bool HayFiltros { get; set; }

    /// <summary>Avisos pendientes. En E7 lo calcula el motor de alertas.</summary>
    [ObservableProperty]
    public partial int AvisosPendientes { get; set; }

    /// <summary>Cuantos colaboradores tiene la empresa sin filtrar.</summary>
    private int _totalSinFiltrar;

    protected override async Task CargarDatosAsync()
    {
        EnHiloUi(() =>
        {
            EmpresaActiva = _contextoEmpresa.NombreEmpresaActiva;
            ColorEmpresa = _contextoEmpresa.ColorEmpresaActiva;
            Usuario = _sesion.NombreCompleto;
            PerfilTexto = DescribirPerfil();
        });

        await CargarSeccionAsync().ConfigureAwait(true);
    }

    /// <summary>Cada seccion trae lo suyo cuando se abre (CLAUDE.md, regla 13).</summary>
    private async Task CargarSeccionAsync(CancellationToken cancelacion = default)
    {
        if (SeccionActiva != Seccion.Colaboradores)
        {
            return;
        }

        if (Departamentos.Count == 0)
        {
            await CargarOpcionesDeFiltroAsync(cancelacion).ConfigureAwait(true);
        }

        await ConsultarAsync(cancelacion).ConfigureAwait(true);
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

    /// <summary>Ejecuta la consulta con los criterios actuales.</summary>
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

    /// <summary>Escribir en el buscador reprograma la consulta, no la dispara.</summary>
    partial void OnBusquedaChanged(string value)
    {
        if (_rellenandoFiltros)
        {
            return;
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

            await ConsultarAsync(cancelacion).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // El usuario siguio escribiendo. Es el camino normal, no un fallo.
        }
        catch (Exception ex)
        {
            Registro.LogError(ex, "Fallo la busqueda de colaboradores.");

            await Dialogo.AvisarAsync(
                "No se pudo completar la busqueda",
                "RH Manager no logro consultar los colaboradores. El detalle quedo en el archivo de registro.")
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
                        "Ese colaborador ya no esta disponible en la empresa activa.").ConfigureAwait(true);
                    return;
                }

                EnHiloUi(() =>
                {
                    PestanaActiva = Pestana.General;
                    Ficha = detalle;
                });
            },
            "apertura del expediente de " + (fila?.NombreCompleto ?? "desconocido"),
            "No se pudo abrir el expediente. El detalle quedo en el archivo de registro.");

    /// <summary>Cierra la ficha y vuelve a la tabla.</summary>
    [RelayCommand]
    private void CerrarFicha() => Ficha = null;

    /// <summary>Cambia de pestana dentro de la ficha.</summary>
    [RelayCommand]
    private void IrAPestana(string? nombre)
    {
        if (Enum.TryParse<Pestana>(nombre, ignoreCase: true, out var pestana))
        {
            PestanaActiva = pestana;
        }
    }

    /// <summary>
    /// La constancia de trabajo se genera en PDF con QuestPDF y codigo QR: es
    /// la etapa E9. Hasta entonces el boton lo dice, no se queda mudo
    /// (CLAUDE.md, regla 4).
    /// </summary>
    [RelayCommand]
    private Task ConstanciaAsync()
        => EjecutarSeguroAsync(
            () => Dialogo.AvisarAsync(
                "Constancia de trabajo",
                "La constancia en PDF con codigo QR verificable se habilita en la etapa de reportes."),
            "solicitud de constancia de trabajo",
            "No se pudo mostrar el aviso.");

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

    private string DescribirPerfil() => _sesion.Perfil switch
    {
        PerfilUsuario.SuperAdministrador => "Super Administrador",
        PerfilUsuario.Administrador => "Administrador",
        PerfilUsuario.SupervisorSucursal => "Supervisor de sucursal",
        PerfilUsuario.Consulta => "Consulta",
        _ => string.Empty
    };

    [RelayCommand]
    private Task IrASeccionAsync(ItemMenu? item)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (item is null)
                {
                    return;
                }

                EnHiloUi(() =>
                {
                    foreach (var otro in Menu)
                    {
                        otro.EsActivo = ReferenceEquals(otro, item);
                    }

                    SeccionActiva = item.Seccion;
                    TituloSeccion = item.Titulo;
                });

                await CargarSeccionAsync().ConfigureAwait(true);
            },
            "cambio a la seccion " + (item?.Titulo ?? "desconocida"),
            "No se pudo abrir esa seccion. El detalle quedo en el archivo de registro.");

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
                Registro.LogInformation("Sesion cerrada.");
                await _navegacion.IrAsync(RutasNavegacion.Acceso).ConfigureAwait(true);
            },
            "cierre de sesion",
            "No se pudo cerrar la sesion.");

    /// <summary>Todo boton visible tiene comando (CLAUDE.md, regla 4).</summary>
    [RelayCommand]
    private Task ProximamenteAsync()
        => EjecutarSeguroAsync(
            () => Dialogo.AvisarAsync("Proximamente",
                "Esta funcion se habilita en una etapa posterior de RH Manager."),
            "aviso de funcion pendiente",
            "No se pudo mostrar el aviso.");
}
