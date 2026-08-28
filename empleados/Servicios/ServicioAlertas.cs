using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioAlertas : IServicioAlertas
{
    /// <summary>Dias de anticipacion con que se avisa el vencimiento de un contrato.</summary>
    private const int DiasAvisoContrato = 60;

    /// <summary>Duracion del periodo de prueba en Honduras.</summary>
    private const int DiasPeriodoPrueba = 60;

    private readonly IDbContextFactory<ContextoSigem> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly ILogger<ServicioAlertas> _registro;

    public ServicioAlertas(
        IDbContextFactory<ContextoSigem> fabrica,
        IContextoEmpresa contextoEmpresa,
        ILogger<ServicioAlertas> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResultadoMotor> GenerarAsync(CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var hoy = DateTime.Today;
        var candidatos = new List<Aviso>();

        candidatos.AddRange(await CalcularVencimientosDeContrato(contexto, hoy, cancelacion).ConfigureAwait(false));
        candidatos.AddRange(await CalcularVencimientosDeDocumento(contexto, hoy, cancelacion).ConfigureAwait(false));
        candidatos.AddRange(await CalcularCumpleanos(contexto, hoy, cancelacion).ConfigureAwait(false));
        candidatos.AddRange(await CalcularAniversarios(contexto, hoy, cancelacion).ConfigureAwait(false));
        candidatos.AddRange(await CalcularFinDePeriodoDePrueba(contexto, hoy, cancelacion).ConfigureAwait(false));

        // La idempotencia se resuelve comparando contra las claves ya guardadas.
        // El indice unico (EmpresaId, ClaveIdempotencia) es la red de seguridad:
        // aunque dos corridas se solaparan, la base rechazaria el duplicado.
        var clavesExistentes = await contexto.Avisos
            .Select(a => a.ClaveIdempotencia)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var yaExistentes = new HashSet<string>(clavesExistentes, StringComparer.Ordinal);

        var nuevos = candidatos
            .GroupBy(a => a.ClaveIdempotencia, StringComparer.Ordinal)
            .Select(g => g.First())
            .Where(a => !yaExistentes.Contains(a.ClaveIdempotencia))
            .ToList();

        if (nuevos.Count > 0)
        {
            contexto.Avisos.AddRange(nuevos);
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }

        var pendientes = await contexto.Avisos
            .CountAsync(a => a.Estado == EstadoAviso.Pendiente, cancelacion)
            .ConfigureAwait(false);

        _registro.LogInformation(
            "Motor de alertas de {Empresa}: {Detectados} detectados, {Nuevos} nuevos, "
                + "{Repetidos} ya existian, {Pendientes} pendientes en total.",
            _contextoEmpresa.NombreEmpresaActiva, candidatos.Count, nuevos.Count,
            candidatos.Count - nuevos.Count, pendientes);

        return new ResultadoMotor(nuevos.Count, candidatos.Count - nuevos.Count, pendientes);
    }

    /// <summary>Contratos temporales que vencen dentro de la ventana de aviso.</summary>
    private async Task<List<Aviso>> CalcularVencimientosDeContrato(
        ContextoSigem contexto, DateTime hoy, CancellationToken cancelacion)
    {
        var limite = hoy.AddDays(DiasAvisoContrato);

        var contratos = await contexto.Contratos
            .Where(c => c.Vigente && c.FechaFin != null && c.FechaFin <= limite)
            .Select(c => new
            {
                c.ColaboradorId,
                c.Numero,
                Fin = c.FechaFin!.Value,
                Nombre = c.Colaborador!.PrimerNombre + " " + c.Colaborador.PrimerApellido
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return contratos.Select(c => Crear(
            TipoAviso.VencimientoContrato,
            c.ColaboradorId,
            "Contrato por vencer: " + c.Nombre,
            "El contrato " + c.Numero + " vence el " + c.Fin.ToString("dd/MM/yyyy") + ".",
            c.Fin,
            "contrato:" + c.Numero + ":" + c.Fin.ToString("yyyy-MM-dd"))).ToList();
    }

    /// <summary>Documentos que vencen dentro de los dias que define su tipo.</summary>
    private async Task<List<Aviso>> CalcularVencimientosDeDocumento(
        ContextoSigem contexto, DateTime hoy, CancellationToken cancelacion)
    {
        var documentos = await contexto.Documentos
            .Where(d => d.FechaVencimiento != null)
            .Select(d => new
            {
                d.Id,
                d.ColaboradorId,
                Vence = d.FechaVencimiento!.Value,
                Tipo = d.TipoDocumento!.Nombre,
                Dias = d.TipoDocumento.DiasAvisoAnticipado,
                Nombre = d.Colaborador!.PrimerNombre + " " + d.Colaborador.PrimerApellido
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return documentos
            .Where(d => d.Vence <= hoy.AddDays(d.Dias))
            .Select(d => Crear(
                TipoAviso.VencimientoDocumento,
                d.ColaboradorId,
                d.Tipo + " por vencer: " + d.Nombre,
                "El documento vence el " + d.Vence.ToString("dd/MM/yyyy") + ".",
                d.Vence,
                "documento:" + d.Id + ":" + d.Vence.ToString("yyyy-MM-dd")))
            .ToList();
    }

    /// <summary>Cumpleanos del mes en curso.</summary>
    private async Task<List<Aviso>> CalcularCumpleanos(
        ContextoSigem contexto, DateTime hoy, CancellationToken cancelacion)
    {
        var personas = await contexto.Colaboradores
            .Where(c => c.Estado == EstadoColaborador.Activo && c.FechaNacimiento.Month == hoy.Month)
            .Select(c => new
            {
                c.Id,
                c.FechaNacimiento,
                Nombre = c.PrimerNombre + " " + c.PrimerApellido
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return personas.Select(p =>
        {
            var dia = Math.Min(p.FechaNacimiento.Day, DateTime.DaysInMonth(hoy.Year, hoy.Month));
            var fecha = new DateTime(hoy.Year, hoy.Month, dia);

            return Crear(
                TipoAviso.Cumpleanos,
                p.Id,
                "Cumpleanos de " + p.Nombre,
                "Cumple anos el " + fecha.ToString("dd/MM") + ".",
                fecha,
                "cumple:" + p.Id + ":" + hoy.ToString("yyyy-MM"));
        }).ToList();
    }

    /// <summary>Aniversarios laborales del mes en curso.</summary>
    private async Task<List<Aviso>> CalcularAniversarios(
        ContextoSigem contexto, DateTime hoy, CancellationToken cancelacion)
    {
        var personas = await contexto.Colaboradores
            .Where(c => c.Estado == EstadoColaborador.Activo
                && c.FechaIngreso.Month == hoy.Month
                && c.FechaIngreso.Year < hoy.Year)
            .Select(c => new
            {
                c.Id,
                c.FechaIngreso,
                Nombre = c.PrimerNombre + " " + c.PrimerApellido
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return personas.Select(p =>
        {
            var dia = Math.Min(p.FechaIngreso.Day, DateTime.DaysInMonth(hoy.Year, hoy.Month));
            var fecha = new DateTime(hoy.Year, hoy.Month, dia);
            var anos = hoy.Year - p.FechaIngreso.Year;

            return Crear(
                TipoAviso.AniversarioLaboral,
                p.Id,
                "Aniversario laboral de " + p.Nombre,
                "Cumple " + anos + " anos en la empresa el " + fecha.ToString("dd/MM") + ".",
                fecha,
                "aniversario:" + p.Id + ":" + hoy.ToString("yyyy-MM"));
        }).ToList();
    }

    /// <summary>Colaboradores cuyo periodo de prueba termina pronto.</summary>
    private async Task<List<Aviso>> CalcularFinDePeriodoDePrueba(
        ContextoSigem contexto, DateTime hoy, CancellationToken cancelacion)
    {
        var desde = hoy.AddDays(-DiasPeriodoPrueba);

        var personas = await contexto.Colaboradores
            .Where(c => c.Estado == EstadoColaborador.Activo && c.FechaIngreso >= desde)
            .Select(c => new
            {
                c.Id,
                c.FechaIngreso,
                Nombre = c.PrimerNombre + " " + c.PrimerApellido
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return personas.Select(p =>
        {
            var fin = p.FechaIngreso.AddDays(DiasPeriodoPrueba);

            return Crear(
                TipoAviso.FinPeriodoPrueba,
                p.Id,
                "Fin de periodo de prueba: " + p.Nombre,
                "El periodo de prueba termina el " + fin.ToString("dd/MM/yyyy") + ".",
                fin,
                "prueba:" + p.Id + ":" + fin.ToString("yyyy-MM-dd"));
        }).ToList();
    }

    /// <summary>
    /// Arma el aviso. EmpresaId se asigna explicitamente porque al insertar no
    /// hay filtro global que lo ponga: el filtro solo actua al leer.
    /// </summary>
    private Aviso Crear(
        TipoAviso tipo, int colaboradorId, string titulo, string descripcion,
        DateTime referencia, string clave) => new()
    {
        EmpresaId = _contextoEmpresa.EmpresaActivaId,
        Tipo = tipo,
        Estado = EstadoAviso.Pendiente,
        ColaboradorId = colaboradorId,
        Titulo = titulo,
        Descripcion = descripcion,
        FechaGeneracion = DateTime.UtcNow,
        FechaReferencia = referencia,
        ClaveIdempotencia = clave,
        FechaCreacion = DateTime.UtcNow
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<LineaAviso>> ObtenerPendientesAsync(CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var avisos = await contexto.Avisos
            .Where(a => a.Estado == EstadoAviso.Pendiente)
            .OrderBy(a => a.FechaReferencia)
            .Select(a => new LineaAviso(
                a.Id,
                a.Tipo,
                a.Estado,
                a.Titulo,
                a.Descripcion,
                a.FechaReferencia,
                a.Colaborador == null
                    ? string.Empty
                    : a.Colaborador.PrimerNombre + " " + a.Colaborador.PrimerApellido))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return avisos;
    }

    /// <inheritdoc />
    public async Task ResolverAsync(int avisoId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var aviso = await contexto.Avisos.FirstOrDefaultAsync(a => a.Id == avisoId, cancelacion)
            .ConfigureAwait(false);

        if (aviso is null)
        {
            return;
        }

        aviso.Estado = EstadoAviso.Resuelto;
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        _registro.LogInformation("Aviso {Id} marcado como resuelto.", avisoId);
    }

    private void ExigirEmpresaActiva()
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException("El motor de alertas necesita una empresa activa.");
        }
    }
}
