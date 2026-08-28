using empleados.Configuracion;
using empleados.Datos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioDiagnostico : IServicioDiagnostico
{
    private readonly OpcionesSigem _opciones;
    private readonly IDbContextFactory<ContextoSigem> _fabrica;
    private readonly ILogger<ServicioDiagnostico> _registro;

    public ServicioDiagnostico(
        OpcionesSigem opciones,
        IDbContextFactory<ContextoSigem> fabrica,
        ILogger<ServicioDiagnostico> registro)
    {
        _opciones = opciones;
        _fabrica = fabrica;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResultadoDiagnostico> VerificarAsync(CancellationToken cancelacion = default)
    {
        var resultadoCarpeta = VerificarCarpetaDeDatos();
        if (resultadoCarpeta is not null)
        {
            return resultadoCarpeta;
        }

        try
        {
            // El contexto se crea, se usa y se libera. Nunca se guarda
            // (CLAUDE.md, regla 2).
            await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

            var pendientes = (await contexto.Database
                .GetPendingMigrationsAsync(cancelacion).ConfigureAwait(false)).ToList();

            if (pendientes.Count > 0)
            {
                _registro.LogInformation("Aplicando {Cantidad} migracion(es) pendiente(s): {Lista}",
                    pendientes.Count, string.Join(", ", pendientes));

                // Se aplican solas. Pedirle al usuario de una demostracion que
                // corra dotnet ef a mano seria una pantalla de diagnostico que
                // nadie puede resolver.
                await contexto.Database.MigrateAsync(cancelacion).ConfigureAwait(false);

                _registro.LogInformation("Migraciones aplicadas. Base creada en {Ruta}",
                    RutasSigem.ArchivoBaseDatos);
            }

            // Consulta minima de comprobacion. Empresa no lleva filtro global,
            // asi que responde sin necesidad de empresa activa.
            var empresas = await contexto.Empresas.CountAsync(cancelacion).ConfigureAwait(false);

            _registro.LogInformation("Base verificada. {Empresas} empresa(s) disponibles.", empresas);

            return new ResultadoDiagnostico(
                EstadoDiagnostico.Correcto,
                "Base de datos lista",
                "La base responde y tiene " + empresas + " empresa(s) registrada(s).",
                string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _registro.LogError(ex, "Fallo la verificacion de la base de datos.");

            return new ResultadoDiagnostico(
                EstadoDiagnostico.MigracionFallida,
                "No se pudo preparar la base de datos",
                "RH Manager no logro abrir o actualizar su base de datos local.",
                "Cierre la aplicacion y vuelva a abrirla." + Environment.NewLine + Environment.NewLine
                    + "Si el problema sigue, borre el archivo de base y deje que se cree de nuevo:"
                    + Environment.NewLine + RutasSigem.ArchivoBaseDatos + Environment.NewLine + Environment.NewLine
                    + "Se perderian los datos de la demostracion, que se vuelven a sembrar solos.",
                ex.ToString());
        }
    }

    /// <summary>
    /// Comprueba que la carpeta de datos exista y se pueda escribir. Es el fallo
    /// mas probable en una maquina ajena y da un mensaje mucho mas util que
    /// dejar que SQLite reviente al abrir el archivo.
    /// </summary>
    private ResultadoDiagnostico? VerificarCarpetaDeDatos()
    {
        try
        {
            RutasSigem.Asegurar();

            var pruebita = Path.Combine(RutasSigem.CarpetaDatos, ".escritura");
            File.WriteAllText(pruebita, "ok");
            File.Delete(pruebita);

            _registro.LogInformation("Carpeta de datos verificada: {Carpeta}", RutasSigem.CarpetaDatos);
            return null;
        }
        catch (Exception ex)
        {
            _registro.LogError(ex, "No se puede escribir en la carpeta de datos {Carpeta}",
                RutasSigem.CarpetaDatos);

            return new ResultadoDiagnostico(
                EstadoDiagnostico.CarpetaNoEscribible,
                "No se puede escribir en la carpeta de datos",
                "RH Manager necesita esa carpeta para guardar su base de datos y sus registros, "
                    + "y el sistema no se lo permite.",
                "Verifique que el usuario de Windows tenga permiso de escritura sobre:"
                    + Environment.NewLine + RutasSigem.CarpetaDatos,
                ex.ToString());
        }
    }
}
