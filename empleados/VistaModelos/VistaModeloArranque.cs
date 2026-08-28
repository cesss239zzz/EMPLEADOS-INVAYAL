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

    public VistaModeloArranque(
        IServicioDiagnostico diagnostico,
        IServicioNavegacion navegacion,
        EstadoAplicacion estado,
        ILogger<VistaModeloArranque> registro,
        IServicioDialogo dialogo)
        : base(registro, dialogo)
    {
        _diagnostico = diagnostico;
        _navegacion = navegacion;
        _estado = estado;

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
                EnHiloUi(() => Mensaje = "Verificando la conexion con la base de datos...");

                var resultado = await _diagnostico.VerificarAsync().ConfigureAwait(true);
                _estado.UltimoDiagnostico = resultado;

                if (resultado.EsCorrecto)
                {
                    EnHiloUi(() => Mensaje = "Conexion verificada.");
                    await _navegacion.IrAsync(RutasNavegacion.Acceso).ConfigureAwait(true);
                    return;
                }

                EnHiloUi(() => Mensaje = resultado.Titulo);
                await _navegacion.IrAsync(RutasNavegacion.Diagnostico).ConfigureAwait(true);
            },
            "verificacion de infraestructura al arrancar",
            "RH Manager no pudo completar la verificacion inicial. El detalle quedo en el archivo de registro.");
}
