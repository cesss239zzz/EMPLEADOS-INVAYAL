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

    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly ILogger<ServicioAlertas> _registro;

    public ServicioAlertas(
        IDbContextFactory<ContextoRhManager> fabrica,
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
        ContextoRhManager contexto, DateTime hoy, CancellationToken cancelacion)
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

    /// <summary>
    /// Documentos por vencer y ya vencidos (solicitud de cambios, CR-10).
    ///
    /// La anticipación NO está fija en el código: sale de la escala configurada
    /// en el tipo de documento ("30,15,5"). Se genera un aviso por cada escalón
    /// alcanzado, cada uno con su propia clave de idempotencia, así que el
    /// recordatorio se repite al acercarse la fecha sin duplicar el anterior.
    ///
    /// Lo ya vencido genera además su propio aviso, que es el que sale en rojo:
    /// un documento vencido no deja de importar porque pasó su último escalón.
    /// </summary>
    private async Task<List<Aviso>> CalcularVencimientosDeDocumento(
        ContextoRhManager contexto, DateTime hoy, CancellationToken cancelacion)
    {
        var documentos = await contexto.Documentos
            .Where(d => d.FechaVencimiento != null)
            .Select(d => new
            {
                d.Id,
                d.ColaboradorId,
                Vence = d.FechaVencimiento!.Value,
                Tipo = d.TipoDocumento!.Nombre,
                Escala = d.TipoDocumento.EscalaAviso,
                DiasBase = d.TipoDocumento.DiasAvisoAnticipado,
                Nombre = d.Colaborador!.PrimerNombre + " " + d.Colaborador.PrimerApellido
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var avisos = new List<Aviso>();

        foreach (var d in documentos)
        {
            var restantes = (int)(d.Vence.Date - hoy.Date).TotalDays;
            var fechaTexto = d.Vence.ToString("dd/MM/yyyy");

            if (restantes < 0)
            {
                avisos.Add(Crear(
                    TipoAviso.VencimientoDocumento,
                    d.ColaboradorId,
                    d.Tipo + " VENCIDO: " + d.Nombre,
                    "Venció el " + fechaTexto + ", hace " + Math.Abs(restantes) + " día(s).",
                    d.Vence,
                    "documento:" + d.Id + ":" + d.Vence.ToString("yyyy-MM-dd") + ":vencido"));

                continue;
            }

            // La escala se reconstruye acá: el tipo ya no está adjunto a la
            // entidad porque la consulta se proyectó a un tipo anónimo.
            var escalones = new TipoDocumento
            {
                EscalaAviso = d.Escala,
                DiasAvisoAnticipado = d.DiasBase
            }.DiasDeAviso();

            // Solo el escalón más ajustado que ya se alcanzó. Sin esto, un
            // documento a 3 días generaría de golpe los avisos de 30, 15 y 5.
            var alcanzado = escalones.Where(e => restantes <= e).OrderBy(e => e).FirstOrDefault();

            if (alcanzado == 0)
            {
                continue;
            }

            avisos.Add(Crear(
                TipoAviso.VencimientoDocumento,
                d.ColaboradorId,
                d.Tipo + " por vencer: " + d.Nombre,
                "Vence el " + fechaTexto + ", en " + restantes + " día(s).",
                d.Vence,
                "documento:" + d.Id + ":" + d.Vence.ToString("yyyy-MM-dd") + ":" + alcanzado));
        }

        return avisos;
    }

    /// <summary>Cumpleanos del mes en curso.</summary>
    private async Task<List<Aviso>> CalcularCumpleanos(
        ContextoRhManager contexto, DateTime hoy, CancellationToken cancelacion)
    {
        // La fecha de nacimiento es opcional (CR-04): a quien no la tenga
        // capturada simplemente no se le felicita, en vez de reventar el motor.
        var personas = await contexto.Colaboradores
            .Where(c => c.Estado == EstadoColaborador.Activo
                && c.FechaNacimiento != null
                && c.FechaNacimiento.Value.Month == hoy.Month)
            .Select(c => new
            {
                c.Id,
                Nacimiento = c.FechaNacimiento!.Value,
                Nombre = c.PrimerNombre + " " + c.PrimerApellido
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return personas.Select(p =>
        {
            var dia = Math.Min(p.Nacimiento.Day, DateTime.DaysInMonth(hoy.Year, hoy.Month));
            var fecha = new DateTime(hoy.Year, hoy.Month, dia);

            return Crear(
                TipoAviso.Cumpleanos,
                p.Id,
                "Cumpleaños de " + p.Nombre,
                "Cumple años el " + fecha.ToString("dd/MM") + ".",
                fecha,
                "cumple:" + p.Id + ":" + hoy.ToString("yyyy-MM"));
        }).ToList();
    }

    /// <summary>Aniversarios laborales del mes en curso.</summary>
    private async Task<List<Aviso>> CalcularAniversarios(
        ContextoRhManager contexto, DateTime hoy, CancellationToken cancelacion)
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
            var años = hoy.Year - p.FechaIngreso.Year;

            return Crear(
                TipoAviso.AniversarioLaboral,
                p.Id,
                "Aniversario laboral de " + p.Nombre,
                "Cumple " + años + (años == 1 ? " año" : " años")
                    + " en la empresa el " + fecha.ToString("dd/MM") + ".",
                fecha,
                "aniversario:" + p.Id + ":" + hoy.ToString("yyyy-MM"));
        }).ToList();
    }

    /// <summary>Colaboradores cuyo periodo de prueba termina pronto.</summary>
    private async Task<List<Aviso>> CalcularFinDePeriodoDePrueba(
        ContextoRhManager contexto, DateTime hoy, CancellationToken cancelacion)
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
    public async Task<int> ContarPendientesAsync(CancellationToken cancelacion = default)
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            return 0;
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        return await contexto.Avisos
            .CountAsync(a => a.Estado == EstadoAviso.Pendiente, cancelacion)
            .ConfigureAwait(false);
    }

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
                    : a.Colaborador.PrimerNombre + " " + a.Colaborador.PrimerApellido,
                a.FechaGeneracion))
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
