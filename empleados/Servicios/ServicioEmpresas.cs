using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioEmpresas : IServicioEmpresas
{
    private readonly IDbContextFactory<ContextoSigem> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioEmpresas> _registro;

    public ServicioEmpresas(
        IDbContextFactory<ContextoSigem> fabrica,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        ILogger<ServicioEmpresas> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TarjetaEmpresa>> ObtenerDisponiblesAsync(CancellationToken cancelacion = default)
    {
        if (!_sesion.EstaAutenticado)
        {
            throw new InvalidOperationException(
                "No se pueden listar empresas sin una sesion iniciada.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Los identificadores permitidos salen de usuario_empresa: un usuario no
        // ve empresas a las que no fue habilitado, ni siquiera en el selector.
        var permitidas = await contexto.UsuariosEmpresas
            .Where(ue => ue.UsuarioId == _sesion.UsuarioId)
            .Select(ue => ue.EmpresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (permitidas.Count == 0)
        {
            _registro.LogWarning("El usuario {Usuario} no tiene ninguna empresa habilitada.",
                _sesion.NombreUsuario);
            return Array.Empty<TarjetaEmpresa>();
        }

        // Empresa no lleva filtro global (ella ES el inquilino), asi que el
        // recorte por usuario se hace explicito aca. Los conteos se resuelven en
        // la base con IgnoreQueryFilters, porque en este punto todavia no hay
        // empresa activa y el filtro global dejaria todo en cero.
        var empresas = await contexto.Empresas
            .Where(e => permitidas.Contains(e.Id) && e.Activa)
            .OrderBy(e => e.Nombre)
            .Select(e => new
            {
                e.Id,
                e.Nombre,
                e.NombreCorto,
                e.Rtn,
                e.ColorPrimario,
                Colaboradores = contexto.Colaboradores
                    .IgnoreQueryFilters()
                    .Count(c => c.EmpresaId == e.Id && c.Estado == EstadoColaborador.Activo),
                Sucursales = contexto.Sucursales
                    .IgnoreQueryFilters()
                    .Count(s => s.EmpresaId == e.Id && s.Activa)
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        _registro.LogInformation("El usuario {Usuario} tiene {Cantidad} empresa(s) disponibles.",
            _sesion.NombreUsuario, empresas.Count);

        return empresas
            .Select(e => new TarjetaEmpresa(e.Id, e.Nombre, e.NombreCorto, e.Rtn,
                e.ColorPrimario, e.Colaboradores, e.Sucursales))
            .ToList();
    }

    /// <inheritdoc />
    public async Task ActivarAsync(int empresaId, CancellationToken cancelacion = default)
    {
        if (!_sesion.EstaAutenticado)
        {
            throw new InvalidOperationException("No se puede activar una empresa sin sesion iniciada.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Se vuelve a comprobar el permiso contra la base. Confiar en que la
        // pantalla solo ofrecio empresas validas seria confiar en la interfaz
        // para algo que es control de acceso.
        var habilitada = await contexto.UsuariosEmpresas
            .AnyAsync(ue => ue.UsuarioId == _sesion.UsuarioId && ue.EmpresaId == empresaId, cancelacion)
            .ConfigureAwait(false);

        if (!habilitada)
        {
            _registro.LogError("El usuario {Usuario} intento activar la empresa {Empresa}, que no tiene habilitada.",
                _sesion.NombreUsuario, empresaId);

            throw new InvalidOperationException("El usuario no tiene acceso a esa empresa.");
        }

        var empresa = await contexto.Empresas
            .FirstOrDefaultAsync(e => e.Id == empresaId && e.Activa, cancelacion)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("La empresa no existe o esta desactivada.");

        _contextoEmpresa.Establecer(empresa.Id, empresa.NombreCorto, empresa.ColorPrimario);

        _registro.LogInformation("Empresa activa: {Empresa} (id {Id}). El filtro global ya apunta ahi.",
            empresa.NombreCorto, empresa.Id);
    }
}
