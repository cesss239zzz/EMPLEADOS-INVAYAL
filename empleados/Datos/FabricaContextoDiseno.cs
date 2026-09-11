using empleados.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace empleados.Datos;

/// <summary>
/// La usa <c>dotnet ef</c> en tiempo de diseño para generar migraciones. No la
/// usa la aplicacion: en ejecucion el contexto sale de la fabrica registrada en
/// MauiProgram. Existe porque las herramientas de EF no pueden arrancar una
/// aplicacion MAUI para pedirle su contenedor de dependencias.
/// </summary>
public sealed class FabricaContextoDiseno : IDesignTimeDbContextFactory<ContextoRhManager>
{
    public ContextoRhManager CreateDbContext(string[] argumentos)
    {
        var opciones = new DbContextOptionsBuilder<ContextoRhManager>()
            .UseSqlite(RutasRhManager.CadenaConexionPorOmision)
            .Options;

        // En diseño no hay sesion, asi que no hay empresa activa. El filtro
        // global queda en 0 y no devuelve filas, cosa que a la generacion de
        // migraciones le da igual: solo le interesa la forma del modelo.
        return new ContextoRhManager(opciones, new ContextoEmpresa(), new empleados.Servicios.SesionUsuario());
    }
}
