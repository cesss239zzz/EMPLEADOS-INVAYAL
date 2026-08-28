namespace empleados.Datos;

/// <summary>
/// Empresa activa de la sesion. Es la fuente del filtro global de consulta
/// (CLAUDE.md, regla 9). Se registra como singleton.
/// </summary>
public interface IContextoEmpresa
{
    /// <summary>
    /// Empresa sobre la que se trabaja. Vale 0 cuando todavia no se eligio
    /// ninguna, y con 0 el filtro global no devuelve ninguna fila: eso es
    /// deliberado, es mas seguro no ver nada que ver de mas.
    /// </summary>
    int EmpresaActivaId { get; }

    /// <summary>Nombre corto de la empresa activa, para la barra superior.</summary>
    string NombreEmpresaActiva { get; }

    /// <summary>Color de identidad de la empresa activa, en formato #RRGGBB.</summary>
    string ColorEmpresaActiva { get; }

    /// <summary>Verdadero cuando hay una empresa elegida.</summary>
    bool HayEmpresaActiva { get; }

    /// <summary>Fija la empresa sobre la que se va a trabajar.</summary>
    void Establecer(int empresaId, string nombreCorto, string colorPrimario);

    /// <summary>Suelta la empresa activa. Se usa al cerrar sesion.</summary>
    void Limpiar();
}
