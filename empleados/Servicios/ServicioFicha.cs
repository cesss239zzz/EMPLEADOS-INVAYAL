using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioFicha : IServicioFicha
{
    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioFicha> _registro;

    public ServicioFicha(
        IDbContextFactory<ContextoRhManager> fabrica,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        ILogger<ServicioFicha> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<DetalleColaborador?> ObtenerAsync(
        int colaboradorId,
        CancellationToken cancelacion = default)
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException("No se puede abrir una ficha sin empresa activa.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var puedeVerSalario = _sesion.PuedeVerSalarios;

        // Sin Where por EmpresaId: si el identificador fuera de otra empresa, el
        // filtro global simplemente no lo encuentra. Ese es el punto.
        var cabecera = await contexto.Colaboradores
            .Where(c => c.Id == colaboradorId)
            .Select(c => new
            {
                c.Id,
                c.Codigo,
                c.PrimerNombre,
                c.SegundoNombre,
                c.PrimerApellido,
                c.SegundoApellido,
                c.Identidad,
                c.Sexo,
                c.FechaNacimiento,
                c.FechaIngreso,
                c.FechaSalida,
                c.Estado,
                c.Telefono,
                c.Correo,
                c.Direccion,
                // Union externa: los tres catalogos son opcionales (CR-05).
                Puesto = c.Puesto == null ? null : c.Puesto.Nombre,
                Departamento = c.Departamento == null ? null : c.Departamento.Nombre,
                Sucursal = c.Sucursal == null ? null : c.Sucursal.Nombre,
                Salario = puedeVerSalario ? c.SalarioBase : null
            })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (cabecera is null)
        {
            _registro.LogWarning("Se pidió la ficha {Id}, que no existe en la empresa activa.", colaboradorId);
            return null;
        }

        var contactos = await contexto.ContactosEmergencia
            .Where(x => x.ColaboradorId == colaboradorId)
            .OrderByDescending(x => x.EsPrincipal).ThenBy(x => x.Nombre)
            .Select(x => new LineaContacto(x.Nombre, x.Parentesco, x.Telefono, x.EsPrincipal))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var movimientosCrudos = await contexto.MovimientosLaborales
            .Where(x => x.ColaboradorId == colaboradorId)
            .OrderByDescending(x => x.Fecha)
            .Select(x => new
            {
                x.Tipo,
                x.Fecha,
                x.Observacion,
                Salario = puedeVerSalario ? (decimal?)x.SalarioAnterior : null,
                SalarioNuevo = puedeVerSalario ? x.SalarioNuevo : null
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // El mas reciente es el vigente: lleva el punto en el color de la empresa.
        var movimientos = movimientosCrudos
            .Select((m, indice) => new LineaMovimiento(
                m.Tipo, m.Fecha, m.Observacion, m.Salario, m.SalarioNuevo, indice == 0))
            .ToList();

        var contratos = await contexto.Contratos
            .Where(x => x.ColaboradorId == colaboradorId)
            .OrderByDescending(x => x.FechaInicio)
            .Select(x => new LineaContrato(
                x.Numero,
                x.TipoContrato!.Nombre,
                x.FechaInicio,
                x.FechaFin,
                puedeVerSalario ? (decimal?)x.SalarioAcordado : null,
                x.Vigente))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var documentos = await contexto.Documentos
            .Where(x => x.ColaboradorId == colaboradorId)
            .OrderBy(x => x.TipoDocumento!.Nombre)
            .Select(x => new FilaDocumento(
                x.Id,
                x.ColaboradorId,
                x.TipoDocumento!.Nombre,
                x.NombreArchivo,
                x.Extension,
                x.Descripcion,
                x.FechaEmision,
                x.FechaVencimiento,
                x.TamanoBytes,
                x.TipoDocumento.DiasAvisoAnticipado))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var incidencias = await contexto.Incidencias
            .Where(x => x.ColaboradorId == colaboradorId)
            .OrderByDescending(x => x.Fecha)
            .Select(x => new LineaIncidencia(x.Tipo, x.Fecha, x.Titulo, x.Descripcion))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var vacaciones = await contexto.Vacaciones
            .Where(x => x.ColaboradorId == colaboradorId)
            .OrderByDescending(x => x.FechaInicio)
            .Select(x => new LineaVacacion(x.FechaInicio, x.FechaFin, x.Dias, x.Estado, x.Observacion))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        _registro.LogInformation(
            "Ficha {Codigo} abierta: {Contactos} contacto(s), {Movimientos} movimiento(s), "
                + "{Contratos} contrato(s), {Documentos} documento(s).",
            cabecera.Codigo, contactos.Count, movimientos.Count, contratos.Count, documentos.Count);

        return new DetalleColaborador(
            cabecera.Id,
            cabecera.Codigo,
            string.Join(' ', new[]
                {
                    cabecera.PrimerNombre, cabecera.SegundoNombre,
                    cabecera.PrimerApellido, cabecera.SegundoApellido
                }
                .Where(parte => !string.IsNullOrWhiteSpace(parte))),
            cabecera.Identidad,
            cabecera.Sexo,
            cabecera.FechaNacimiento,
            cabecera.FechaIngreso,
            cabecera.FechaSalida,
            cabecera.Estado,
            cabecera.Telefono,
            cabecera.Correo,
            cabecera.Direccion,
            cabecera.Puesto,
            cabecera.Departamento,
            cabecera.Sucursal,
            cabecera.Salario,
            puedeVerSalario,
            contactos,
            movimientos,
            contratos,
            documentos,
            incidencias,
            vacaciones);
    }
}
