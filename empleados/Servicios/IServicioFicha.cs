using empleados.Datos.Entidades;

namespace empleados.Servicios;

/// <summary>Un contacto de emergencia, listo para mostrar.</summary>
public sealed record LineaContacto(string Nombre, string Parentesco, string Telefono, bool EsPrincipal)
{
    public string Etiqueta => EsPrincipal ? "Principal" : "Alterno";
}

/// <summary>Un hecho del historial laboral, listo para la linea de tiempo.</summary>
public sealed record LineaMovimiento(
    TipoMovimiento Tipo,
    DateTime Fecha,
    string Descripcion,
    decimal? SalarioAnterior,
    decimal? SalarioNuevo,
    bool EsVigente)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

    public string TipoTexto => Tipo switch
    {
        TipoMovimiento.Ingreso => "Ingreso",
        TipoMovimiento.CambioPuesto => "Cambio de puesto",
        TipoMovimiento.CambioSalario => "Cambio de salario",
        TipoMovimiento.TrasladoSucursal => "Traslado de sucursal",
        TipoMovimiento.Suspension => "Suspension",
        TipoMovimiento.Reingreso => "Reingreso",
        TipoMovimiento.Salida => "Salida",
        _ => "Movimiento"
    };

    /// <summary>Texto del cambio salarial, o vacio si el movimiento no lo toca.</summary>
    public string CambioSalario => SalarioNuevo is null
        ? string.Empty
        : SalarioAnterior is null
            ? SalarioNuevo.Value.ToString("C")
            : SalarioAnterior.Value.ToString("C") + "  →  " + SalarioNuevo.Value.ToString("C");

    public bool HayCambioSalario => SalarioNuevo is not null;
}

/// <summary>Un contrato del expediente.</summary>
public sealed record LineaContrato(
    string Numero,
    string Tipo,
    DateTime FechaInicio,
    DateTime? FechaFin,
    decimal? Salario,
    bool Vigente)
{
    public string PeriodoTexto => FechaFin is null
        ? FechaInicio.ToString("dd/MM/yyyy") + "  →  indefinido"
        : FechaInicio.ToString("dd/MM/yyyy") + "  →  " + FechaFin.Value.ToString("dd/MM/yyyy");

    public string SalarioTexto => Salario is null ? "———" : Salario.Value.ToString("C");

    public string EstadoTexto => Vigente ? "Vigente" : "Terminado";
}

/// <summary>
/// Semaforo documental del expediente. Es lo que tiñe cada renglon del panel
/// "Expediente Digital" de la ficha: verde al dia, ambar por vencer, rojo vencido.
/// </summary>
public enum SituacionDocumento
{
    Vigente = 0,
    PorVencer = 1,
    Vencido = 2
}


/// <summary>
/// Expediente completo de un colaborador. Se consulta al abrir la ficha, nunca
/// antes (CLAUDE.md, regla 13).
/// </summary>
public sealed record DetalleColaborador(
    int Id,
    string Codigo,
    string NombreCompleto,
    string? Identidad,
    Sexo? Sexo,
    DateTime? FechaNacimiento,
    DateTime FechaIngreso,
    DateTime? FechaSalida,
    EstadoColaborador Estado,
    string? Telefono,
    string? Correo,
    string? Direccion,
    string? Puesto,
    string? Departamento,
    string? Sucursal,
    decimal? Salario,
    bool PuedeVerSalario,
    IReadOnlyList<LineaContacto> Contactos,
    IReadOnlyList<LineaMovimiento> Movimientos,
    IReadOnlyList<LineaContrato> Contratos,
    IReadOnlyList<FilaDocumento> Documentos,
    IReadOnlyList<LineaIncidencia> Incidencias,
    IReadOnlyList<LineaVacacion> Vacaciones)
{
    /// <summary>
    /// Lo que ve el usuario donde no hay dato. La ficha nunca deja un hueco en
    /// blanco: un espacio vacio no distingue "no lo capturamos" de "se rompio
    /// la pantalla" (solicitud de cambios, CR-04).
    /// </summary>
    public const string SinDato = "Sin registrar";

    public string IdentidadTexto => Mostrar(Identidad);
    public string TelefonoTexto => Mostrar(Telefono);
    public string CorreoTexto => Mostrar(Correo);
    public string DireccionTexto => Mostrar(Direccion);
    public string PuestoTexto => Mostrar(Puesto);
    public string DepartamentoTexto => Mostrar(Departamento);
    public string SucursalTexto => Mostrar(Sucursal);

    // El nombre completo del tipo hace falta porque la propiedad Sexo tapa al
    // tipo Sexo dentro de este record.
    public string SexoTexto => Sexo switch
    {
        empleados.Datos.Entidades.Sexo.Femenino => "Femenino",
        empleados.Datos.Entidades.Sexo.Masculino => "Masculino",
        _ => SinDato
    };

    public string NacimientoTexto => FechaNacimiento is { } fecha
        ? fecha.ToString("dd/MM/yyyy")
        : SinDato;

    public string IngresoTexto => FechaIngreso.ToString("dd/MM/yyyy");

    public string SalarioTexto => !PuedeVerSalario ? "———"
        : Salario is null ? SinDato
        : Salario.Value.ToString("C");

    private static string Mostrar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? SinDato : valor;

    public string EstadoTexto => Estado switch
    {
        EstadoColaborador.Activo => "Activo",
        EstadoColaborador.Suspendido => "Suspendido",
        _ => "Inactivo"
    };

    /// <summary>Edad cumplida hoy, o nula si no se capturo la fecha de nacimiento.</summary>
    public int? Edad
    {
        get
        {
            if (FechaNacimiento is not { } nacimiento)
            {
                return null;
            }

            var edad = DateTime.Today.Year - nacimiento.Year;
            if (nacimiento.Date > DateTime.Today.AddYears(-edad))
            {
                edad--;
            }

            return edad;
        }
    }

    public string EdadTexto => Edad is { } edad
        ? edad + (edad == 1 ? " año" : " años")
        : SinDato;

    /// <summary>Antiguedad en la empresa, en años y meses.</summary>
    public string AntiguedadTexto
    {
        get
        {
            var hasta = FechaSalida ?? DateTime.Today;
            var meses = ((hasta.Year - FechaIngreso.Year) * 12) + hasta.Month - FechaIngreso.Month;
            if (hasta.Day < FechaIngreso.Day)
            {
                meses--;
            }

            meses = Math.Max(meses, 0);
            var años = meses / 12;
            var restantes = meses % 12;

            var textoAños = años == 1 ? "1 año" : años + " años";
            var textoMeses = restantes == 1 ? "1 mes" : restantes + " meses";

            return años == 0
                ? textoMeses
                : restantes == 0 ? textoAños : textoAños + " y " + textoMeses;
        }
    }

    /// <summary>Iniciales para el avatar cuando no hay fotografia.</summary>
    public string Iniciales
    {
        get
        {
            var partes = NombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0)
            {
                return "?";
            }

            var primera = partes[0][..1];
            var segunda = partes.Length > 2 ? partes[2][..1] : partes.Length > 1 ? partes[1][..1] : string.Empty;
            return (primera + segunda).ToUpperInvariant();
        }
    }
}

/// <summary>Consulta del expediente completo de un colaborador.</summary>
public interface IServicioFicha
{
    /// <summary>
    /// Trae el expediente. Devuelve nulo si el colaborador no existe o no
    /// pertenece a la empresa activa: el filtro global se encarga de lo segundo.
    /// </summary>
    Task<DetalleColaborador?> ObtenerAsync(int colaboradorId, CancellationToken cancelacion = default);
}
