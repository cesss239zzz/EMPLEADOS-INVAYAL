namespace empleados.Servicios;

/// <summary>Empresa tal como se muestra en el selector. Solo lo que la tarjeta necesita.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Nombre">Razon social completa.</param>
/// <param name="NombreCorto">Nombre para la barra superior.</param>
/// <param name="Rtn">Registro Tributario Nacional.</param>
/// <param name="ColorPrimario">Color de identidad, formato #RRGGBB.</param>
/// <param name="Colaboradores">Cuantas personas activas tiene.</param>
/// <param name="Sucursales">Cuantas sucursales tiene.</param>
public sealed record TarjetaEmpresa(
    int Id,
    string Nombre,
    string NombreCorto,
    string Rtn,
    string ColorPrimario,
    int Colaboradores,
    int Sucursales);

/// <summary>Consulta de empresas a las que el usuario autenticado tiene acceso.</summary>
public interface IServicioEmpresas
{
    /// <summary>
    /// Empresas visibles para el usuario de la sesion. Se consulta al abrir el
    /// selector, nunca al arrancar (CLAUDE.md, regla 13).
    /// </summary>
    Task<IReadOnlyList<TarjetaEmpresa>> ObtenerDisponiblesAsync(CancellationToken cancelacion = default);

    /// <summary>Fija la empresa activa y deja el filtro global apuntando a ella.</summary>
    Task ActivarAsync(int empresaId, CancellationToken cancelacion = default);
}
