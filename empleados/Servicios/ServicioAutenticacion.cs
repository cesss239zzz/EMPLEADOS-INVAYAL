using empleados.Datos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioAutenticacion : IServicioAutenticacion
{
    /// <summary>Intentos fallidos consecutivos antes de bloquear la cuenta.</summary>
    private const int IntentosPermitidos = 5;

    /// <summary>Duracion del bloqueo temporal.</summary>
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioAutenticacion> _registro;

    public ServicioAutenticacion(
        IDbContextFactory<ContextoRhManager> fabrica,
        SesionUsuario sesion,
        ILogger<ServicioAutenticacion> registro)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<IntentoAcceso> AutenticarAsync(
        string nombreUsuario,
        string contrasena,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrWhiteSpace(contrasena))
        {
            return new IntentoAcceso(ResultadoAcceso.CredencialesInvalidas,
                "Escriba su usuario y su contraseña.");
        }

        var usuarioNormalizado = nombreUsuario.Trim();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var usuario = await contexto.Usuarios
            .FirstOrDefaultAsync(u => u.NombreUsuario == usuarioNormalizado, cancelacion)
            .ConfigureAwait(false);

        if (usuario is null)
        {
            // Se registra el nombre probado, nunca la contrasena (CLAUDE.md, regla 12).
            _registro.LogWarning("Intento de acceso con usuario inexistente: {Usuario}", usuarioNormalizado);

            return new IntentoAcceso(ResultadoAcceso.CredencialesInvalidas,
                "Usuario o contraseña incorrectos.");
        }

        if (usuario.BloqueadoHasta is { } hasta && hasta > DateTime.UtcNow)
        {
            var restantes = (int)Math.Ceiling((hasta - DateTime.UtcNow).TotalMinutes);
            _registro.LogWarning("Acceso rechazado: la cuenta {Usuario} está bloqueada {Minutos} minuto(s) más.",
                usuario.NombreUsuario, restantes);

            return new IntentoAcceso(ResultadoAcceso.Bloqueado,
                "La cuenta está bloqueada por intentos fallidos. Vuelva a intentar en "
                    + restantes + " minuto(s).", restantes);
        }

        if (!usuario.Activo)
        {
            _registro.LogWarning("Acceso rechazado: la cuenta {Usuario} está desactivada.", usuario.NombreUsuario);

            return new IntentoAcceso(ResultadoAcceso.UsuarioInactivo,
                "La cuenta está desactivada. Comuníquese con el administrador.");
        }

        // BCrypt.Verify es deliberadamente lento: ese costo es la defensa contra
        // probar contrasenas en masa. No sustituirlo por una comparacion directa.
        var coincide = BCrypt.Net.BCrypt.Verify(contrasena, usuario.HashContrasena);

        if (!coincide)
        {
            usuario.IntentosFallidos++;

            if (usuario.IntentosFallidos >= IntentosPermitidos)
            {
                usuario.BloqueadoHasta = DateTime.UtcNow.Add(DuracionBloqueo);
                usuario.IntentosFallidos = 0;

                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

                _registro.LogWarning("Cuenta {Usuario} bloqueada por {Minutos} minutos tras {Intentos} fallos.",
                    usuario.NombreUsuario, DuracionBloqueo.TotalMinutes, IntentosPermitidos);

                return new IntentoAcceso(ResultadoAcceso.Bloqueado,
                    "Demasiados intentos fallidos. La cuenta quedó bloqueada por "
                        + (int)DuracionBloqueo.TotalMinutes + " minutos.",
                    (int)DuracionBloqueo.TotalMinutes);
            }

            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

            var quedan = IntentosPermitidos - usuario.IntentosFallidos;
            _registro.LogWarning("Contraseña incorrecta para {Usuario}. Quedan {Quedan} intento(s).",
                usuario.NombreUsuario, quedan);

            return new IntentoAcceso(ResultadoAcceso.CredencialesInvalidas,
                "Usuario o contraseña incorrectos. Le quedan " + quedan + " intento(s) antes del bloqueo.");
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoAcceso = DateTime.UtcNow;
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        _sesion.Iniciar(usuario);

        _registro.LogInformation("Acceso correcto de {Usuario} con perfil {Perfil}.",
            usuario.NombreUsuario, usuario.Perfil);

        return new IntentoAcceso(ResultadoAcceso.Correcto, "Bienvenido, " + usuario.NombreCompleto + ".");
    }
}
