namespace empleados.Datos;

/// <inheritdoc />
public sealed class ContextoEmpresa : IContextoEmpresa
{
    /// <inheritdoc />
    public int EmpresaActivaId { get; private set; }

    /// <inheritdoc />
    public string NombreEmpresaActiva { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string ColorEmpresaActiva { get; private set; } = "#0F3D6E";

    /// <inheritdoc />
    public bool HayEmpresaActiva => EmpresaActivaId > 0;

    /// <inheritdoc />
    public void Establecer(int empresaId, string nombreCorto, string colorPrimario)
    {
        if (empresaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(empresaId),
                "No se puede activar una empresa sin identificador valido.");
        }

        EmpresaActivaId = empresaId;
        NombreEmpresaActiva = nombreCorto;
        ColorEmpresaActiva = string.IsNullOrWhiteSpace(colorPrimario) ? "#0F3D6E" : colorPrimario;
    }

    /// <inheritdoc />
    public void Limpiar()
    {
        EmpresaActivaId = 0;
        NombreEmpresaActiva = string.Empty;
        ColorEmpresaActiva = "#0F3D6E";
    }
}
