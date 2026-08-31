using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;

namespace empleados.Datos;

/// <summary>
/// Siembra de sistema. Es lo UNICO que la aplicacion trae precargado.
///
/// RH Manager se entrega como un cascaron limpio (solicitud de cambios, CR-01):
/// al abrirlo por primera vez no existe ninguna empresa, ningun colaborador,
/// ningun documento, ninguna alerta ni ningun registro de historial. El usuario
/// carga toda su informacion desde cero.
///
/// Aca solo vive el usuario administrador inicial, que es lo estrictamente
/// necesario para poder entrar la primera vez. Sin el no habria forma de
/// autenticarse y la aplicacion quedaria cerrada sobre si misma.
///
/// Los catalogos (departamentos, puestos, sucursales, tipos de documento) NO se
/// siembran: se administran desde el apartado de catalogos y arrancan vacios,
/// porque los valores de fabrica no sirven para una empresa que use otros.
/// </summary>
internal static class SiembraSistema
{
    /// <summary>Marca fija de creacion. Debe ser constante: un valor dinamico
    /// haria que cada migracion detecte un cambio inexistente.</summary>
    private static readonly DateTime Marca = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Hash BCrypt factor 11 de la contrasena del usuario inicial.
    /// La contrasena en claro NUNCA aparece en el codigo ni en la migracion
    /// (CLAUDE.md, regla 12). El usuario debe cambiarla al primer acceso.
    ///
    /// El hash que sembro la migracion inicial no correspondia a la contrasena
    /// que se le entrego al cliente. Se genero un hash y la clave en claro no
    /// quedo anotada en ninguna parte, asi que "cregalado" no podia entrar en
    /// ninguna maquina: BCrypt.Verify devolvia falso siempre y, tras cinco
    /// intentos, la cuenta se bloqueaba quince minutos.
    ///
    /// Lo corrige la migracion CorrigeContrasenaInicial, que ademas arregla las
    /// bases que ya estan creadas.
    /// </summary>
    private const string HashContrasenaInicial =
        "$2a$11$GpgRg/FpTrjEdGCSqifH7ekvwN5VZJtyz..1bYZdLrpEhwb22F8ri";

    public static void Aplicar(ModelBuilder constructor)
    {
        SembrarUsuarioInicial(constructor);
    }

    /// <summary>
    /// Usuario administrador inicial. No se le asocia ninguna empresa porque
    /// todavia no existe ninguna: el SuperAdministrador atraviesa empresas por
    /// perfil, no por filas de usuario_empresa.
    /// </summary>
    private static void SembrarUsuarioInicial(ModelBuilder c) => c.Entity<Usuario>().HasData(
        new Usuario
        {
            Id = 1,
            NombreUsuario = "cregalado",
            HashContrasena = HashContrasenaInicial,
            NombreCompleto = "Cesar Regalado",
            Correo = "cregalado@invayal.hn",
            Perfil = PerfilUsuario.SuperAdministrador,
            Activo = true,
            DebeCambiarContrasena = true,
            IntentosFallidos = 0,
            FechaCreacion = Marca
        });
}
