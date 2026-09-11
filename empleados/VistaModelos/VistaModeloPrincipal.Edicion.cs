using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Datos.Entidades;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>Una opcion de un desplegable que representa un valor de enumeracion.</summary>
/// <param name="Valor">Valor entero persistido de la enumeracion.</param>
/// <param name="Nombre">Texto que ve el usuario.</param>
public sealed record OpcionEnum(int Valor, string Nombre);

/// <summary>
/// Parte de captura del contenedor principal: el formulario de alta y edicion
/// de colaboradores. Vive en un archivo aparte para no engrosar el nucleo del
/// ViewModel, pero es la misma clase parcial y comparte <c>EstaOcupado</c>, el
/// hilo de interfaz y el patron protegido de <see cref="VistaModeloBase"/>.
///
/// El formulario es un panel mas —<c>PanelEdicion</c>— que tapa a los demas,
/// igual que la ficha. No hay navegacion de Shell: la captura ocurre dentro de
/// la misma pantalla (CLAUDE.md, reglas 1 y 6 quedan cubiertas sin ruta nueva).
/// </summary>
public sealed partial class VistaModeloPrincipal
{
    /// <summary>Id del colaborador en edicion; cero mientras es un alta.</summary>
    private int _idEnEdicion;

    // ─── Estado del formulario ──────────────────────────────────────────────

    /// <summary>Verdadero mientras el formulario de captura esta abierto.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelEdicion))]
    [NotifyPropertyChangedFor(nameof(PanelFicha))]
    [NotifyPropertyChangedFor(nameof(PanelResumen))]
    [NotifyPropertyChangedFor(nameof(PanelColaboradores))]
    [NotifyPropertyChangedFor(nameof(PanelAlertas))]
    [NotifyPropertyChangedFor(nameof(PanelPendiente))]
    public partial bool ModoEdicion { get; set; }

    [ObservableProperty]
    public partial string TituloEdicion { get; set; }

    [ObservableProperty]
    public partial string SubtituloEdicion { get; set; }

    /// <summary>Mensaje de validacion bajo el formulario; vacio si todo esta bien.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayErrorEdicion))]
    public partial string ErrorEdicion { get; set; }

    public bool HayErrorEdicion => !string.IsNullOrEmpty(ErrorEdicion);

    /// <summary>El alta y la edicion son de Administrador o superior (regla del cliente).</summary>
    public bool PuedeCapturar => _sesion.PuedeCapturar;

    // ─── Campos editables ───────────────────────────────────────────────────

    [ObservableProperty] public partial string Codigo { get; set; }
    [ObservableProperty] public partial string Identidad { get; set; }
    [ObservableProperty] public partial string PrimerNombre { get; set; }
    [ObservableProperty] public partial string SegundoNombre { get; set; }
    [ObservableProperty] public partial string PrimerApellido { get; set; }
    [ObservableProperty] public partial string SegundoApellido { get; set; }
    [ObservableProperty] public partial string Telefono { get; set; }
    [ObservableProperty] public partial string Correo { get; set; }
    [ObservableProperty] public partial string Direccion { get; set; }

    /// <summary>Salario como texto: se valida y convierte a decimal al guardar.</summary>
    [ObservableProperty] public partial string Salario { get; set; }

    /// <summary>Opcional (CR-04): nula mientras no se capture.</summary>
    [ObservableProperty] public partial DateTime? FechaNacimiento { get; set; }

    /// <summary>Obligatoria: es el unico dato de fecha que siempre se conoce.</summary>
    [ObservableProperty] public partial DateTime FechaIngreso { get; set; }

    [ObservableProperty] public partial OpcionEnum? SexoSeleccionado { get; set; }
    [ObservableProperty] public partial OpcionEnum? EstadoEdSeleccionado { get; set; }

    [ObservableProperty] public partial OpcionCatalogo? SucursalEd { get; set; }

    [ObservableProperty] public partial OpcionCatalogo? DepartamentoEd { get; set; }

    [ObservableProperty] public partial OpcionCatalogo? PuestoEd { get; set; }

