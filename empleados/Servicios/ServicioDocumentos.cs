using empleados.Datos;
using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioDocumentos : IServicioDocumentos
{
    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioDocumentos> _registro;

    public ServicioDocumentos(
        IDbContextFactory<ContextoRhManager> fabrica,
        IContextoEmpresa contextoEmpresa,
        SesionUsuario sesion,
        ILogger<ServicioDocumentos> registro)
    {
        _fabrica = fabrica;
        _contextoEmpresa = contextoEmpresa;
        _sesion = sesion;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VigenciaTipoDocumento>> ObtenerTiposAsync(
        CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        return await contexto.TiposDocumento
            .Where(t => t.Activo)
            .OrderBy(t => t.Nombre)
            .Select(t => new VigenciaTipoDocumento(t.Id, t.Nombre, t.RequiereVencimiento, t.MesesVigencia))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilaDocumento>> ObtenerDeColaboradorAsync(
        int colaboradorId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // La proyeccion NO toca contenido_documento: listar diez documentos no
        // puede costar diez escaneos en memoria.
        return await contexto.Documentos
            .Where(d => d.ColaboradorId == colaboradorId)
            .OrderBy(d => d.TipoDocumento!.Nombre)
            .Select(d => new FilaDocumento(
                d.Id,
                d.ColaboradorId,
                d.TipoDocumento!.Nombre,
                d.NombreArchivo,
                d.Extension,
                d.Descripcion,
                d.FechaEmision,
                d.FechaVencimiento,
                d.TamanoBytes,
                d.TipoDocumento.DiasAvisoAnticipado))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<FilaDocumento?> ObtenerAsync(int documentoId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        return await contexto.Documentos
            .Where(d => d.Id == documentoId)
            .Select(d => new FilaDocumento(
                d.Id,
                d.ColaboradorId,
                d.TipoDocumento!.Nombre,
                d.NombreArchivo,
                d.Extension,
                d.Descripcion,
                d.FechaEmision,
                d.FechaVencimiento,
                d.TamanoBytes,
                d.TipoDocumento.DiasAvisoAnticipado))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<byte[]?> ObtenerContenidoAsync(
        int documentoId, bool registrarDescarga = false, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var bytes = await contexto.ContenidosDocumento
            .Where(c => c.DocumentoId == documentoId)
            .Select(c => c.Bytes)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (bytes is null)
        {
            _registro.LogWarning("Se pidió el contenido del documento {Id} y no está.", documentoId);
            return null;
        }

        if (registrarDescarga)
        {
            var documento = await contexto.Documentos
                .Where(d => d.Id == documentoId)
                .Select(d => new { d.ColaboradorId, d.NombreArchivo })
                .FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false);

            if (documento is not null)
            {
                Anotar(contexto, documento.ColaboradorId, documentoId, documento.NombreArchivo,
                    AccionDocumento.Descarga, "Se guardó una copia fuera del sistema.");
                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            }
        }

        return bytes;
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> GuardarAsync(
        DatosDocumento datos, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ExigirEmpresaActiva();

        if (!_sesion.PuedeCapturar)
        {
            return ResultadoGuardado.Falla("Su perfil no tiene permiso para administrar documentos.");
        }

        byte[]? bytes = null;
        var nombreArchivo = string.Empty;
        var extension = string.Empty;

        // El archivo se lee y se valida ANTES de tocar la base: si algo no
        // cuadra, no queda una fila a medio guardar.
        if (datos.ReemplazaArchivo)
        {
            var lectura = await LeerArchivoAsync(datos.RutaOrigen, cancelacion).ConfigureAwait(false);
            if (lectura.Error is not null)
            {
                return ResultadoGuardado.Falla(lectura.Error);
            }

            bytes = lectura.Bytes;
            nombreArchivo = Path.GetFileName(datos.RutaOrigen);
            extension = Path.GetExtension(datos.RutaOrigen).ToLowerInvariant();
        }
        else if (datos.EsAlta)
        {
            return ResultadoGuardado.Falla("Elija el archivo que quiere adjuntar.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var tipo = await contexto.TiposDocumento
            .Where(t => t.Id == datos.TipoDocumentoId)
            .Select(t => new { t.Id, t.Nombre, t.RequiereVencimiento })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (tipo is null)
        {
            return ResultadoGuardado.Falla("Seleccione un tipo de documento válido.");
        }

        // Un tipo que no vence no guarda vencimiento aunque venga uno en el
        // formulario: seria un dato fantasma que despues genera alertas (CR-10).
        var vencimiento = tipo.RequiereVencimiento ? datos.FechaVencimiento : null;

        if (datos.FechaEmision == default || (tipo.RequiereVencimiento && vencimiento is null))
        {
            return ResultadoGuardado.Falla("Indique la fecha de emisión y el vencimiento cuando el tipo lo requiera.");
        }
        if ((datos.Descripcion?.Trim().Length ?? 0) > 500)
            return ResultadoGuardado.Falla("La descripción admite hasta 500 caracteres.");

        if (vencimiento is { } vence && vence.Date < datos.FechaEmision.Date)
        {
            return ResultadoGuardado.Falla(
                "El vencimiento no puede ser anterior a la fecha de emisión.");
        }

        DocumentoDigitalizado documento;
        AccionDocumento accion;
        string detalle;

        if (datos.EsAlta)
        {
            var colaboradorExiste = await contexto.Colaboradores
                .AnyAsync(c => c.Id == datos.ColaboradorId, cancelacion).ConfigureAwait(false);

            if (!colaboradorExiste)
            {
                return ResultadoGuardado.Falla("El colaborador no está disponible en la empresa activa.");
            }

            documento = new DocumentoDigitalizado
            {
                EmpresaId = _contextoEmpresa.EmpresaActivaId,
                FechaCreacion = DateTime.UtcNow,
                ColaboradorId = datos.ColaboradorId
            };

            contexto.Documentos.Add(documento);
            accion = AccionDocumento.Subida;
            detalle = "Se adjuntó " + nombreArchivo + ".";
        }
        else
        {
            var existente = await contexto.Documentos
                .FirstOrDefaultAsync(d => d.Id == datos.Id, cancelacion)
                .ConfigureAwait(false);

            if (existente is null)
            {
                return ResultadoGuardado.Falla("El documento ya no está en el expediente.");
            }

            documento = existente;
            accion = datos.ReemplazaArchivo ? AccionDocumento.Reemplazo : AccionDocumento.Edicion;
            detalle = datos.ReemplazaArchivo
                ? "El archivo pasó a ser " + nombreArchivo + "."
                : "Se corrigieron los datos del documento.";
        }

        documento.TipoDocumentoId = tipo.Id;
        documento.FechaEmision = datos.FechaEmision;
        documento.FechaVencimiento = vencimiento;
        documento.Descripcion = string.IsNullOrWhiteSpace(datos.Descripcion)
            ? null
            : datos.Descripcion.Trim();

        if (bytes is not null)
        {
            documento.NombreArchivo = nombreArchivo;
            documento.Extension = extension;
            documento.TamanoBytes = bytes.LongLength;
        }

        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);
        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

            if (bytes is not null)
            {
                var contenido = await contexto.ContenidosDocumento
                    .FirstOrDefaultAsync(c => c.DocumentoId == documento.Id, cancelacion)
                    .ConfigureAwait(false);

                if (contenido is null)
                {
                    contexto.ContenidosDocumento.Add(new ContenidoDocumento
                    {
                        EmpresaId = _contextoEmpresa.EmpresaActivaId,
                        FechaCreacion = DateTime.UtcNow,
                        DocumentoId = documento.Id,
                        Bytes = bytes
                    });
                }
                else
                {
                    contenido.Bytes = bytes;
                }
            }

            // Una renovación o corrección invalida los avisos de la fecha anterior.
            await contexto.Avisos
                .Where(a => a.Tipo == TipoAviso.VencimientoDocumento
                    && a.Estado == EstadoAviso.Pendiente
                    && a.ClaveIdempotencia.StartsWith("documento:" + documento.Id + ":")
                    && (vencimiento == null || a.FechaReferencia != vencimiento))
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(a => a.Estado, EstadoAviso.Resuelto), cancelacion)
                .ConfigureAwait(false);
            Anotar(contexto, documento.ColaboradorId, documento.Id, documento.NombreArchivo, accion, detalle);
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _registro.LogError(ex, "La base rechazó el guardado del documento {Nombre}.", nombreArchivo);
            return ResultadoGuardado.Falla("La base de datos rechazó el documento.");
        }

        _registro.LogInformation(
            "Documento {Id} ({Accion}) del colaborador {Colaborador}: {Nombre}, {Bytes} bytes.",
            documento.Id, accion, documento.ColaboradorId, documento.NombreArchivo, documento.TamanoBytes);

        return ResultadoGuardado.Ok(documento.Id);
    }

    /// <inheritdoc />
    public async Task<ResultadoGuardado> EliminarAsync(
        int documentoId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        if (!_sesion.PuedeCapturar)
        {
            return ResultadoGuardado.Falla("Su perfil no tiene permiso para eliminar documentos.");
        }

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        var documento = await contexto.Documentos
            .FirstOrDefaultAsync(d => d.Id == documentoId, cancelacion)
            .ConfigureAwait(false);

        if (documento is null)
        {
            return ResultadoGuardado.Falla("El documento ya no está en el expediente.");
        }

        var colaboradorId = documento.ColaboradorId;
        var nombre = documento.NombreArchivo;

        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);

        // Los avisos que colgaban de este documento dejan de tener sentido.
        await contexto.Avisos
            .Where(a => a.Tipo == TipoAviso.VencimientoDocumento
                && a.ClaveIdempotencia.StartsWith("documento:" + documentoId + ":"))
            .ExecuteDeleteAsync(cancelacion)
            .ConfigureAwait(false);

        // El contenido se va en cascada; el historial NO, y por eso la anotación
        // se hace con DocumentoId nulo: el rastro sobrevive al documento.
        contexto.Documentos.Remove(documento);
        Anotar(contexto, colaboradorId, null, nombre, AccionDocumento.Eliminacion,
            "Se eliminó el documento del expediente.");

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

        _registro.LogWarning("Documento {Id} ({Nombre}) eliminado por {Usuario}.",
            documentoId, nombre, _sesion.NombreUsuario);

        return ResultadoGuardado.Ok(documentoId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LineaHistorialDocumento>> ObtenerHistorialAsync(
        int colaboradorId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        return await contexto.MovimientosDocumento
            .Where(m => m.ColaboradorId == colaboradorId)
            .OrderByDescending(m => m.Fecha)
            .Take(100)
            .Select(m => new LineaHistorialDocumento(
                m.NombreDocumento, m.Accion, m.Fecha, m.NombreUsuario, m.Detalle))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Lee el archivo del disco y comprueba tamaño y extensión (CR-09).
    /// Devuelve el motivo del rechazo en vez de una excepción: que alguien elija
    /// un archivo demasiado grande es un caso previsto, no un fallo.
    /// </summary>
    private async Task<(byte[]? Bytes, string? Error)> LeerArchivoAsync(
        string ruta, CancellationToken cancelacion)
    {
        if (!File.Exists(ruta))
        {
            return (null, "No se encontró el archivo seleccionado.");
        }

        var extension = Path.GetExtension(ruta).ToLowerInvariant();

        if (!LimitesDocumento.Extensiones.Contains(extension))
        {
            return (null, "Ese tipo de archivo no se admite. Solo se aceptan "
                + LimitesDocumento.ExtensionesTexto + ".");
        }

        var info = new FileInfo(ruta);

        if (info.Length == 0)
        {
            return (null, "El archivo está vacío.");
        }

        if (info.Length > LimitesDocumento.MaximoBytes)
        {
            return (null, "El archivo pesa " + (info.Length / (1024d * 1024d)).ToString("0.#")
                + " MB y el máximo es " + LimitesDocumento.MaximoTexto
                + ". Escanéelo con menos resolución o divídalo.");
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(ruta, cancelacion).ConfigureAwait(false);
            return (bytes, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _registro.LogError(ex, "No se pudo leer el archivo {Ruta}.", ruta);
            return (null, "No se pudo leer el archivo. Verifique que no esté abierto en otro programa.");
        }
    }

    /// <summary>Agrega un renglón al historial. No guarda: lo hace quien llama.</summary>
    private void Anotar(
        ContextoRhManager contexto,
        int colaboradorId,
        int? documentoId,
        string nombreDocumento,
        AccionDocumento accion,
        string detalle)
    {
        contexto.MovimientosDocumento.Add(new MovimientoDocumento
        {
            EmpresaId = _contextoEmpresa.EmpresaActivaId,
            FechaCreacion = DateTime.UtcNow,
            ColaboradorId = colaboradorId,
            DocumentoId = documentoId,
            NombreDocumento = nombreDocumento,
            Accion = accion,
            Fecha = DateTime.UtcNow,
            UsuarioId = _sesion.UsuarioId,
            NombreUsuario = _sesion.NombreCompleto,
            Detalle = detalle
        });
    }

    private void ExigirEmpresaActiva()
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException("No se pueden gestionar documentos sin empresa activa.");
        }
    }
}
