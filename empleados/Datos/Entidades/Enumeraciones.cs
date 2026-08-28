namespace empleados.Datos.Entidades;

// Todas las enumeraciones se persisten como entero pequeno, nunca como texto
// ni como ENUM del motor (CLAUDE.md, regla 10). Los valores son explicitos
// para que agregar un elemento no corra la numeracion de los ya guardados.

/// <summary>Perfil de acceso. Define que ve y que puede hacer un usuario.</summary>
public enum PerfilUsuario
{
    SuperAdministrador = 1,
    Administrador = 2,
    SupervisorSucursal = 3,
    Consulta = 4
}

/// <summary>Situacion actual del colaborador dentro de la empresa.</summary>
public enum EstadoColaborador
{
    Activo = 1,
    Suspendido = 2,
    Inactivo = 3
}

/// <summary>Sexo registrado en el expediente.</summary>
public enum Sexo
{
    Masculino = 1,
    Femenino = 2
}

/// <summary>Tipo de movimiento en el historial laboral.</summary>
public enum TipoMovimiento
{
    Ingreso = 1,
    CambioPuesto = 2,
    CambioSalario = 3,
    TrasladoSucursal = 4,
    Suspension = 5,
    Reingreso = 6,
    Salida = 7
}

/// <summary>Motivo por el que se genero un aviso.</summary>
public enum TipoAviso
{
    VencimientoContrato = 1,
    VencimientoDocumento = 2,
    Cumpleanos = 3,
    AniversarioLaboral = 4,
    FinPeriodoPrueba = 5
}

/// <summary>Ciclo de vida de un aviso.</summary>
public enum EstadoAviso
{
    Pendiente = 1,
    Visto = 2,
    Resuelto = 3
}