    // ─── Colecciones de catalogo ────────────────────────────────────────────

    public ObservableCollection<OpcionCatalogo> SucursalesEd { get; } = [];
    public ObservableCollection<OpcionCatalogo> DepartamentosEd { get; } = [];
    public ObservableCollection<OpcionCatalogo> PuestosEd { get; } = [];

    // ─── Atajos a catalogos vacios (CR-05) ──────────────────────────────────
    //
    // Cuando un catalogo esta vacio, el desplegable no sirve de nada. En su lugar
    // se ofrece un acceso directo al apartado de catalogos, y al volver el
    // formulario sigue con todo lo que ya se habia escrito.

    [ObservableProperty] public partial bool SinSucursales { get; set; }
    [ObservableProperty] public partial bool SinDepartamentos { get; set; }
    [ObservableProperty] public partial bool SinPuestos { get; set; }

    /// <summary>
    /// Verdadero mientras el usuario esta en catalogos habiendo salido del
    /// formulario de captura. Es lo que dibuja el boton "Volver al formulario".
    /// </summary>
    [ObservableProperty] public partial bool VolverAEdicionPendiente { get; set; }

    /// <summary>
    /// El sexo tambien es opcional (CR-04), asi que el desplegable ofrece la
    /// opcion de no declararlo. Vale cero para distinguirla de los valores reales
    /// de la enumeracion, que arrancan en uno.
    /// </summary>
    public IReadOnlyList<OpcionEnum> Sexos { get; } =
    [
        new OpcionEnum(0, "Sin especificar"),
        new OpcionEnum((int)Sexo.Masculino, "Masculino"),
        new OpcionEnum((int)Sexo.Femenino, "Femenino")
    ];

    public IReadOnlyList<OpcionEnum> EstadosEd { get; } =
    [
        new OpcionEnum((int)EstadoColaborador.Activo, "Activo"),
        new OpcionEnum((int)EstadoColaborador.Suspendido, "Suspendido"),
        new OpcionEnum((int)EstadoColaborador.Inactivo, "Inactivo")
    ];

    /// <summary>
    /// Valores fijos del formulario. Se llama desde el constructor: ningun campo
    /// enlazado debe quedar en null ni una fecha en 01/01/0001, que el DatePicker
    /// rechaza. No consulta la base (CLAUDE.md, regla 13).
    /// </summary>
    private void InicializarCamposEdicion()
    {
        TituloEdicion = string.Empty;
        SubtituloEdicion = string.Empty;
        ErrorEdicion = string.Empty;

        Codigo = string.Empty;
        Identidad = string.Empty;
        PrimerNombre = string.Empty;
        SegundoNombre = string.Empty;
        PrimerApellido = string.Empty;
        SegundoApellido = string.Empty;
        Telefono = string.Empty;
        Correo = string.Empty;
        Direccion = string.Empty;
        Salario = string.Empty;

        // Nacimiento arranca vacio: es opcional y ponerle una fecha de fabrica
        // hace que se guarde una mentira si nadie la corrige (CR-04).
        FechaNacimiento = null;
        FechaIngreso = DateTime.Today;

        SexoSeleccionado = Sexos[0];
        EstadoEdSeleccionado = EstadosEd[0];

        SinSucursales = false;
        SinDepartamentos = false;
        SinPuestos = false;
        VolverAEdicionPendiente = false;
    }

    // ─── Desplegables sin encadenar (CR-05) ─────────────────────────────────
    //
    // Antes, elegir departamento acotaba la lista de puestos y el puesto se
    // borraba si no pertenecia a ese departamento. Eso se elimino: sucursal,
    // departamento y puesto son ahora tres elecciones independientes y opcionales.
    // Una empresa puede tener un puesto de limpieza que no cuelga de ningun
    // departamento, y obligar a inventarle uno solo para poder guardarlo era
    // exactamente el problema que reporto el cliente.

    // ─── Comandos ───────────────────────────────────────────────────────────

