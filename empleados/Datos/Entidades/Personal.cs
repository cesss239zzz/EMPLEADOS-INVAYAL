namespace empleados.Datos.Entidades;

/// <summary>
/// Expediente del colaborador. Es el centro del sistema.
/// Un colaborador pertenece a UNA sucursal (decision del cliente).
/// </summary>
public class Colaborador : EntidadEmpresa
{
    /// <summary>Codigo interno del expediente.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Numero de identidad. Unico dentro de la empresa.</summary>
    public string Identidad { get; set; } = string.Empty;

    public string PrimerNombre { get; set; } = string.Empty;
    public string SegundoNombre { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    public string SegundoApellido { get; set; } = string.Empty;

    /// <summary>Nombre armado para listas y busquedas. No se persiste.</summary>
    public string NombreCompleto =>
        string.Join(' ', new[] { PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido }
            .Where(parte => !string.IsNullOrWhiteSpace(parte)));

    public Sexo Sexo { get; set; }
    public DateTime FechaNacimiento { get; set; }
    public DateTime FechaIngreso { get; set; }

    /// <summary>Solo tiene valor cuando el colaborador ya salio.</summary>
    public DateTime? FechaSalida { get; set; }

    public EstadoColaborador Estado { get; set; } = EstadoColaborador.Activo;

    public string Telefono { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;

    /// <summary>Importe en decimal, jamas en double ni float (CLAUDE.md, regla 10).</summary>
    public decimal SalarioBase { get; set; }

    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    public int DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }

    public int PuestoId { get; set; }
    public Puesto? Puesto { get; set; }

    public ICollection<ContactoEmergencia> ContactosEmergencia { get; set; } = new List<ContactoEmergencia>();
    public ICollection<MovimientoLaboral> Movimientos { get; set; } = new List<MovimientoLaboral>();
    public ICollection<Contrato> Contratos { get; set; } = new List<Contrato>();
    public ICollection<DocumentoDigitalizado> Documentos { get; set; } = new List<DocumentoDigitalizado>();
}

/// <summary>A quien llamar si le pasa algo al colaborador.</summary>
public class ContactoEmergencia : EntidadEmpresa
{
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string Parentesco { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string TelefonoAlterno { get; set; } = string.Empty;

    /// <summary>Contacto al que se llama primero.</summary>
    public bool EsPrincipal { get; set; }
}

/// <summary>
/// Un hecho del historial laboral. Es inmutable: no se edita, se agrega otro.
/// El historial es la memoria del expediente y por eso nunca se reescribe.
/// </summary>
public class MovimientoLaboral : EntidadEmpresa
{
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    public TipoMovimiento Tipo { get; set; }
    public DateTime Fecha { get; set; }

    public int? PuestoAnteriorId { get; set; }
    public int? PuestoNuevoId { get; set; }
    public int? SucursalAnteriorId { get; set; }
    public int? SucursalNuevaId { get; set; }

    public decimal? SalarioAnterior { get; set; }
    public decimal? SalarioNuevo { get; set; }

    public string Observacion { get; set; } = string.Empty;
}

/// <summary>Contrato firmado. Su vencimiento alimenta el motor de alertas.</summary>
public class Contrato : EntidadEmpresa
{
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    public int TipoContratoId { get; set; }
    public TipoContrato? TipoContrato { get; set; }

    public string Numero { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }

    /// <summary>Nula en los contratos indefinidos.</summary>
    public DateTime? FechaFin { get; set; }

    public decimal SalarioAcordado { get; set; }
    public bool Vigente { get; set; } = true;
    public string Observacion { get; set; } = string.Empty;
}

/// <summary>
/// Documento escaneado del expediente. La subida de archivos esta fuera del
/// alcance de la demostracion; la tabla existe porque la ficha la muestra.
/// </summary>
public class DocumentoDigitalizado : EntidadEmpresa
{
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    public int TipoDocumentoId { get; set; }
    public TipoDocumento? TipoDocumento { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;

    /// <summary>Ruta relativa a la carpeta de archivos, nunca una ruta absoluta.</summary>
    public string RutaRelativa { get; set; } = string.Empty;

    public DateTime FechaEmision { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public long TamanoBytes { get; set; }
}

/// <summary>
/// Aviso generado por el motor de alertas. <see cref="ClaveIdempotencia"/> es
/// lo que impide que correr el motor dos veces duplique el mismo aviso.
/// </summary>
public class Aviso : EntidadEmpresa
{
    public TipoAviso Tipo { get; set; }
    public EstadoAviso Estado { get; set; } = EstadoAviso.Pendiente;

    public int? ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public DateTime FechaGeneracion { get; set; }

    /// <summary>Fecha del hecho que se avisa: el vencimiento, el cumpleanos.</summary>
    public DateTime FechaReferencia { get; set; }

    /// <summary>
    /// Identidad logica del aviso: tipo + entidad + fecha. Lleva indice unico,
    /// asi que un segundo intento de sembrar el mismo aviso choca en la base
    /// en vez de duplicarlo en silencio.
    /// </summary>
    public string ClaveIdempotencia { get; set; } = string.Empty;
}
