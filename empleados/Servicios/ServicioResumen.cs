using System.Globalization;
using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioResumen : IServicioResumen
{
    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly ILogger<ServicioResumen> _registro;

    public ServicioResumen(
        IDbContextFactory<ContextoRhManager> fabrica,
        IContextoEmpresa contextoEmpresa,
        ILogger<ServicioResumen> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResumenGeneral> ObtenerAsync(CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var hoy = DateTime.Today;
        var limite = hoy.AddDays(ResumenGeneral.DiasDeVentana + 1);

        var activos = await contexto.Colaboradores
            .CountAsync(c => c.Estado == EstadoColaborador.Activo, cancelacion)
            .ConfigureAwait(false);

        var registrados = await contexto.Colaboradores
            .CountAsync(cancelacion)
            .ConfigureAwait(false);

        // El mes se compara en SQL: SQLite lo resuelve con strftime, no hay que
        // traer las fechas de nacimiento para filtrarlas aca.
        var cumpleanos = await contexto.Colaboradores
            .CountAsync(
                c => c.Estado == EstadoColaborador.Activo
                    && c.FechaNacimiento != null
                    && c.FechaNacimiento.Value.Month == hoy.Month,
                cancelacion)
            .ConfigureAwait(false);

        var documentos = await contexto.Documentos
            .CountAsync(
                d => d.FechaVencimiento != null
                    && d.FechaVencimiento >= hoy
                    && d.FechaVencimiento < limite,
                cancelacion)
            .ConfigureAwait(false);

        var contratos = await contexto.Contratos
            .CountAsync(
                c => c.Vigente
                    && c.FechaFin != null
                    && c.FechaFin >= hoy
                    && c.FechaFin < limite,
                cancelacion)
            .ConfigureAwait(false);

        var documentosVencidos = await contexto.Documentos
            .CountAsync(d => d.FechaVencimiento != null && d.FechaVencimiento < hoy, cancelacion)
            .ConfigureAwait(false);
        var contratosVencidos = await contexto.Contratos
            .CountAsync(c => c.Vigente && c.FechaFin != null && c.FechaFin < hoy, cancelacion)
            .ConfigureAwait(false);

        var proximo = await CalcularProximoCumpleanosAsync(contexto, hoy, cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Resumen de {Empresa}: {Activos} activos, {Cumpleanos} cumpleaños del mes, "
                + "{Documentos} documentos y {Contratos} contratos por vencer en {Dias} días.",
            _contextoEmpresa.NombreEmpresaActiva, activos, cumpleanos, documentos, contratos,
            ResumenGeneral.DiasDeVentana);

        return new ResumenGeneral(activos, registrados, cumpleanos, proximo, documentos, contratos)
        {
            DocumentosVencidos = documentosVencidos,
            ContratosVencidos = contratosVencidos
        };
    }

    /// <summary>
    /// El cumpleanos mas cercano que queda por celebrarse este mes. Se pide una
    /// sola fila con FirstOrDefault: el orden y el recorte los hace la base.
    /// </summary>
    private static async Task<string> CalcularProximoCumpleanosAsync(
        ContextoRhManager contexto, DateTime hoy, CancellationToken cancelacion)
    {
        var siguiente = await contexto.Colaboradores
            .Where(c => c.Estado == EstadoColaborador.Activo
                && c.FechaNacimiento != null
                && c.FechaNacimiento.Value.Month == hoy.Month
                && c.FechaNacimiento.Value.Day >= hoy.Day)
            .OrderBy(c => c.FechaNacimiento!.Value.Day)
            .Select(c => new { Dia = c.FechaNacimiento!.Value.Day, c.PrimerNombre })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (siguiente is null)
        {
            return string.Empty;
        }

        // La misma convención que el motor: 29/02 se celebra el 28 en años no bisiestos.
        var dia = Math.Min(siguiente.Dia, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        var cuando = (dia - hoy.Day) switch
        {
            0 => "hoy",
            1 => "mañana",
            _ => "el " + dia + " de "
                + CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(hoy.Month).ToLowerInvariant()
        };

        return siguiente.PrimerNombre + ", " + cuando;
    }

    private void ExigirEmpresaActiva()
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException(
                "No se puede calcular el resumen sin empresa activa.");
        }
    }
}
