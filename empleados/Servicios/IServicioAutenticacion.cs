namespace empleados.Servicios;

/// <summary>Desenlace de un intento de acceso.</summary>
public enum ResultadoAcceso
{
    Correcto,
    /// <summary>Usuario inexistente o contrasena incorrecta. No se distingue cual,
    /// a proposito: decir "ese usuario no existe" le regala informacion a quien
    /// esta probando nombres.</summary>
    CredencialesInvalidas,
    /// <summary>La cuenta esta desactivada.</summary>
    UsuarioInactivo,
    /// <summary>Demasiados intentos fallidos. Bloqueada temporalmente.</summary>
    Bloqueado,
    /// <summary>Fallo tecnico al verificar.</summary>
    Error
}

/// <summary>Resultado de un intento de acceso, listo para mostrar.</summary>
/// <param name="Resultado">Desenlace.</param>
/// <param name="Mensaje">Texto en espanol para el usuario. Nunca una traza.</param>
/// <param name="MinutosBloqueo">Minutos que faltan para poder reintentar.</param>
public sealed record IntentoAcceso(ResultadoAcceso Resultado, string Mensaje, int MinutosBloqueo = 0)
{
    public bool EsCorrecto => Resultado == ResultadoAcceso.Correcto;
}

/// <summary>Verificacion de credenciales con BCrypt y bloqueo por intentos.</summary>
public interface IServicioAutenticacion
{
    /// <summary>
    /// Verifica usuario y contrasena. Si es correcto, deja la sesion iniciada.
    /// </summary>
    Task<IntentoAcceso> AutenticarAsync(string nombreUsuario, string contrasena, CancellationToken cancelacion = default);
}