    /// <summary>Abre el formulario en blanco para dar de alta un colaborador.</summary>
    [RelayCommand]
    private Task NuevoColaboradorAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (!await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                await CargarCatalogosEdicionAsync().ConfigureAwait(true);

                EnHiloUi(() =>
                {
                    _idEnEdicion = 0;
                    LimpiarFormulario();
                    TituloEdicion = "Nuevo colaborador";
                    SubtituloEdicion =
                        "Registro de un expediente nuevo en " + _contextoEmpresa.NombreEmpresaActiva + ".";
                    ErrorEdicion = string.Empty;
                    Ficha = null;
                    ModoEdicion = true;
                });
            },
            "apertura del alta de colaborador",
            "No se pudo abrir el formulario de alta. El detalle quedó en el archivo de registro.");

    /// <summary>Abre el formulario con los datos del expediente que se esta viendo.</summary>
    [RelayCommand]
    private Task EditarColaboradorAsync()
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

                await CargarCatalogosEdicionAsync().ConfigureAwait(true);

                var datos = await _colaboradores.ObtenerParaEdicionAsync(Ficha.Id).ConfigureAwait(true);
                if (datos is null)
                {
                    await Dialogo.AvisarAsync(
                        "Expediente no disponible",
                        "Ese colaborador ya no está disponible en la empresa activa.").ConfigureAwait(true);
                    return;
                }

                EnHiloUi(() =>
                {
                    CargarFormulario(datos);
                    TituloEdicion = "Editar colaborador";
                    SubtituloEdicion = "Modificación del expediente de " + Ficha.NombreCompleto + ".";
                    ErrorEdicion = string.Empty;
                    ModoEdicion = true;
                });
            },
            "apertura de la edición de colaborador",
            "No se pudo abrir el formulario de edición. El detalle quedó en el archivo de registro.");

    /// <summary>Valida el formulario y guarda; abre la ficha del expediente resultante.</summary>
    [RelayCommand]
    private Task GuardarEdicionAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                var (valido, datos, error) = ValidarFormulario();
                if (!valido || datos is null)
                {
                    EnHiloUi(() => ErrorEdicion = error);
                    return;
                }

                var resultado = await _colaboradores.GuardarAsync(datos).ConfigureAwait(true);
                if (!resultado.Exito)
                {
                    EnHiloUi(() => ErrorEdicion = resultado.Error ?? "No se pudo guardar el expediente.");
                    return;
                }

                var eraAlta = datos.EsAlta;

                EnHiloUi(() =>
                {
                    ErrorEdicion = string.Empty;
                    ModoEdicion = false;
                    SeccionActiva = Seccion.Colaboradores;
                });

                // El directorio se refresca para que el cambio se vea al volver.
                await CargarColaboradoresAsync(CancellationToken.None).ConfigureAwait(true);

                // Se abre la ficha recien guardada: el usuario ve el resultado.
                await AbrirFichaPorIdAsync(resultado.Id).ConfigureAwait(true);

                await Dialogo.AvisarAsync(
                    eraAlta ? "Colaborador registrado" : "Cambios guardados",
                    eraAlta
                        ? "El expediente nuevo quedó guardado en la empresa activa."
                        : "Los cambios del expediente se guardaron correctamente.").ConfigureAwait(true);
            },
            "guardado del expediente de colaborador",
            "No se pudo guardar el expediente. El detalle quedó en el archivo de registro.");

    /// <summary>Cierra el formulario sin guardar y vuelve a lo que se veia antes.</summary>
    [RelayCommand]
    private void CancelarEdicion()
    {
        if (EstaOcupado) return;
        ModoEdicion = false;
        ErrorEdicion = string.Empty;
        VolverAEdicionPendiente = false;
    }

    /// <summary>
    /// Atajo del formulario al apartado de catalogos cuando el desplegable esta
    /// vacio (CR-05: «el desplegable ofrece un acceso directo del tipo + Crear
    /// nuevo que lleva al apartado de catalogos sin perder lo ya capturado»).
    ///
    /// No se pierde nada porque el formulario y los catalogos viven en el MISMO
    /// ViewModel: solo se tapa el panel de captura, sus campos siguen donde
    /// estaban y al volver reaparecen intactos.
    /// </summary>
    [RelayCommand]
    private Task CrearCatalogoDesdeEdicionAsync(string? nombreCatalogo)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (!Enum.TryParse<TipoCatalogo>(nombreCatalogo, ignoreCase: true, out var tipo))
                {
                    Registro.LogWarning("Se pidió un catálogo desconocido desde la edición: {Nombre}",
                        nombreCatalogo ?? "(nulo)");
                    return;
                }

                var pestana = PestanasCatalogo.FirstOrDefault(p => p.Tipo == tipo);

                EnHiloUi(() =>
                {
                    VolverAEdicionPendiente = true;
                    ModoEdicion = false;
                    ModoFormularioCatalogo = false;
                    SeccionActiva = Seccion.Configuracion;

                    if (pestana is not null)
                    {
                        PestanaCatalogoActiva = pestana;
                    }
                });

                await CargarCatalogoAsync(CancellationToken.None).ConfigureAwait(true);
            },
            "salto al catálogo " + (nombreCatalogo ?? "desconocido") + " desde la captura",
            "No se pudo abrir el apartado de catálogos. El detalle quedó en el archivo de registro.");

    /// <summary>
    /// Regresa al formulario de captura tras crear valores de catalogo. Recarga
    /// los desplegables para que aparezca lo recien creado y deja los campos del
    /// formulario tal como estaban.
    /// </summary>
    [RelayCommand]
    private Task VolverAEdicionAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                // Se recuerda lo elegido: recargar los desplegables reemplaza las
                // instancias y sin esto la seleccion se perderia.
                var sucursalId = SucursalEd?.Id;
                var departamentoId = DepartamentoEd?.Id;
                var puestoId = PuestoEd?.Id;

                await CargarCatalogosEdicionAsync().ConfigureAwait(true);

                EnHiloUi(() =>
                {
                    SucursalEd = SucursalesEd.FirstOrDefault(s => s.Id == sucursalId);
                    DepartamentoEd = DepartamentosEd.FirstOrDefault(d => d.Id == departamentoId);
                    PuestoEd = PuestosEd.FirstOrDefault(p => p.Id == puestoId);

                    VolverAEdicionPendiente = false;
                    SeccionActiva = Seccion.Colaboradores;
                    ModoEdicion = true;
                });
            },
            "regreso al formulario de captura",
            "No se pudo volver al formulario. El detalle quedó en el archivo de registro.");

    // ─── Auxiliares de carga ────────────────────────────────────────────────

    private async Task CargarCatalogosEdicionAsync()
    {
        var catalogos = await _colaboradores.ObtenerCatalogosEdicionAsync().ConfigureAwait(true);

        EnHiloUi(() =>
        {
            SucursalesEd.Clear();
            foreach (var sucursal in catalogos.Sucursales)
            {
                SucursalesEd.Add(sucursal);
            }

            DepartamentosEd.Clear();
            foreach (var departamento in catalogos.Departamentos)
            {
                DepartamentosEd.Add(departamento);
            }

            // Los puestos ya no se acotan por departamento: van todos (CR-05).
            PuestosEd.Clear();
            foreach (var puesto in catalogos.Puestos)
            {
                PuestosEd.Add(puesto);
            }

            // Con el catalogo vacio el desplegable no sirve: la pantalla ofrece
            // en su lugar el atajo para crear el primer valor.
            SinSucursales = SucursalesEd.Count == 0;
            SinDepartamentos = DepartamentosEd.Count == 0;
            SinPuestos = PuestosEd.Count == 0;
        });
    }

    /// <summary>Reabre la ficha por identificador tras guardar. No reentra en el comando.</summary>
    private async Task AbrirFichaPorIdAsync(int colaboradorId)
    {
        var detalle = await _ficha.ObtenerAsync(colaboradorId).ConfigureAwait(true);
        if (detalle is null)
        {
            return;
        }

        EnHiloUi(() =>
        {
            PestanaActiva = Pestana.General;
            Ficha = detalle;
            NotificarCamposDeLaFicha();
        });
    }

    /// <summary>Deja el formulario en blanco para un alta.</summary>
    private void LimpiarFormulario()
    {
        Codigo = string.Empty;
        Identidad = string.Empty;
        PrimerNombre = string.Empty;
        SegundoNombre = string.Empty;
        PrimerApellido = string.Empty;
        SegundoApellido = string.Empty;
        Telefono = string.Empty;
        Correo = string.Empty;
        Direccion = string.Empty;
        Salario = string.Empty;

        FechaNacimiento = null;
        FechaIngreso = DateTime.Today;

        SexoSeleccionado = Sexos[0];
        EstadoEdSeleccionado = EstadosEd[0];

        // Ninguno viene elegido de fabrica: los tres son opcionales (CR-05).
        SucursalEd = null;
        DepartamentoEd = null;
        PuestoEd = null;
    }

    /// <summary>Vuelca los datos de un expediente en los campos del formulario.</summary>
    private void CargarFormulario(DatosEdicionColaborador datos)
    {
        _idEnEdicion = datos.Id;

        Codigo = datos.Codigo;
        Identidad = datos.Identidad ?? string.Empty;
        PrimerNombre = datos.PrimerNombre;
        SegundoNombre = datos.SegundoNombre ?? string.Empty;
        PrimerApellido = datos.PrimerApellido;
        SegundoApellido = datos.SegundoApellido ?? string.Empty;
        Telefono = datos.Telefono ?? string.Empty;
        Correo = datos.Correo ?? string.Empty;
        Direccion = datos.Direccion ?? string.Empty;

        // Un salario sin capturar deja el campo vacio, no "0.00": escribir cero
        // seria afirmar que la persona no gana nada (CR-04).
        Salario = datos.SalarioBase is { } sueldo
            ? sueldo.ToString("0.00", CultureInfo.CurrentCulture)
            : string.Empty;

        FechaNacimiento = datos.FechaNacimiento;
        FechaIngreso = datos.FechaIngreso == default ? DateTime.Today : datos.FechaIngreso;

        SexoSeleccionado = datos.Sexo is { } sexo
            ? Sexos.FirstOrDefault(s => s.Valor == (int)sexo) ?? Sexos[0]
            : Sexos[0];

        EstadoEdSeleccionado = EstadosEd.FirstOrDefault(e => e.Valor == (int)datos.Estado) ?? EstadosEd[0];

        // Los tres son independientes: ya no hay refiltrado ni descarte en cadena.
        SucursalEd = SucursalesEd.FirstOrDefault(s => s.Id == datos.SucursalId);
        DepartamentoEd = DepartamentosEd.FirstOrDefault(d => d.Id == datos.DepartamentoId);
        PuestoEd = PuestosEd.FirstOrDefault(p => p.Id == datos.PuestoId);
    }

    // ─── Validacion ─────────────────────────────────────────────────────────

    /// <summary>
    /// Validacion del formulario segun CR-04 y CR-07.
    ///
    /// Solo cuatro campos son obligatorios: codigo de expediente, primer nombre,
    /// primer apellido y fecha de ingreso. Todo lo demas se puede dejar en blanco
    /// y completarse despues; lo que se deja vacio viaja como NULO.
    ///
    /// Lo opcional se valida UNICAMENTE si trae algo. Un correo en blanco no se
    /// valida; un correo escrito, si. Esa es la diferencia que pedia el cliente:
    /// buena parte del personal no tiene correo y eso no puede frenar el alta.
    /// </summary>
    private (bool valido, DatosEdicionColaborador? datos, string error) ValidarFormulario()
    {
        // ─── Obligatorios ───────────────────────────────────────────────────

        if (string.IsNullOrWhiteSpace(Codigo))
        {
            return (false, null, "El código de expediente es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(PrimerNombre))
        {
            return (false, null, "El primer nombre es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(PrimerApellido))
        {
            return (false, null, "El primer apellido es obligatorio.");
        }

        if (FechaIngreso.Date > DateTime.Today)
        {
            return (false, null, "La fecha de ingreso no puede ser posterior a hoy.");
        }

        // ─── Opcionales: solo se validan si traen algo ──────────────────────

        decimal? salario = null;
        if (!string.IsNullOrWhiteSpace(Salario))
        {
            if (!decimal.TryParse(Salario, NumberStyles.Number, CultureInfo.CurrentCulture, out var importe)
                || importe < 0)
            {
                return (false, null, "El salario base debe ser un número valido y no negativo. "
                    + "Déjelo en blanco si todavía no lo tiene.");
            }

            salario = importe;
        }

        if (FechaNacimiento is { } nacimiento)
        {
            if (nacimiento.Date >= FechaIngreso.Date)
            {
                return (false, null, "La fecha de nacimiento debe ser anterior a la de ingreso.");
            }

            if (nacimiento.Date > DateTime.Today)
            {
                return (false, null, "La fecha de nacimiento no puede ser posterior a hoy.");
            }
        }

        if (!string.IsNullOrWhiteSpace(Correo) && !CorreoTieneFormaValida(Correo))
        {
            return (false, null, "El correo electronico no tiene un formato valido. "
                + "Déjelo en blanco si la persona no tiene correo.");
        }

        if (!string.IsNullOrWhiteSpace(Telefono) && !Telefono.Any(char.IsDigit))
        {
            return (false, null, "El teléfono debe contener al menos un número. "
                + "Déjelo en blanco si no lo tiene.");
        }

        var datos = new DatosEdicionColaborador
        {
            Id = _idEnEdicion,
            Codigo = Codigo,
            PrimerNombre = PrimerNombre,
            PrimerApellido = PrimerApellido,
            FechaIngreso = FechaIngreso,
            Estado = EstadoEdSeleccionado is { } estado
                ? (EstadoColaborador)estado.Valor
                : EstadoColaborador.Activo,

            // Lo opcional va tal cual; el servicio se encarga de convertir el
            // texto en blanco a nulo antes de guardar.
            Identidad = Identidad,
            SegundoNombre = SegundoNombre,
            SegundoApellido = SegundoApellido,

            // Valor cero en el desplegable significa "sin especificar" (CR-04).
            Sexo = SexoSeleccionado is { Valor: > 0 } sexo ? (Sexo)sexo.Valor : null,

            FechaNacimiento = FechaNacimiento,
            Telefono = Telefono,
            Correo = Correo,
            Direccion = Direccion,
            SalarioBase = salario,
            SucursalId = SucursalEd?.Id,
            DepartamentoId = DepartamentoEd?.Id,
            PuestoId = PuestoEd?.Id
        };

        return (true, datos, string.Empty);
    }

    /// <summary>
    /// Comprobacion de forma del correo, deliberadamente simple: algo, arroba,
    /// algo, punto, algo. No se persigue la exactitud del estandar —validar
    /// correos "bien" rechaza direcciones legitimas— sino atajar la errata obvia.
    /// </summary>
    private static bool CorreoTieneFormaValida(string correo)
    {
        var texto = correo.Trim();
        var arroba = texto.IndexOf('@');

        if (arroba <= 0 || arroba != texto.LastIndexOf('@') || arroba == texto.Length - 1)
        {
            return false;
        }

        var dominio = texto[(arroba + 1)..];
        var punto = dominio.IndexOf('.');

        return punto > 0
            && punto != dominio.Length - 1
            && !texto.Contains(' ');
    }

    /// <summary>Avisa y devuelve falso si el perfil no puede capturar.</summary>
    private async Task<bool> ExigirPermisoCapturaAsync()
    {
        if (PuedeCapturar)
        {
            return true;
        }

        Registro.LogWarning("El perfil {Perfil} intentó abrir la captura sin permiso.", _sesion.Perfil);
        await Dialogo.AvisarAsync(
            "Sin permiso",
            "Su perfil no tiene permiso para dar de alta ni editar expedientes.").ConfigureAwait(true);
        return false;
    }
}
