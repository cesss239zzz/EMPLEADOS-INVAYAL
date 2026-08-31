using System.Globalization;
using empleados.Configuracion;
using empleados.Datos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioRespaldos : IServicioRespaldos
{
    /// <summary>Prefijo de todo archivo de respaldo. Filtra la carpeta.</summary>
    private const string Prefijo = "rhmanager-";

    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly ILogger<ServicioRespaldos> _registro;

    public ServicioRespaldos(
        IDbContextFactory<ContextoRhManager> fabrica,
        ILogger<ServicioRespaldos> registro)
    {
        _fabrica = fabrica;
        _registro = registro;
    }

    /// <inheritdoc />
    public int DiasRetencion => 30;

    /// <inheritdoc />
    public string Carpeta => RutasRhManager.CarpetaRespaldos;

    /// <inheritdoc />
    public async Task<ResultadoRespaldo> RespaldarAsync(
        MotivoRespaldo motivo, CancellationToken cancelacion = default)
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _registro.LogError(ex, "No se pudo crear la carpeta de respaldos {Carpeta}.", Carpeta);
            return ResultadoRespaldo.Falla(
                "No se pudo crear la carpeta de respaldos. Verifique que tenga permiso sobre "
                + Carpeta + ".");
        }

        var destino = Path.Combine(Carpeta, NombreDeArchivo(DateTime.Now, motivo));

        try
        {
            await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

            // VACUUM INTO y no File.Copy: la aplicacion tiene la base abierta y
            // SQLite trabaja con archivos -wal y -shm aparte. Copiar el .db suelto
            // mientras hay escrituras en el diario da una copia incompleta que
            // parece valida hasta el dia que se necesita. VACUUM INTO le pide a
            // SQLite que escriba una base nueva, coherente y ya compactada.
            //
            // La ruta se interpola porque VACUUM INTO exige un literal; se
            // duplican las comillas simples, que es como se escapan en SQL.
            var rutaSql = destino.Replace("'", "''");

            await contexto.Database
                .ExecuteSqlRawAsync("VACUUM INTO '" + rutaSql + "'", cancelacion)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _registro.LogError(ex, "Fallo el respaldo de la base hacia {Destino}.", destino);
            LimpiarAMedias(destino);
            return ResultadoRespaldo.Falla(
                "No se pudo escribir el respaldo. El detalle quedó en el archivo de registro.");
        }

        // Un archivo de cero bytes seria un respaldo inservible que ademas
        // aparentaria estar bien en la lista.
        var info = new FileInfo(destino);
        if (!info.Exists || info.Length == 0)
        {
            LimpiarAMedias(destino);
            _registro.LogError("El respaldo {Destino} quedó vacío; se descarta.", destino);
            return ResultadoRespaldo.Falla("El respaldo quedó vacío y se descartó.");
        }

        _registro.LogInformation("Respaldo {Motivo} creado: {Archivo} ({Bytes} bytes).",
            motivo, destino, info.Length);

        return ResultadoRespaldo.Ok(destino);
    }

    /// <inheritdoc />
    public async Task<ResultadoRespaldo?> RespaldarSiTocaAsync(CancellationToken cancelacion = default)
    {
        var respaldos = await ListarAsync(cancelacion).ConfigureAwait(false);

        // Basta con que exista uno de hoy, sea manual o automatico: el objetivo
        // es tener una foto diaria, no acumular una por cada arranque.
        if (respaldos.Any(r => r.Momento.Date == DateTime.Today))
        {
            _registro.LogInformation("Ya hay un respaldo de hoy; no se crea otro.");
            await PurgarAntiguosAsync(cancelacion).ConfigureAwait(false);
            return null;
        }

        var resultado = await RespaldarAsync(MotivoRespaldo.Diario, cancelacion).ConfigureAwait(false);
        await PurgarAntiguosAsync(cancelacion).ConfigureAwait(false);
        return resultado;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LineaRespaldo>> ListarAsync(CancellationToken cancelacion = default)
    {
        // Trabajo de disco: fuera del hilo de interfaz.
        return Task.Run<IReadOnlyList<LineaRespaldo>>(() =>
        {
            if (!Directory.Exists(Carpeta))
            {
                return Array.Empty<LineaRespaldo>();
            }

            var lista = new List<LineaRespaldo>();

            foreach (var ruta in Directory.EnumerateFiles(Carpeta, Prefijo + "*.db"))
            {
                var info = new FileInfo(ruta);
                var (momento, motivo) = Interpretar(info);
                lista.Add(new LineaRespaldo(ruta, info.Name, momento, motivo, info.Length));
            }

            return lista.OrderByDescending(r => r.Momento).ToList();
        }, cancelacion);
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> RestaurarAsync(
        string archivoRespaldo, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(archivoRespaldo) || !File.Exists(archivoRespaldo))
        {
            return ResultadoGuardado.Falla("El archivo de respaldo ya no existe.");
        }

        // Se comprueba que sea una base SQLite legible ANTES de tocar nada. Pisar
        // la base buena con un archivo corrupto seria el peor final posible para
        // una funcion que existe justamente para no perder datos.
        var revision = await RevisarRespaldoAsync(archivoRespaldo, cancelacion).ConfigureAwait(false);
        if (revision is not null)
        {
            return ResultadoGuardado.Falla(revision);
        }

        // Red de seguridad: el estado actual se guarda antes de reemplazarlo, asi
        // que restaurar el respaldo equivocado tambien tiene vuelta atras.
        var previo = await RespaldarAsync(MotivoRespaldo.PrevioRestauracion, cancelacion)
            .ConfigureAwait(false);

        if (!previo.Exito)
        {
            return ResultadoGuardado.Falla(
                "No se pudo respaldar el estado actual, así que no se restauró nada. "
                + (previo.Error ?? string.Empty));
        }

        var destino = RutasRhManager.ArchivoBaseDatos;

        try
        {
            // Sin esto, el grupo de conexiones de SQLite conserva descriptores
            // abiertos sobre el archivo y Windows no deja reemplazarlo.
            SqliteConnection.ClearAllPools();

            await Task.Run(() =>
            {
                File.Copy(archivoRespaldo, destino, overwrite: true);

                // Los diarios del estado anterior ya no corresponden a esta base:
                // dejarlos haria que SQLite reaplique escrituras de otro archivo.
                BorrarSiExiste(destino + "-wal");
                BorrarSiExiste(destino + "-shm");
            }, cancelacion).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _registro.LogError(ex, "Fallo la restauración desde {Archivo}.", archivoRespaldo);
            return ResultadoGuardado.Falla(
                "No se pudo reemplazar la base. Su información quedó intacta y el respaldo "
                + "previo está en " + previo.Archivo);
        }

        _registro.LogWarning(
            "Base RESTAURADA desde {Archivo}. El estado anterior quedó en {Previo}.",
            archivoRespaldo, previo.Archivo);

        return ResultadoGuardado.Ok(0);
    }

    /// <summary>
    /// Abre el respaldo y comprueba que sea una base de RH Manager legible.
    /// Devuelve nulo si esta bien, o el motivo del rechazo.
    /// </summary>
    private async Task<string?> RevisarRespaldoAsync(string archivo, CancellationToken cancelacion)
    {
        try
        {
            var constructor = new SqliteConnectionStringBuilder
            {
                DataSource = archivo,
                Mode = SqliteOpenMode.ReadOnly
            };

            await using var conexion = new SqliteConnection(constructor.ToString());
            await conexion.OpenAsync(cancelacion).ConfigureAwait(false);

            await using var orden = conexion.CreateCommand();

            // La tabla de usuarios es la unica que nunca puede faltar: sin ella
            // no habria forma de entrar al sistema restaurado.
            orden.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'usuario'";

            var tablas = Convert.ToInt64(
                await orden.ExecuteScalarAsync(cancelacion).ConfigureAwait(false) ?? 0L,
                CultureInfo.InvariantCulture);

            if (tablas == 0)
            {
                return "Ese archivo no es un respaldo de RH Manager: no contiene sus tablas.";
            }

            return null;
        }
        catch (Exception ex)
        {
            _registro.LogError(ex, "El respaldo {Archivo} no se pudo abrir para revisarlo.", archivo);
            return "El archivo de respaldo está dañado o no se puede leer. No se restauró nada.";
        }
    }

    /// <inheritdoc />
    public Task<int> PurgarAntiguosAsync(CancellationToken cancelacion = default)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(Carpeta))
            {
                return 0;
            }

            var limite = DateTime.Today.AddDays(-DiasRetencion);
            var archivos = Directory.EnumerateFiles(Carpeta, Prefijo + "*.db")
                .Select(r => new FileInfo(r))
                .Select(i => (Info: i, Datos: Interpretar(i)))
                .OrderByDescending(x => x.Datos.momento)
                .ToList();

            var borrados = 0;

            foreach (var (info, datos) in archivos)
            {
                if (datos.momento.Date >= limite)
                {
                    continue;
                }

                // Nunca se deja la carpeta vacia. Un respaldo viejo sigue siendo
                // infinitamente mejor que ninguno.
                if (archivos.Count - borrados <= 1)
                {
                    break;
                }

                try
                {
                    info.Delete();
                    borrados++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _registro.LogWarning(ex, "No se pudo borrar el respaldo vencido {Archivo}.", info.Name);
                }
            }

            if (borrados > 0)
            {
                _registro.LogInformation(
                    "Purga de respaldos: {Borrados} archivo(s) con más de {Dias} días.",
                    borrados, DiasRetencion);
            }

            return borrados;
        }, cancelacion);
    }

    /// <summary>Nombre con fecha, hora y motivo. Ordena bien alfabéticamente.</summary>
    private static string NombreDeArchivo(DateTime momento, MotivoRespaldo motivo)
    {
        var sufijo = motivo switch
        {
            MotivoRespaldo.Diario => "diario",
            MotivoRespaldo.Manual => "manual",
            MotivoRespaldo.PrevioRestauracion => "previo-restauracion",
            _ => "respaldo"
        };

        return Prefijo
            + momento.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + "-" + sufijo + ".db";
    }

    /// <summary>
    /// Saca fecha y motivo del nombre. Si el nombre no encaja —alguien renombro
    /// el archivo— se cae a la fecha de escritura, que siempre existe.
    /// </summary>
    private static (DateTime momento, MotivoRespaldo motivo) Interpretar(FileInfo info)
    {
        var nombre = Path.GetFileNameWithoutExtension(info.Name);
        var partes = nombre.Split('-');

        var motivo = nombre.EndsWith("previo-restauracion", StringComparison.Ordinal)
            ? MotivoRespaldo.PrevioRestauracion
            : nombre.EndsWith("manual", StringComparison.Ordinal)
                ? MotivoRespaldo.Manual
                : MotivoRespaldo.Diario;

        if (partes.Length >= 3
            && DateTime.TryParseExact(
                partes[1] + partes[2], "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var momento))
        {
            return (momento, motivo);
        }

        return (info.LastWriteTime, motivo);
    }

    private void LimpiarAMedias(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _registro.LogWarning(ex, "No se pudo borrar el respaldo incompleto {Archivo}.", ruta);
        }
    }

    private static void BorrarSiExiste(string ruta)
    {
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }
}
