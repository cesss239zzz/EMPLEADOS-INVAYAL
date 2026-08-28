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

/// <summary>Un documento digitalizado del expediente.</summary>
public sealed record LineaDocumento(
    string Tipo,
    string NombreArchivo,
    DateTime FechaEmision,
    DateTime? FechaVencimiento)
{
    public string EmisionTexto => FechaEmision.ToString("dd/MM/yyyy");

    public string VencimientoTexto => FechaVencimiento is null
        ? "No vence"
        : FechaVencimiento.Value.ToString("dd/MM/yyyy");

    /// <summary>Dias que faltan para vencer. Nulo si no vence.</summary>
    public int? DiasParaVencer => FechaVencimiento is null
        ? null
        : (int)(FechaVencimiento.Value.Date - DateTime.Today).TotalDays;

    public bool EstaVencido => DiasParaVencer is < 0;

    public bool PorVencer => DiasParaVencer is >= 0 and <= 60;

    public string SituacionTexto => FechaVencimiento is null ? "Vigente"
        : EstaVencido ? "Vencido"
        : PorVencer ? "Por vencer"
        : "Vigente";
}

/// <summary>
/// Expediente completo de un colaborador. Se consulta al abrir la ficha, nunca
/// antes (CLAUDE.md, regla 13).
/// </summary>
public sealed record DetalleColaborador(
    int Id,
    string Codigo,
    string NombreCompleto,
    string Identidad,
    Sexo Sexo,
    DateTime FechaNacimiento,
    DateTime FechaIngreso,
    DateTime? FechaSalida,
    EstadoColaborador Estado,
    string Telefono,
    string Correo,
    string Direccion,
    string Puesto,
    string Departamento,
    string Sucursal,
    decimal? Salario,
    IReadOnlyList<LineaContacto> Contactos,
    IReadOnlyList<LineaMovimiento> Movimientos,
    IReadOnlyList<LineaContrato> Contratos,
    IReadOnlyList<LineaDocumento> Documentos)
{
    public string SexoTexto => Sexo == Sexo.Femenino ? "Femenino" : "Masculino";

    public string NacimientoTexto => FechaNacimiento.ToString("dd/MM/yyyy");

    public string IngresoTexto => FechaIngreso.ToString("dd/MM/yyyy");

    public string SalarioTexto => Salario is null ? "———" : Salario.Value.ToString("C");

    public string EstadoTexto => Estado switch
    {
        EstadoColaborador.Activo => "Activo",
        EstadoColaborador.Suspendido => "Suspendido",
        _ => "Inactivo"
    };

    /// <summary>Edad cumplida hoy.</summary>
    public int Edad
    {
        get
        {
            var edad = DateTime.Today.Year - FechaNacimiento.Year;
            if (FechaNacimiento.Date > DateTime.Today.AddYears(-edad))
            {
                edad--;
            }

            return edad;
        }
    }

    public string EdadTexto => Edad + " anos";

    /// <summary>Antiguedad en la empresa, en anos y meses.</summary>
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
            var anos = meses / 12;
            var restantes = meses % 12;

            return anos == 0
                ? restantes + " meses"
                : restantes == 0 ? anos + " anos" : anos + " anos y " + restantes + " meses";
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
