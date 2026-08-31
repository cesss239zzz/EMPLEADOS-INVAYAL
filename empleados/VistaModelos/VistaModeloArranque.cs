using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using empleados.Servicios;
using Microsoft.Extensions.Logging;

namespace empleados.VistaModelos;

/// <summary>
/// Pantalla de arranque. Verifica la infraestructura antes de dejar entrar al
/// sistema (CLAUDE.md, regla 8) y deriva a diagnostico o al acceso segun el resultado.
/// </summary>
public sealed partial class VistaModeloArranque : VistaModeloBase
{
    private readonly IServicioDiagnostico _diagnostico;
    private readonly IServicioNavegacion _navegacion;
    private readonly EstadoAplicacion _estado;
    private readonly IServicioRespaldos _respaldos;

    public VistaModeloArranque(
        IServicioDiagnostico diagnostico,
        IServicioNavegacion navegacion,
        EstadoAplicacion estado,
        IServicioRespaldos respaldos,
        ILogger<VistaModeloArranque> registro,
        IServicioDialogo dialogo)
        : base(registro, dialogo)
    {
        _diagnostico = diagnostico;
        _navegacion = navegacion;
        _estado = estado;
        _respaldos = respaldos;

        Titulo = "RH Manager";
        Mensaje = "Iniciando RH Manager...";
    }

    /// <summary>Texto que ve el usuario mientras se verifica el entorno.</summary>
    [ObservableProperty]
    public partial string Mensaje { get; set; }

    /// <summary>Verifica la infraestructura y navega segun el resultado.</summary>
    [RelayCommand]
    private Task VerificarAsync()
        => EjecutarSeguroAsync(
            async () =>
            {
                EnHiloUi(() => Mensaje = "Verificando la conexión con la base de datos...");

                var resultado = await _diagnostico.VerificarAsync().ConfigureAwait(true);
                _estado.UltimoDiagnostico = resultado;

                if (resultado.EsCorrecto)
                {
                    EnHiloUi(() => Mensaje = "Conexión verificada.");

                    // El respaldo del día se toma con la base ya verificada y
                    // antes de que nadie empiece a escribir (CR-12).
                    await RespaldarElDiaAsync().ConfigureAwait(true);

                    await _navegacion.IrAsync(RutasNavegacion.Acceso).ConfigureAwait(true);
                    return;
                }

                EnHiloUi(() => Mensaje = resultado.Titulo);
                await _navegacion.IrAsync(RutasNavegacion.Diagnostico).ConfigureAwait(true);
            },
            "verificación de infraestructura al arrancar",
            "RH Manager no pudo completar la verificación inicial. El detalle quedó en el archivo de registro.");

    /// <summary>
    /// Respaldo automático del día y purga de los vencidos (CR-12).
    ///
    /// Lleva su propio try/catch a propósito: que no se pueda escribir el
    /// respaldo —un disco lleno, la carpeta Documentos redirigida a una unidad de
    /// red caída— es un problema real, pero no es motivo para dejar al usuario
    /// fuera del sistema. Queda anotado en el registro y la aplicación sigue.
    /// </summary>
    private async Task RespaldarElDiaAsync()
    {
        try
        {
            EnHiloUi(() => Mensaje = "Revisando la copia de seguridad del día...");

            var resultado = await _respaldos.RespaldarSiTocaAsync().ConfigureAwait(true);

            if (resultado is null)
            {
                Registro.LogInformation("El respaldo de hoy ya existía.");
            }
            else if (!resultado.Exito)
            {
                Registro.LogWarning("No se pudo crear el respaldo del día: {Error}", resultado.Error);
            }
        }
        catch (Exception ex)
        {
            Registro.LogError(ex, "Falló el respaldo automático del día. El arranque continúa.");
        }
    }
}
