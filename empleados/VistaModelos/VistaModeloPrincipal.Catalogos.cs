using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>Una pestana del apartado de catalogos.</summary>
/// <param name="Tipo">Catalogo que representa.</param>
/// <param name="Nombre">Texto de la pestana.</param>
/// <param name="Singular">Como se nombra un valor suelto, para titulos y mensajes.</param>
public sealed record PestanaCatalogo(TipoCatalogo Tipo, string Nombre, string Singular);

/// <summary>Unidad en que se expresa la vigencia de un tipo de documento.</summary>
/// <param name="Meses">Cuantos meses vale una unidad.</param>
/// <param name="Nombre">Texto del desplegable.</param>
public sealed record UnidadVigencia(int Meses, string Nombre);

/// <summary>
/// Parte de catalogos del contenedor principal: el apartado de Configuracion
/// donde el usuario crea, edita, activa y elimina los valores de todos los
/// desplegables del sistema (solicitud de cambios, CR-06).
///
/// Es la misma clase parcial que el nucleo del ViewModel: comparte
/// <c>EstaOcupado</c>, el hilo de interfaz y el patron protegido de
/// <see cref="VistaModeloBase"/>. El apartado es un panel mas, sin ruta de Shell
/// nueva.
/// </summary>
public sealed partial class VistaModeloPrincipal
{
    private readonly IServicioCatalogos _catalogos;

    /// <summary>Espera entre la ultima tecla y la consulta del buscador de catalogo.</summary>
    private static readonly TimeSpan EsperaAntesDeBuscarCatalogo = TimeSpan.FromMilliseconds(250);

    /// <summary>Cancela la busqueda pendiente cuando el usuario sigue escribiendo.</summary>
    private CancellationTokenSource? _cancelacionBusquedaCatalogo;

    /// <summary>Id del valor en edicion; cero mientras es un alta.</summary>
    private int _idCatalogoEnEdicion;

    // ─── Pestanas ───────────────────────────────────────────────────────────

    /// <summary>
    /// Los cinco catalogos que administra el usuario. El orden es el de la
    /// pantalla y sigue el de la solicitud de cambios.
    /// </summary>
    public IReadOnlyList<PestanaCatalogo> PestanasCatalogo { get; } =
    [
        new(TipoCatalogo.Departamento, "Departamentos", "departamento"),
        new(TipoCatalogo.Puesto, "Puestos", "puesto"),
        new(TipoCatalogo.Sucursal, "Sucursales", "sucursal"),
        new(TipoCatalogo.TipoDocumento, "Tipos de documento", "tipo de documento"),
        new(TipoCatalogo.TipoContrato, "Tipos de contrato", "tipo de contrato")
    ];

