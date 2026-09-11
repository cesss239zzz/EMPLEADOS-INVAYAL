using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>Las dos mitades del apartado de Configuración.</summary>
public enum SubseccionConfiguracion
{
    Catalogos = 1,
    Respaldos = 2
}

/// <summary>
/// Parte de respaldos del contenedor principal (solicitud de cambios, CR-12).
///
/// Vive dentro de Configuración, junto a los catálogos. Ofrece lo que pidió el
/// cliente: respaldar ahora, restaurar un respaldo y ver los que hay, con la
/// política de conservar treinta días.
/// </summary>
public sealed partial class VistaModeloPrincipal
{
    private readonly IServicioRespaldos _respaldos;

    // ─── Subsección de Configuración ────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsSubCatalogos))]
    [NotifyPropertyChangedFor(nameof(EsSubRespaldos))]
    public partial SubseccionConfiguracion SubseccionActiva { get; set; }

    public bool EsSubCatalogos => SubseccionActiva == SubseccionConfiguracion.Catalogos;
    public bool EsSubRespaldos => SubseccionActiva == SubseccionConfiguracion.Respaldos;

    // ─── Estado de la pantalla ──────────────────────────────────────────────

    /// <summary>Respaldos disponibles, del más reciente al más antiguo.</summary>
    public ObservableCollection<LineaRespaldo> Respaldos { get; } = [];

    [ObservableProperty]
    public partial bool SinRespaldos { get; set; }

    /// <summary>Carpeta donde se guardan, tal como se le muestra al usuario.</summary>
    [ObservableProperty]
    public partial string CarpetaRespaldos { get; set; }

    /// <summary>Explicación de la política de retención.</summary>
    [ObservableProperty]
    public partial string PoliticaRespaldos { get; set; }

    [ObservableProperty]
    public partial string ResumenRespaldos { get; set; }

    /// <summary>
    /// Valores fijos del apartado. Se llama desde el constructor y no consulta
    /// nada (CLAUDE.md, regla 13).
    /// </summary>
    private void InicializarCamposRespaldo()
    {
        SubseccionActiva = SubseccionConfiguracion.Catalogos;
        SinRespaldos = false;
        CarpetaRespaldos = _respaldos.Carpeta;
        PoliticaRespaldos =
            "RH Manager guarda una copia de la base al abrirse cada día y conserva las de los "
            + "últimos " + _respaldos.DiasRetencion + " días. Nunca borra la última que quede.";
        ResumenRespaldos = string.Empty;
    }

    // ─── Comandos ───────────────────────────────────────────────────────────

    /// <summary>Cambia entre Catálogos y Respaldos dentro de Configuración.</summary>
    [RelayCommand]
    private Task CambiarSubseccionAsync(string? nombre)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (!Enum.TryParse<SubseccionConfiguracion>(nombre, ignoreCase: true, out var sub))
                {
                    Registro.LogWarning("Subsección desconocida de Configuración: {Nombre}",
                        nombre ?? "(nula)");
                    return;
                }

                EnHiloUi(() => SubseccionActiva = sub);

