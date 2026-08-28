using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioColaboradores : IServicioColaboradores
{
    /// <summary>Documentos que se consideran un expediente completo.</summary>
    private const int DocumentosParaExpedienteCompleto = 3;

    private readonly IDbContextFactory<ContextoSigem> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioColaboradores> _registro;

    public ServicioColaboradores(
        IDbContextFactory<ContextoSigem> fabrica,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        ILogger<ServicioColaboradores> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilaColaborador>> ObtenerAsync(
        FiltroColaboradores filtro,
        CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var puedeVerSalario = _sesion.PuedeVerSalarios;

        // El filtro se arma sobre IQueryable y viaja a SQL. Traer la tabla entera
        // para filtrarla en memoria esta prohibido (CLAUDE.md, regla 13): con 12
        // filas daria igual, con 4000 no.
        var consulta = contexto.Colaboradores.AsQueryable();

        if (filtro.DepartamentoId is { } departamento)
        {
            consulta = consulta.Where(c => c.DepartamentoId == departamento);
        }

        if (filtro.SucursalId is { } sucursal)
        {
            consulta = consulta.Where(c => c.SucursalId == sucursal);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(c => c.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // LIKE de SQLite no distingue mayusculas en ASCII, asi que "delmy"
            // y "Delmy" encuentran lo mismo. No ignora tildes: buscar "Nunez"
            // no encuentra "Nunez" escrito con enie.
            var patron = "%" + filtro.Texto.Trim() + "%";

            consulta = consulta.Where(c =>
                EF.Functions.Like(c.PrimerNombre, patron)
                || EF.Functions.Like(c.SegundoNombre, patron)
                || EF.Functions.Like(c.PrimerApellido, patron)
                || EF.Functions.Like(c.SegundoApellido, patron)
                || EF.Functions.Like(c.Codigo, patron)
                || EF.Functions.Like(c.Identidad, patron));
        }

        var ordenada = consulta
            .OrderBy(c => c.PrimerApellido).ThenBy(c => c.PrimerNombre);

        // El tope viaja como LIMIT. Traer todo y quedarse con los primeros seria
        // exactamente lo que prohibe la regla 13.
        var acotada = filtro.Tope is > 0 ? ordenada.Take(filtro.Tope.Value) : ordenada.AsQueryable();

        var filas = await acotada
            .Select(c => new
            {
                c.Id,
                c.Codigo,
                c.PrimerNombre,
                c.SegundoNombre,
                c.PrimerApellido,
                c.SegundoApellido,
                c.Identidad,
                Puesto = c.Puesto!.Nombre,
                Departamento = c.Departamento!.Nombre,
                Sucursal = c.Sucursal!.Nombre,
                c.FechaIngreso,
                c.Estado,
                // El salario no se trae siquiera cuando el perfil no puede verlo:
                // ocultarlo solo en la vista dejaria el dato viajando igual.
                Salario = puedeVerSalario ? (decimal?)c.SalarioBase : null,
                Documentos = contexto.Documentos.Count(d => d.ColaboradorId == c.Id)
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        _registro.LogInformation(
            "Colaboradores de {Empresa}: {Cantidad} fila(s). Texto: {Texto}, departamento: {Departamento}, "
                + "sucursal: {Sucursal}, estado: {Estado}. Salario visible: {VeSalario}",
            _contextoEmpresa.NombreEmpresaActiva, filas.Count, filtro.Texto ?? "(sin texto)",
            filtro.DepartamentoId, filtro.SucursalId, filtro.Estado, puedeVerSalario);

        return filas.Select(f => new FilaColaborador(
            f.Id,
            f.Codigo,
            string.Join(' ', new[] { f.PrimerNombre, f.SegundoNombre, f.PrimerApellido, f.SegundoApellido }
                .Where(parte => !string.IsNullOrWhiteSpace(parte))),
            f.Identidad,
            f.Puesto,
            f.Departamento,
            f.Sucursal,
            f.FechaIngreso,
            f.Estado,
            f.Documentos >= DocumentosParaExpedienteCompleto ? EstadoExpediente.Completo
                : f.Documentos > 0 ? EstadoExpediente.Parcial
                : EstadoExpediente.Incompleto,
            f.Salario)).ToList();
    }

    /// <inheritdoc />
    public async Task<OpcionesFiltro> ObtenerOpcionesAsync(CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Los catalogos tambien salen filtrados por empresa: el desplegable de una
        // empresa no puede ofrecer departamentos de la otra.
        var departamentos = await contexto.Departamentos
            .Where(d => d.Activo)
            .OrderBy(d => d.Nombre)
            .Select(d => new OpcionFiltro(d.Id, d.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var sucursales = await contexto.Sucursales
            .Where(s => s.Activa)
            .OrderBy(s => s.Nombre)
            .Select(s => new OpcionFiltro(s.Id, s.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var total = await contexto.Colaboradores.CountAsync(cancelacion).ConfigureAwait(false);

        departamentos.Insert(0, new OpcionFiltro(0, "Todos los departamentos"));
        sucursales.Insert(0, new OpcionFiltro(0, "Todas las sucursales"));

        return new OpcionesFiltro(departamentos, sucursales, total);
    }

    private void ExigirEmpresaActiva()
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException(
                "No se pueden consultar colaboradores sin empresa activa.");
        }
    }
}