    /// <summary>Unidades en que se captura la vigencia. Se guarda siempre en meses.</summary>
    public IReadOnlyList<UnidadVigencia> UnidadesVigencia { get; } =
    [
        new UnidadVigencia(1, "meses"),
        new UnidadVigencia(12, "años")
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsCatalogoDepartamento))]
    [NotifyPropertyChangedFor(nameof(EsCatalogoPuesto))]
    [NotifyPropertyChangedFor(nameof(EsCatalogoSucursal))]
    [NotifyPropertyChangedFor(nameof(EsCatalogoTipoDocumento))]
    [NotifyPropertyChangedFor(nameof(EsCatalogoTipoContrato))]
    [NotifyPropertyChangedFor(nameof(TextoNuevoCatalogo))]
    [NotifyPropertyChangedFor(nameof(TextoVacioCatalogo))]
    public partial PestanaCatalogo? PestanaCatalogoActiva { get; set; }

    public bool EsCatalogoDepartamento => PestanaCatalogoActiva?.Tipo == TipoCatalogo.Departamento;
    public bool EsCatalogoPuesto => PestanaCatalogoActiva?.Tipo == TipoCatalogo.Puesto;
    public bool EsCatalogoSucursal => PestanaCatalogoActiva?.Tipo == TipoCatalogo.Sucursal;
    public bool EsCatalogoTipoDocumento => PestanaCatalogoActiva?.Tipo == TipoCatalogo.TipoDocumento;
    public bool EsCatalogoTipoContrato => PestanaCatalogoActiva?.Tipo == TipoCatalogo.TipoContrato;

    public string TextoNuevoCatalogo =>
        "+ Nuevo " + (PestanaCatalogoActiva?.Singular ?? "valor");

    public string TextoVacioCatalogo =>
        "Todavía no hay ningún " + (PestanaCatalogoActiva?.Singular ?? "valor")
        + " en esta empresa. Cree el primero para que aparezca en los desplegables del sistema.";

    // ─── Tabla ──────────────────────────────────────────────────────────────

    /// <summary>Valores del catalogo abierto.</summary>
    public ObservableCollection<FilaCatalogo> ValoresCatalogo { get; } = [];

    /// <summary>Buscador del catalogo.</summary>
    [ObservableProperty]
    public partial string BusquedaCatalogo { get; set; }

    /// <summary>Si la tabla muestra tambien los valores desactivados.</summary>
    [ObservableProperty]
    public partial bool MostrarInactivos { get; set; }

    /// <summary>Verdadero cuando el catalogo abierto no tiene ningun valor que mostrar.</summary>
    [ObservableProperty]
    public partial bool CatalogoVacio { get; set; }

    /// <summary>Texto de la cabecera: "4 valores".</summary>
    [ObservableProperty]
    public partial string ConteoCatalogo { get; set; }

    // ─── Formulario ─────────────────────────────────────────────────────────

    /// <summary>Verdadero mientras el formulario de catalogo esta abierto.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TablaCatalogoVisible))]
    public partial bool ModoFormularioCatalogo { get; set; }

    /// <summary>La tabla y el formulario se turnan.</summary>
    public bool TablaCatalogoVisible => !ModoFormularioCatalogo;

    [ObservableProperty]
    public partial string TituloFormularioCatalogo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayErrorCatalogo))]
    public partial string ErrorCatalogo { get; set; }

    public bool HayErrorCatalogo => !string.IsNullOrEmpty(ErrorCatalogo);

    [ObservableProperty] public partial string NombreCatalogo { get; set; }
    [ObservableProperty] public partial bool ActivoCatalogo { get; set; }

    // Sucursal
    [ObservableProperty] public partial string CodigoSucursal { get; set; }
    [ObservableProperty] public partial string DireccionSucursal { get; set; }
    [ObservableProperty] public partial string TelefonoSucursal { get; set; }

    // Puesto
    public ObservableCollection<OpcionCatalogo> DepartamentosCatalogo { get; } = [];

    [ObservableProperty] public partial OpcionCatalogo? DepartamentoCatalogo { get; set; }

    // Tipo de documento y tipo de contrato
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VigenciaHabilitada))]
    public partial bool RequiereVencimientoCatalogo { get; set; }

    /// <summary>La vigencia solo tiene sentido si el tipo vence (CR-10).</summary>
    public bool VigenciaHabilitada => RequiereVencimientoCatalogo;

    [ObservableProperty] public partial string VigenciaCatalogo { get; set; }
    [ObservableProperty] public partial UnidadVigencia? UnidadVigenciaCatalogo { get; set; }
    [ObservableProperty] public partial string DiasAvisoCatalogo { get; set; }

    /// <summary>
    /// Escalones de recordatorio, separados por coma: "30,15,5" (CR-10).
    /// Es el campo que hace que la anticipación deje de estar fija en el código.
    /// </summary>
    [ObservableProperty] public partial string EscalaAvisoCatalogo { get; set; }

    /// <summary>
    /// Valores fijos del apartado de catalogos. Se llama desde el constructor:
    /// ningun campo enlazado debe quedar en null. No consulta la base
    /// (CLAUDE.md, regla 13).
    /// </summary>
    private void InicializarCamposCatalogo()
    {
        PestanaCatalogoActiva = PestanasCatalogo[0];
        BusquedaCatalogo = string.Empty;
        MostrarInactivos = true;
        CatalogoVacio = false;
        ConteoCatalogo = string.Empty;

        TituloFormularioCatalogo = string.Empty;
        ErrorCatalogo = string.Empty;
        NombreCatalogo = string.Empty;
        ActivoCatalogo = true;

        CodigoSucursal = string.Empty;
        DireccionSucursal = string.Empty;
        TelefonoSucursal = string.Empty;

        RequiereVencimientoCatalogo = false;
        VigenciaCatalogo = string.Empty;
        UnidadVigenciaCatalogo = UnidadesVigencia[0];
        DiasAvisoCatalogo = "30";
        EscalaAvisoCatalogo = "30,15,5";
    }

    // ─── Reaccion a los controles ───────────────────────────────────────────

    /// <summary>Escribir en el buscador recarga con espera, no en cada tecla.</summary>
    partial void OnBusquedaCatalogoChanged(string value)
    {
        if (SeccionActiva != Seccion.Configuracion)
        {
            return;
        }

        _cancelacionBusquedaCatalogo?.Cancel();
        _cancelacionBusquedaCatalogo?.Dispose();
        _cancelacionBusquedaCatalogo = new CancellationTokenSource();
        var testigo = _cancelacionBusquedaCatalogo.Token;

        _ = BuscarEnCatalogoConEsperaAsync(testigo);
    }

    partial void OnMostrarInactivosChanged(bool value)
    {
        if (SeccionActiva == Seccion.Configuracion)
        {
            _ = RecargarCatalogoSeguroAsync();
        }
    }

    /// <summary>
    /// Espera a que el usuario deje de escribir y recarga. Es un metodo asincrono
    /// que devuelve Task, no async void: una excepcion aca no puede tumbar el
    /// proceso (CLAUDE.md, regla 3).
    /// </summary>
    private async Task BuscarEnCatalogoConEsperaAsync(CancellationToken cancelacion)
    {
        try
        {
            await Task.Delay(EsperaAntesDeBuscarCatalogo, cancelacion).ConfigureAwait(true);
            await CargarCatalogoAsync(cancelacion).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // El usuario siguio escribiendo. No es un error.
        }
        catch (Exception ex)
        {
            Registro.LogError(ex, "Falló la búsqueda en el catálogo.");
        }
    }

    /// <summary>Recarga el catalogo con la proteccion completa de la regla 4.</summary>
    private Task RecargarCatalogoSeguroAsync()
        => EjecutarSeguroAsync(
            () => CargarCatalogoAsync(CancellationToken.None),
            "recarga del catálogo",
            "No se pudo cargar el catálogo. El detalle quedó en el archivo de registro.");

    // ─── Carga ──────────────────────────────────────────────────────────────

    /// <summary>Trae los valores del catalogo abierto. La llama CargarSeccionAsync.</summary>
    private async Task CargarCatalogoAsync(CancellationToken cancelacion)
    {
        var tipo = PestanaCatalogoActiva?.Tipo ?? TipoCatalogo.Departamento;

        var valores = await _catalogos
            .ObtenerAsync(tipo, BusquedaCatalogo, MostrarInactivos, cancelacion)
            .ConfigureAwait(true);

        cancelacion.ThrowIfCancellationRequested();

        EnHiloUi(() =>
        {
            ValoresCatalogo.Clear();
            foreach (var valor in valores)
            {
                ValoresCatalogo.Add(valor);
            }

            CatalogoVacio = ValoresCatalogo.Count == 0;
            ConteoCatalogo = ValoresCatalogo.Count switch
            {
                0 => "Sin valores",
                1 => "1 valor",
                var n => n + " valores"
            };
        });
    }

    // ─── Comandos ───────────────────────────────────────────────────────────

    /// <summary>Cambia de catalogo desde las pestanas.</summary>
    [RelayCommand]
    private Task CambiarCatalogoAsync(PestanaCatalogo? pestana)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (pestana is null || pestana.Tipo == PestanaCatalogoActiva?.Tipo)
                {
                    return;
                }

                EnHiloUi(() =>
                {
                    PestanaCatalogoActiva = pestana;
                    ModoFormularioCatalogo = false;
                    ErrorCatalogo = string.Empty;
                    ValoresCatalogo.Clear();
                });

                await CargarCatalogoAsync(CancellationToken.None).ConfigureAwait(true);
            },
            "cambio al catálogo " + (pestana?.Nombre ?? "desconocido"),
            "No se pudo abrir ese catálogo. El detalle quedó en el archivo de registro.");

    /// <summary>Abre el formulario en blanco para crear un valor.</summary>
    [RelayCommand]
    private Task NuevoValorCatalogoAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (!await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                // El desplegable de departamentos solo hace falta en el catalogo
                // de puestos: no se consulta si no se va a mostrar (regla 13).
                if (EsCatalogoPuesto)
                {
                    await CargarDepartamentosCatalogoAsync().ConfigureAwait(true);
                }

                EnHiloUi(() =>
                {
                    _idCatalogoEnEdicion = 0;
                    LimpiarFormularioCatalogo();
                    TituloFormularioCatalogo =
                        "Nuevo " + (PestanaCatalogoActiva?.Singular ?? "valor");
                    ErrorCatalogo = string.Empty;
                    ModoFormularioCatalogo = true;
                });
            },
            "apertura del alta de catálogo",
            "No se pudo abrir el formulario. El detalle quedó en el archivo de registro.");

    /// <summary>Abre el formulario con un valor existente.</summary>
    [RelayCommand]
    private Task EditarValorCatalogoAsync(FilaCatalogo? fila)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (fila is null)
                {
                    return;
                }

                if (!await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                var tipo = PestanaCatalogoActiva?.Tipo ?? TipoCatalogo.Departamento;

                if (EsCatalogoPuesto)
                {
                    await CargarDepartamentosCatalogoAsync().ConfigureAwait(true);
                }

                var datos = await _catalogos.ObtenerParaEdicionAsync(tipo, fila.Id).ConfigureAwait(true);
                if (datos is null)
                {
                    await Dialogo.AvisarAsync(
                        "Valor no disponible",
                        "Ese valor ya no esta en el catálogo.").ConfigureAwait(true);
                    return;
                }

                EnHiloUi(() =>
                {
                    _idCatalogoEnEdicion = datos.Id;
                    CargarFormularioCatalogo(datos);
                    TituloFormularioCatalogo =
                        "Editar " + (PestanaCatalogoActiva?.Singular ?? "valor");
                    ErrorCatalogo = string.Empty;
                    ModoFormularioCatalogo = true;
                });
            },
            "apertura de la edición de un valor de catálogo",
            "No se pudo abrir el formulario. El detalle quedó en el archivo de registro.");

    /// <summary>Valida y guarda el formulario del catalogo.</summary>
    [RelayCommand]
    private Task GuardarValorCatalogoAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                var (valido, datos, error) = ValidarFormularioCatalogo();
                if (!valido || datos is null)
                {
                    EnHiloUi(() => ErrorCatalogo = error);
                    return;
                }

                var resultado = await _catalogos.GuardarAsync(datos).ConfigureAwait(true);
                if (!resultado.Exito)
                {
                    EnHiloUi(() => ErrorCatalogo = resultado.Error ?? "No se pudo guardar el valor.");
                    return;
                }

                EnHiloUi(() =>
                {
                    ErrorCatalogo = string.Empty;
                    ModoFormularioCatalogo = false;
                });

                await CargarCatalogoAsync(CancellationToken.None).ConfigureAwait(true);
            },
            "guardado de un valor de catálogo",
            "No se pudo guardar el valor. El detalle quedó en el archivo de registro.");

    /// <summary>Cierra el formulario sin guardar.</summary>
    [RelayCommand]
    private void CancelarFormularioCatalogo()
    {
        ModoFormularioCatalogo = false;
        ErrorCatalogo = string.Empty;
    }

    /// <summary>Activa o desactiva un valor sin borrarlo.</summary>
    [RelayCommand]
    private Task AlternarActivoCatalogoAsync(FilaCatalogo? fila)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (fila is null)
                {
                    return;
                }

                if (!await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                var tipo = PestanaCatalogoActiva?.Tipo ?? TipoCatalogo.Departamento;

                var resultado = await _catalogos
                    .CambiarActivoAsync(tipo, fila.Id, !fila.Activo)
                    .ConfigureAwait(true);

                if (!resultado.Exito)
                {
                    await Dialogo.AvisarAsync(
                        "No se pudo cambiar",
                        resultado.Error ?? "No se pudo cambiar el estado del valor.").ConfigureAwait(true);
                    return;
                }

                await CargarCatalogoAsync(CancellationToken.None).ConfigureAwait(true);
            },
            "cambio de estado de un valor de catálogo",
            "No se pudo cambiar el estado. El detalle quedó en el archivo de registro.");

    /// <summary>
    /// Elimina un valor. Si algun registro lo usa, el servicio lo rechaza y aca
    /// se le ofrece al usuario desactivarlo, que es lo que de verdad quiere hacer.
    /// </summary>
    [RelayCommand]
    private Task EliminarValorCatalogoAsync(FilaCatalogo? fila)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (fila is null)
                {
                    return;
                }

                if (!await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                var tipo = PestanaCatalogoActiva?.Tipo ?? TipoCatalogo.Departamento;

                if (fila.EnUso > 0)
                {
                    var desactivar = await Dialogo.ConfirmarAsync(
                        "No se puede eliminar",
                        "\"" + fila.Nombre + "\" lo usan " + fila.EnUso + " registro(s), así que no se "
                            + "puede borrar sin romper esos expedientes."
                            + Environment.NewLine + Environment.NewLine
                            + "¿Desea desactivarlo? Dejara de ofrecerse en nuevas selecciones, pero los "
                            + "expedientes que ya lo tienen lo seguiran mostrando.",
                        "Desactivar", "Cancelar").ConfigureAwait(true);

                    if (!desactivar)
                    {
                        return;
                    }

                    var apagado = await _catalogos
                        .CambiarActivoAsync(tipo, fila.Id, false).ConfigureAwait(true);

                    if (!apagado.Exito)
                    {
                        await Dialogo.AvisarAsync(
                            "No se pudo desactivar",
                            apagado.Error ?? "No se pudo desactivar el valor.").ConfigureAwait(true);
                        return;
                    }

                    await CargarCatalogoAsync(CancellationToken.None).ConfigureAwait(true);
                    return;
                }

                var confirmar = await Dialogo.ConfirmarAsync(
                    "Eliminar valor",
                    "¿Eliminar \"" + fila.Nombre + "\" del catálogo? No lo usa ningún registro, "
                        + "así que se puede borrar sin consecuencias.",
                    "Eliminar", "Cancelar").ConfigureAwait(true);

                if (!confirmar)
                {
                    return;
                }

                var resultado = await _catalogos.EliminarAsync(tipo, fila.Id).ConfigureAwait(true);

                if (!resultado.Exito)
                {
                    await Dialogo.AvisarAsync(
                        "No se pudo eliminar",
                        resultado.Error ?? "No se pudo eliminar el valor.").ConfigureAwait(true);
                    return;
                }

                await CargarCatalogoAsync(CancellationToken.None).ConfigureAwait(true);
            },
            "eliminación de un valor de catálogo",
            "No se pudo eliminar el valor. El detalle quedó en el archivo de registro.");

    // ─── Auxiliares ─────────────────────────────────────────────────────────

    private async Task CargarDepartamentosCatalogoAsync()
    {
        var departamentos = await _catalogos.ObtenerDepartamentosActivosAsync().ConfigureAwait(true);

        EnHiloUi(() =>
        {
            DepartamentosCatalogo.Clear();
            foreach (var departamento in departamentos)
            {
                DepartamentosCatalogo.Add(departamento);
            }
        });
    }

    private void LimpiarFormularioCatalogo()
    {
        NombreCatalogo = string.Empty;
        ActivoCatalogo = true;
        CodigoSucursal = string.Empty;
        DireccionSucursal = string.Empty;
        TelefonoSucursal = string.Empty;
        DepartamentoCatalogo = null;
        RequiereVencimientoCatalogo = false;
        VigenciaCatalogo = string.Empty;
        UnidadVigenciaCatalogo = UnidadesVigencia[0];
        DiasAvisoCatalogo = "30";
        EscalaAvisoCatalogo = "30,15,5";
    }

    private void CargarFormularioCatalogo(DatosCatalogo datos)
    {
        NombreCatalogo = datos.Nombre;
        ActivoCatalogo = datos.Activo;

        CodigoSucursal = datos.Codigo;
        DireccionSucursal = datos.Direccion;
        TelefonoSucursal = datos.Telefono;

        DepartamentoCatalogo = datos.DepartamentoId is { } id
            ? DepartamentosCatalogo.FirstOrDefault(d => d.Id == id)
            : null;

        RequiereVencimientoCatalogo = datos.RequiereVencimiento;
        DiasAvisoCatalogo = datos.DiasAvisoAnticipado.ToString(CultureInfo.CurrentCulture);
        EscalaAvisoCatalogo = datos.EscalaAviso;

        // La vigencia se guarda en meses; se muestra en años cuando es multiplo
        // exacto de doce, que es como la piensa el usuario.
        if (datos.MesesVigencia is { } meses && meses > 0)
        {
            if (meses % 12 == 0)
            {
                UnidadVigenciaCatalogo = UnidadesVigencia[1];
                VigenciaCatalogo = (meses / 12).ToString(CultureInfo.CurrentCulture);
            }
            else
            {
                UnidadVigenciaCatalogo = UnidadesVigencia[0];
                VigenciaCatalogo = meses.ToString(CultureInfo.CurrentCulture);
            }
        }
        else
        {
            UnidadVigenciaCatalogo = UnidadesVigencia[0];
            VigenciaCatalogo = string.Empty;
        }
    }

    private (bool valido, DatosCatalogo? datos, string error) ValidarFormularioCatalogo()
    {
        var tipo = PestanaCatalogoActiva?.Tipo ?? TipoCatalogo.Departamento;

        if (string.IsNullOrWhiteSpace(NombreCatalogo))
        {
            return (false, null, "El nombre es obligatorio.");
        }

        var datos = new DatosCatalogo
        {
            Tipo = tipo,
            Id = _idCatalogoEnEdicion,
            Nombre = NombreCatalogo,
            Activo = ActivoCatalogo
        };

        if (tipo == TipoCatalogo.Sucursal)
        {
            if (string.IsNullOrWhiteSpace(CodigoSucursal))
            {
                return (false, null, "El código de la sucursal es obligatorio. Sirve para distinguirla "
                    + "en listados y reportes; por ejemplo CM para Casa Matriz.");
            }

            if (CodigoSucursal.Trim().Length > 20)
            {
                return (false, null, "El código no puede pasar de 20 caracteres.");
            }

            datos.Codigo = CodigoSucursal;
            datos.Direccion = DireccionSucursal;
            datos.Telefono = TelefonoSucursal;
        }

        if (tipo == TipoCatalogo.Puesto)
        {
            // El departamento es opcional a proposito (CR-05).
            datos.DepartamentoId = DepartamentoCatalogo?.Id;
        }

        if (tipo is TipoCatalogo.TipoDocumento or TipoCatalogo.TipoContrato)
        {
            datos.RequiereVencimiento = RequiereVencimientoCatalogo;
        }

        if (tipo == TipoCatalogo.TipoDocumento && RequiereVencimientoCatalogo)
        {
            if (!string.IsNullOrWhiteSpace(VigenciaCatalogo))
            {
                if (!int.TryParse(VigenciaCatalogo, NumberStyles.Integer, CultureInfo.CurrentCulture, out var cantidad)
                    || cantidad <= 0)
                {
                    return (false, null, "La vigencia debe ser un número entero mayor que cero.");
                }

                var factor = UnidadVigenciaCatalogo?.Meses ?? 1;
                var meses = cantidad * factor;

                if (meses > 1200)
                {
                    return (false, null, "La vigencia no puede pasar de 100 años.");
                }

                datos.MesesVigencia = meses;
            }

            if (!int.TryParse(DiasAvisoCatalogo, NumberStyles.Integer, CultureInfo.CurrentCulture, out var dias)
                || dias <= 0)
            {
                return (false, null, "Los días de aviso deben ser un número entero mayor que cero.");
            }

            if (dias > 365)
            {
                return (false, null, "Los días de aviso no pueden pasar de 365.");
            }

            datos.DiasAvisoAnticipado = dias;
            datos.EscalaAviso = EscalaAvisoCatalogo;
        }

        return (true, datos, string.Empty);
    }
}