                if (sub == SubseccionConfiguracion.Respaldos)
                {
                    await CargarRespaldosAsync(CancellationToken.None).ConfigureAwait(true);
                }
                else
                {
                    await CargarCatalogoAsync(CancellationToken.None).ConfigureAwait(true);
                }
            },
            "cambio de subsección de configuración",
            "No se pudo abrir ese apartado. El detalle quedó en el archivo de registro.");

    /// <summary>Trae la lista de respaldos. La llama CargarSeccionAsync.</summary>
    private async Task CargarRespaldosAsync(CancellationToken cancelacion)
    {
        if (!_sesion.PuedeElegirEmpresa)
        {
            await Dialogo.AvisarAsync("Acceso restringido", "Los respaldos contienen todas las empresas. Solo el SuperAdministrador puede abrir este apartado.").ConfigureAwait(true);
            EnHiloUi(() => SubseccionActiva = SubseccionConfiguracion.Catalogos);
            return;
        }
        var lista = await _respaldos.ListarAsync(cancelacion).ConfigureAwait(true);

        cancelacion.ThrowIfCancellationRequested();

        EnHiloUi(() =>
        {
            Respaldos.Clear();
            foreach (var r in lista)
            {
                Respaldos.Add(r);
            }

            SinRespaldos = Respaldos.Count == 0;
            CarpetaRespaldos = _respaldos.Carpeta;

            ResumenRespaldos = Respaldos.Count switch
            {
                0 => "Todavía no hay ningún respaldo.",
                1 => "1 respaldo guardado.",
                var n => n + " respaldos guardados."
            };
        });
    }

    /// <summary>Crea un respaldo en el momento.</summary>
    [RelayCommand]
    private Task RespaldarAhoraAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                var resultado = await _respaldos.RespaldarAsync(MotivoRespaldo.Manual)
                    .ConfigureAwait(true);

                if (!resultado.Exito)
                {
                    await Dialogo.AvisarAsync(
                        "No se pudo respaldar",
                        resultado.Error ?? "No se pudo crear el respaldo.").ConfigureAwait(true);
                    return;
                }

                await CargarRespaldosAsync(CancellationToken.None).ConfigureAwait(true);

                await Dialogo.AvisarAsync(
                    "Respaldo creado",
                    "La copia quedó guardada en:" + Environment.NewLine + Environment.NewLine
                        + resultado.Archivo).ConfigureAwait(true);
            },
            "creación de un respaldo manual",
            "No se pudo crear el respaldo. El detalle quedó en el archivo de registro.");

    /// <summary>
    /// Restaura un respaldo. Es la operación más destructiva del sistema —pisa
    /// TODA la información actual— así que se explica con detalle y se pide
    /// confirmación escrita, igual que al eliminar una empresa.
    /// </summary>
    [RelayCommand]
    private Task RestaurarRespaldoAsync(LineaRespaldo? respaldo)
        => EjecutarSeguroAsync(
            async () =>
            {
                if (respaldo is null)
                {
                    return;
                }

                if (!await ExigirPermisoCapturaAsync().ConfigureAwait(true))
                {
                    return;
                }

                var escrito = await Dialogo.PedirTextoAsync(
                    "Restaurar respaldo",
                    "Va a reemplazar TODA la información actual por la del respaldo del "
                        + respaldo.MomentoTexto + "."
                        + Environment.NewLine + Environment.NewLine
                        + "Todo lo que se haya cargado después de esa fecha se perderá."
                        + Environment.NewLine + Environment.NewLine
                        + "Antes de reemplazar nada se guarda una copia del estado actual, así que "
                        + "esto se puede deshacer volviendo a restaurar esa copia."
                        + Environment.NewLine + Environment.NewLine
                        + "Para confirmar, escriba: RESTAURAR",
                    "RESTAURAR",
                    "Restaurar",
                    "Cancelar").ConfigureAwait(true);

                if (escrito is null)
                {
                    return;
                }

                if (!string.Equals(escrito.Trim(), "RESTAURAR", StringComparison.OrdinalIgnoreCase))
                {
                    await Dialogo.AvisarAsync(
                        "No se restauró nada",
                        "El texto no coincide con RESTAURAR. Su información quedó intacta.")
                        .ConfigureAwait(true);
                    return;
                }

                var resultado = await _respaldos.RestaurarAsync(respaldo.Archivo).ConfigureAwait(true);

                if (!resultado.Exito)
                {
                    await Dialogo.AvisarAsync(
                        "No se pudo restaurar",
                        resultado.Error ?? "No se pudo restaurar el respaldo.").ConfigureAwait(true);
                    return;
                }

                // La aplicación tiene el modelo y las conexiones apuntando al
                // archivo anterior. Seguir trabajando encima seria pedir un fallo
                // intermitente e imposible de explicar: se cierra y se vuelve a abrir.
                await Dialogo.AvisarAsync(
                    "Respaldo restaurado",
                    "La información del " + respaldo.MomentoTexto + " quedó restaurada."
                        + Environment.NewLine + Environment.NewLine
                        + "RH Manager se va a cerrar. Vuelva a abrirlo para trabajar con "
                        + "los datos restaurados.",
                    "Cerrar ahora").ConfigureAwait(true);

                Registro.LogWarning("Cierre de la aplicación tras restaurar {Archivo}.", respaldo.Archivo);
                EnHiloUi(() => Application.Current?.Quit());
            },
            "restauración de un respaldo",
            "No se pudo restaurar el respaldo. El detalle quedó en el archivo de registro.");

    /// <summary>Abre la carpeta de respaldos en el explorador de Windows.</summary>
    [RelayCommand]
    private Task AbrirCarpetaRespaldosAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                if (!_sesion.PuedeElegirEmpresa)
                {
                    await Dialogo.AvisarAsync("Acceso restringido", "Solo el SuperAdministrador puede abrir los respaldos.").ConfigureAwait(true);
                    return;
                }
                Directory.CreateDirectory(_respaldos.Carpeta);
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(_respaldos.Carpeta)
                }).ConfigureAwait(true);
            },
            "apertura de la carpeta de respaldos",
            "No se pudo abrir la carpeta. Está en: " + _respaldos.Carpeta);
}
