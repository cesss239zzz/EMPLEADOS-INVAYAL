namespace empleados.Datos.Entidades;

/// <summary>
/// Expediente del colaborador. Es el centro del sistema.
/// Un colaborador pertenece a UNA sucursal (decision del cliente).
/// </summary>
public class Colaborador : EntidadEmpresa
{
    // ─────────────────────────────────────────────────────────────────────────
    // Obligatorio contra opcional (solicitud de cambios, CR-04).
    //
    // Solo cuatro datos hacen falta para abrir un expediente: codigo, primer
    // nombre, primer apellido y fecha de ingreso. Todo lo demas es opcional y se
    // completa despues, porque en la practica el expediente se abre el dia que
    // entra la persona y la papeleria llega la semana siguiente.
    //
    // Lo opcional que falta se guarda como NULO, nunca como cadena vacia ni como
    // cero: "sin dato" y "cero lempiras" no son lo mismo, y confundirlos hace que
    // un salario sin capturar aparezca como gratis en un reporte.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Codigo interno del expediente. Obligatorio.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Numero de identidad. Opcional; unico dentro de la empresa cuando se captura.</summary>
    public string? Identidad { get; set; }

    /// <summary>Obligatorio.</summary>
    public string PrimerNombre { get; set; } = string.Empty;

    public string? SegundoNombre { get; set; }

    /// <summary>Obligatorio.</summary>
    public string PrimerApellido { get; set; } = string.Empty;

    public string? SegundoApellido { get; set; }

    /// <summary>Nombre armado para listas y busquedas. No se persiste.</summary>
    public string NombreCompleto =>
        string.Join(' ', new[] { PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido }
            .Where(parte => !string.IsNullOrWhiteSpace(parte)));

    /// <summary>
    /// Copia plegada de nombres, codigo e identidad: sin tildes y en mayusculas.
    /// Es lo que consulta el buscador para que "jose nunez" encuentre a
    /// "José Núñez" (CR-08). La mantiene el servicio al guardar; no se muestra
    /// nunca en pantalla.
    /// </summary>
    public string TextoBusqueda { get; set; } = string.Empty;

    public Sexo? Sexo { get; set; }

    public DateTime? FechaNacimiento { get; set; }

    /// <summary>Obligatorio: es la unica fecha que siempre se conoce al registrar.</summary>
    public DateTime FechaIngreso { get; set; }

    /// <summary>Solo tiene valor cuando el colaborador ya salio.</summary>
    public DateTime? FechaSalida { get; set; }

    public EstadoColaborador Estado { get; set; } = EstadoColaborador.Activo;

    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Direccion { get; set; }

    /// <summary>
    /// Importe en decimal, jamas en double ni float (CLAUDE.md, regla 10).
    /// Nulo mientras no se capture: cero seria decir que trabaja gratis.
    /// </summary>
    public decimal? SalarioBase { get; set; }

    // Los tres catalogos son opcionales. Se eligen si existen; si el catalogo
    // esta vacio, el expediente se abre igual y se asignan despues (CR-05).

    public int? SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    public int? DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }

    public int? PuestoId { get; set; }
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
/// Documento escaneado del expediente.
///
/// El archivo ya NO vive en una carpeta del equipo: vive dentro de la base, en
/// <see cref="ContenidoDocumento"/> (solicitud de cambios, CR-09). Antes se
/// guardaba solo la ruta, y bastaba con mover o renombrar una carpeta para que
/// el expediente quedara apuntando a la nada. Guardarlo adentro tiene ademas un
/// efecto que importa: el respaldo de la base (CR-12) se lleva tambien los
/// documentos, sin tener que acordarse de copiar una carpeta aparte.
/// </summary>
public class DocumentoDigitalizado : EntidadEmpresa
{
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    public int TipoDocumentoId { get; set; }
    public TipoDocumento? TipoDocumento { get; set; }

    /// <summary>Nombre original del archivo, tal como lo trajo el usuario.</summary>
    public string NombreArchivo { get; set; } = string.Empty;

    /// <summary>Extension en minusculas y con punto: ".pdf", ".jpg".</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>Nota libre del usuario sobre este documento. Opcional.</summary>
    public string? Descripcion { get; set; }

    public DateTime FechaEmision { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public long TamanoBytes { get; set; }

    /// <summary>El archivo en si. Se carga solo cuando se abre o se descarga.</summary>
    public ContenidoDocumento? Contenido { get; set; }
}

/// <summary>
/// Los bytes de un documento, en tabla aparte a proposito.
///
/// Separarlo de <see cref="DocumentoDigitalizado"/> no es una preferencia de
/// estilo: si los bytes colgaran de la misma entidad, cualquier consulta que
/// listara documentos —la ficha, el motor de alertas, un reporte— arrastraria
/// megabytes de escaneos sin quererlo. En tabla propia hay que pedirlos
/// explicitamente, que es exactamente cuando se necesitan.
/// </summary>
public class ContenidoDocumento : EntidadEmpresa
{
    public int DocumentoId { get; set; }
    public DocumentoDigitalizado? Documento { get; set; }

    public byte[] Bytes { get; set; } = [];
}

/// <summary>Que se le hizo a un documento. Alimenta el historial (CR-09).</summary>
public enum AccionDocumento
{
    Subida = 1,
    Edicion = 2,
    Reemplazo = 3,
    Eliminacion = 4,
    Descarga = 5
}

/// <summary>
/// Rastro de todo lo que se hace con un documento: subirlo, editarlo,
/// reemplazarlo, descargarlo o eliminarlo (solicitud de cambios, CR-09).
///
/// Sobrevive al documento: cuando se elimina uno, su fila de eliminacion queda,
/// con el nombre que tenia. Un historial que se borra junto con lo que registra
/// no sirve para nada.
/// </summary>
public class MovimientoDocumento : EntidadEmpresa
{
    public int ColaboradorId { get; set; }

    /// <summary>Nulo cuando el documento al que se refiere ya fue eliminado.</summary>
    public int? DocumentoId { get; set; }

    /// <summary>Nombre del documento en el momento del hecho.</summary>
    public string NombreDocumento { get; set; } = string.Empty;

    public AccionDocumento Accion { get; set; }
    public DateTime Fecha { get; set; }

    /// <summary>Quien lo hizo.</summary>
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;

    /// <summary>Que cambio exactamente, en una linea.</summary>
    public string Detalle { get; set; } = string.Empty;
}

/// <summary>
/// Incidencia registrada en el expediente: amonestacion, memorando, permiso,
/// felicitacion, ausencia o tardanza. Es un hecho puntual con su fecha.
/// </summary>
public class Incidencia : EntidadEmpresa
{
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    public TipoIncidencia Tipo { get; set; }

    /// <summary>Fecha en que ocurrio el hecho.</summary>
    public DateTime Fecha { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Usuario que la registro, para dejar rastro de quien la capturo.</summary>
    public int RegistradaPorUsuarioId { get; set; }
}

/// <summary>
/// Periodo de vacaciones programado para un colaborador. Los dias se calculan al
/// registrar y se guardan para no recalcularlos en cada lectura.
/// </summary>
public class Vacacion : EntidadEmpresa
{
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }

    /// <summary>Dias calendario del periodo, extremos incluidos.</summary>
    public int Dias { get; set; }

    public EstadoVacacion Estado { get; set; } = EstadoVacacion.Programada;

    public string Observacion { get; set; } = string.Empty;

    public int RegistradaPorUsuarioId { get; set; }
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
