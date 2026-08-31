using System.Globalization;
using ClosedXML.Excel;
using empleados.Configuracion;
using empleados.Datos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// MAUI tambien define un IContainer; en este archivo IContainer es siempre el de QuestPDF.
using IContainer = QuestPDF.Infrastructure.IContainer;

namespace empleados.Servicios;

/// <inheritdoc />
public sealed class ServicioReportes : IServicioReportes
{
    // Colores del sistema visual de RH Manager, para que el reporte y la pantalla
    // se vean de la misma familia.
    private const string ColorPrimario = "#0453CD";
    private const string ColorTinta = "#0A2540";
    private const string ColorLinea = "#E1E4E8";
    private const string ColorSuave = "#5B6B7A";
    private const string ColorFranja = "#F1F4F9";

    private readonly IServicioColaboradores _colaboradores;
    private readonly IServicioFicha _ficha;
    private readonly IContextoEmpresa _contextoEmpresa;
    private readonly IDbContextFactory<ContextoRhManager> _fabrica;
    private readonly SesionUsuario _sesion;
    private readonly ILogger<ServicioReportes> _registro;

    public ServicioReportes(
        IServicioColaboradores colaboradores,
        IServicioFicha ficha,
        IContextoEmpresa contextoEmpresa,
        IDbContextFactory<ContextoRhManager> fabrica,
        SesionUsuario sesion,
        ILogger<ServicioReportes> registro)
    {
        _colaboradores = colaboradores;
        _ficha = ficha;
        _contextoEmpresa = contextoEmpresa;
        _fabrica = fabrica;
        _sesion = sesion;
        _registro = registro;
    }

    // ─── Listado de colaboradores ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string> ExportarColaboradoresAsync(
        FiltroColaboradores filtro,
        FormatoReporte formato,
        CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        var filas = await _colaboradores.ObtenerAsync(filtro, cancelacion).ConfigureAwait(false);
        var empresa = await ObtenerEmpresaAsync(cancelacion).ConfigureAwait(false);
        var verSalario = _sesion.PuedeVerSalarios;

        // La generacion del archivo es trabajo de CPU y disco: fuera del hilo de
        // interfaz para no congelar la ventana.
        var ruta = formato == FormatoReporte.Excel
            ? await Task.Run(() => GenerarExcelColaboradores(filas, empresa, verSalario), cancelacion).ConfigureAwait(false)
            : await Task.Run(() => GenerarPdfColaboradores(filas, empresa, verSalario), cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Directorio exportado ({Formato}, {Cantidad} filas) a {Ruta}", formato, filas.Count, ruta);

        return ruta;
    }

    private static string GenerarExcelColaboradores(
        IReadOnlyList<FilaColaborador> filas, DatosEmpresa empresa, bool verSalario)
    {
        var ruta = RutaNueva("Colaboradores", ".xlsx");
        var columnas = verSalario ? 9 : 8;

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Colaboradores");

        hoja.Cell(1, 1).Value = empresa.Nombre;
        hoja.Range(1, 1, 1, columnas).Merge();
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 15;

        hoja.Cell(2, 1).Value = "Directorio de colaboradores  ·  Generado el "
            + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        hoja.Range(2, 1, 2, columnas).Merge();
        hoja.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml(ColorSuave);

        var encabezados = new List<string>
        {
            "Código", "Nombre completo", "Identidad", "Puesto", "Departamento", "Sucursal", "Ingreso"
        };
        if (verSalario)
        {
            encabezados.Add("Salario base");
        }

        encabezados.Add("Estado");

        const int filaEncabezado = 4;
        for (var i = 0; i < encabezados.Count; i++)
        {
            var celda = hoja.Cell(filaEncabezado, i + 1);
            celda.Value = encabezados[i];
            celda.Style.Font.Bold = true;
            celda.Style.Font.FontColor = XLColor.White;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml(ColorPrimario);
        }

        var fila = filaEncabezado + 1;
        foreach (var c in filas)
        {
            var col = 1;
            hoja.Cell(fila, col++).Value = c.Codigo;
            hoja.Cell(fila, col++).Value = c.NombreCompleto;
            hoja.Cell(fila, col++).Value = c.IdentidadTexto;
            hoja.Cell(fila, col++).Value = c.PuestoTexto;
            hoja.Cell(fila, col++).Value = c.DepartamentoTexto;
            hoja.Cell(fila, col++).Value = c.SucursalTexto;

            var celdaIngreso = hoja.Cell(fila, col++);
            celdaIngreso.Value = c.FechaIngreso;
            celdaIngreso.Style.DateFormat.Format = "dd/MM/yyyy";

            if (verSalario)
            {
                var celdaSalario = hoja.Cell(fila, col++);
                if (c.Salario is { } salario)
                {
                    celdaSalario.Value = salario;
                    celdaSalario.Style.NumberFormat.Format = "#,##0.00";
                }
            }

            hoja.Cell(fila, col).Value = c.EstadoTexto;
            fila++;
        }

        hoja.Columns().AdjustToContents();
        hoja.SheetView.FreezeRows(filaEncabezado);
        libro.SaveAs(ruta);

        return ruta;
    }

    private static string GenerarPdfColaboradores(
        IReadOnlyList<FilaColaborador> filas, DatosEmpresa empresa, bool verSalario)
    {
        var ruta = RutaNueva("Colaboradores", ".pdf");

        Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4.Landscape());
                pagina.Margin(28);
                pagina.DefaultTextStyle(t => t.FontSize(9).FontColor(ColorTinta));

                Encabezado(pagina.Header(), empresa, "Directorio de colaboradores");

                pagina.Content().PaddingVertical(12).Table(tabla =>
                {
                    tabla.ColumnsDefinition(columnas =>
                    {
                        columnas.ConstantColumn(60);    // Codigo
                        columnas.RelativeColumn(3);      // Nombre
                        columnas.RelativeColumn(2);      // Identidad
                        columnas.RelativeColumn(2.4f);   // Puesto
                        columnas.RelativeColumn(2);      // Departamento
                        columnas.RelativeColumn(2);      // Sucursal
                        columnas.ConstantColumn(66);     // Ingreso
                        if (verSalario)
                        {
                            columnas.ConstantColumn(78); // Salario
                        }

                        columnas.ConstantColumn(64);     // Estado
                    });

                    tabla.Header(encabezado =>
                    {
                        CeldaEncabezado(encabezado, "Código");
                        CeldaEncabezado(encabezado, "Nombre completo");
                        CeldaEncabezado(encabezado, "Identidad");
                        CeldaEncabezado(encabezado, "Puesto");
                        CeldaEncabezado(encabezado, "Departamento");
                        CeldaEncabezado(encabezado, "Sucursal");
                        CeldaEncabezado(encabezado, "Ingreso");
                        if (verSalario)
                        {
                            CeldaEncabezado(encabezado, "Salario");
                        }

                        CeldaEncabezado(encabezado, "Estado");
                    });

                    var alterna = false;
                    foreach (var c in filas)
                    {
                        var fondo = alterna ? ColorFranja : "#FFFFFF";
                        alterna = !alterna;

                        Celda(tabla, fondo, c.Codigo);
                        Celda(tabla, fondo, c.NombreCompleto);
                        Celda(tabla, fondo, c.IdentidadTexto);
                        Celda(tabla, fondo, c.PuestoTexto);
                        Celda(tabla, fondo, c.DepartamentoTexto);
                        Celda(tabla, fondo, c.SucursalTexto);
                        Celda(tabla, fondo, c.FechaIngresoTexto);
                        if (verSalario)
                        {
                            Celda(tabla, fondo, c.SalarioTexto);
                        }

                        Celda(tabla, fondo, c.EstadoTexto);
                    }
                });

                Pie(pagina.Footer(), filas.Count + " colaborador(es)");
            });
        }).GeneratePdf(ruta);

        return ruta;
    }

    // ─── Ficha del colaborador ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string> ExportarFichaPdfAsync(int colaboradorId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        var detalle = await _ficha.ObtenerAsync(colaboradorId, cancelacion).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El colaborador no existe en la empresa activa.");
        var empresa = await ObtenerEmpresaAsync(cancelacion).ConfigureAwait(false);

        var ruta = await Task.Run(() => GenerarPdfFicha(detalle, empresa), cancelacion).ConfigureAwait(false);
        _registro.LogInformation("Ficha {Codigo} exportada a {Ruta}", detalle.Codigo, ruta);
        return ruta;
    }

    private static string GenerarPdfFicha(DetalleColaborador d, DatosEmpresa empresa)
    {
        var ruta = RutaNueva("Ficha_" + Sanitizar(d.Codigo), ".pdf");

        Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(34);
                pagina.DefaultTextStyle(t => t.FontSize(10).FontColor(ColorTinta));

                Encabezado(pagina.Header(), empresa, "Ficha del colaborador");

                pagina.Content().PaddingVertical(14).Column(columna =>
                {
                    columna.Spacing(4);
                    columna.Item().Text(d.NombreCompleto).FontSize(18).Bold().FontColor(ColorPrimario);
                    // Solo se unen las partes que existen: con los tres catalogos
                    // opcionales (CR-05), concatenar a ciegas dejaria una linea
                    // de separadores sueltos.
                    var ubicacion = string.Join("  ·  ",
                        new[] { d.Puesto, d.Departamento, d.Sucursal }
                            .Where(parte => !string.IsNullOrWhiteSpace(parte)));

                    if (ubicacion.Length > 0)
                    {
                        columna.Item().Text(ubicacion).FontSize(10).FontColor(ColorSuave);
                    }

                    Seccion(columna.Item().PaddingTop(14), "Información personal");
                    Rejilla(columna.Item(),
                    [
                        ("Código de expediente", d.Codigo),
                        ("Número de identidad", d.IdentidadTexto),
                        ("Sexo", d.SexoTexto),
                        ("Fecha de nacimiento", d.NacimientoTexto),
                        ("Edad", d.EdadTexto),
                        ("Estado", d.EstadoTexto)
                    ]);

                    Seccion(columna.Item().PaddingTop(10), "Información laboral");
                    Rejilla(columna.Item(),
                    [
                        ("Fecha de ingreso", d.IngresoTexto),
                        ("Antigüedad", d.AntiguedadTexto),
                        ("Salario base", d.SalarioTexto),
                        ("Puesto", d.PuestoTexto),
                        ("Departamento", d.DepartamentoTexto),
                        ("Sucursal", d.SucursalTexto)
                    ]);

                    Seccion(columna.Item().PaddingTop(10), "Contacto");
                    Rejilla(columna.Item(),
                    [
                        ("Teléfono", d.TelefonoTexto),
                        ("Correo electronico", d.CorreoTexto),
                        ("Dirección", d.DireccionTexto)
                    ]);
                });

                Pie(pagina.Footer(), "Expediente " + d.Codigo);
            });
        }).GeneratePdf(ruta);

        return ruta;
    }

    // ─── Constancia de trabajo ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string> GenerarConstanciaPdfAsync(int colaboradorId, CancellationToken cancelacion = default)
    {
        ExigirEmpresaActiva();

        var detalle = await _ficha.ObtenerAsync(colaboradorId, cancelacion).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El colaborador no existe en la empresa activa.");
        var empresa = await ObtenerEmpresaAsync(cancelacion).ConfigureAwait(false);

        var ruta = await Task.Run(() => GenerarPdfConstancia(detalle, empresa), cancelacion).ConfigureAwait(false);
        _registro.LogInformation("Constancia de {Codigo} generada en {Ruta}", detalle.Codigo, ruta);
        return ruta;
    }

    private static string GenerarPdfConstancia(DetalleColaborador d, DatosEmpresa empresa)
    {
        var ruta = RutaNueva("Constancia_" + Sanitizar(d.Codigo), ".pdf");
        var hoy = DateTime.Today;

        // La constancia es un documento que sale de la empresa, asi que se redacta
        // con lo que hay: una frase que dijera "con identidad numero Sin registrar"
        // seria impresentable. Cada dato opcional agrega su clausula solo si existe.
        var cuerpo = "Por este medio se hace constar que " + d.NombreCompleto;

        if (!string.IsNullOrWhiteSpace(d.Identidad))
        {
            cuerpo += ", con tarjeta de identidad número " + d.Identidad;
        }

        cuerpo += ", labora para " + empresa.Nombre + " desde el " + d.IngresoTexto;

        if (!string.IsNullOrWhiteSpace(d.Puesto))
        {
            cuerpo += ", desempenando el cargo de " + d.Puesto;
        }

        if (!string.IsNullOrWhiteSpace(d.Departamento))
        {
            cuerpo += " en el departamento de " + d.Departamento;
        }

        if (!string.IsNullOrWhiteSpace(d.Sucursal))
        {
            cuerpo += ", en la sucursal " + d.Sucursal;
        }

        cuerpo += ".";

        if (d.Salario is { } salario)
        {
            cuerpo += " Devenga un salario base de L. " + salario.ToString("#,##0.00", CultureInfo.CurrentCulture)
                + " mensuales.";
        }

        var cierre = "Se extiende la presente constancia a solicitud de la persona interesada, para los "
            + "fines que estime convenientes, el "
            + hoy.ToString("dd 'de' MMMM 'de' yyyy", CultureInfo.CurrentCulture) + ".";

        Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(48);
                pagina.DefaultTextStyle(t => t.FontSize(12).FontColor(ColorTinta).LineHeight(1.5f));

                Encabezado(pagina.Header(), empresa, "Constancia de trabajo");

                pagina.Content().PaddingVertical(30).Column(columna =>
                {
                    columna.Spacing(22);

                    columna.Item().AlignCenter().Text("CONSTANCIA DE TRABAJO")
                        .FontSize(16).Bold().FontColor(ColorPrimario);

                    columna.Item().PaddingTop(10).Text(texto =>
                    {
                        texto.Justify();
                        texto.Span(cuerpo);
                    });

                    columna.Item().Text(texto =>
                    {
                        texto.Justify();
                        texto.Span(cierre);
                    });

                    columna.Item().PaddingTop(60).AlignCenter().Column(firma =>
                    {
                        firma.Item().Width(240).LineHorizontal(1).LineColor(ColorTinta);
                        firma.Item().AlignCenter().Text("Recursos Humanos").Bold();
                        firma.Item().AlignCenter().Text(empresa.Nombre).FontColor(ColorSuave).FontSize(10);
                    });
                });

                Pie(pagina.Footer(),
                    "Documento generado por RH Manager el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture));
            });
        }).GeneratePdf(ruta);

        return ruta;
    }

    // ─── Componentes compartidos de los PDF ─────────────────────────────────

    private static void Encabezado(IContainer contenedor, DatosEmpresa empresa, string titulo)
    {
        contenedor.BorderBottom(2).BorderColor(ColorPrimario).PaddingBottom(8).Row(fila =>
        {
            fila.RelativeItem().Column(columna =>
            {
                columna.Item().Text(empresa.Nombre).FontSize(15).Bold().FontColor(ColorTinta);
                if (!string.IsNullOrWhiteSpace(empresa.Rtn))
                {
                    columna.Item().Text("RTN: " + empresa.Rtn).FontSize(9).FontColor(ColorSuave);
                }

                if (!string.IsNullOrWhiteSpace(empresa.Direccion))
                {
                    columna.Item().Text(empresa.Direccion).FontSize(9).FontColor(ColorSuave);
                }
            });

            fila.ConstantItem(200).AlignRight().Column(columna =>
            {
                columna.Item().Text(titulo).FontSize(13).Bold().FontColor(ColorPrimario);
                columna.Item().Text("RH Manager · Expediente digital").FontSize(9).FontColor(ColorSuave);
            });
        });
    }

    private static void Pie(IContainer contenedor, string nota)
    {
        contenedor.BorderTop(1).BorderColor(ColorLinea).PaddingTop(6).Row(fila =>
        {
            fila.RelativeItem().AlignLeft().Text(nota).FontSize(8).FontColor(ColorSuave);
            fila.RelativeItem().AlignRight().Text(texto =>
            {
                texto.DefaultTextStyle(t => t.FontSize(8).FontColor(ColorSuave));
                texto.Span("Pagina ");
                texto.CurrentPageNumber();
                texto.Span(" de ");
                texto.TotalPages();
            });
        });
    }

    private static void CeldaEncabezado(TableCellDescriptor fila, string texto)
        => fila.Cell().Background(ColorPrimario).Padding(5).Text(texto).FontColor("#FFFFFF").Bold().FontSize(9);

    private static void Celda(TableDescriptor tabla, string fondo, string texto)
        => tabla.Cell().Background(fondo).BorderBottom(1).BorderColor(ColorLinea).Padding(5)
            .Text(texto ?? string.Empty).FontSize(9);

    private static void Seccion(IContainer contenedor, string titulo)
        => contenedor.BorderBottom(1).BorderColor(ColorLinea).PaddingBottom(4)
            .Text(titulo).FontSize(12).Bold().FontColor(ColorPrimario);

    private static void Rejilla(IContainer contenedor, IReadOnlyList<(string Etiqueta, string Valor)> campos)
        => contenedor.PaddingTop(8).Table(tabla =>
        {
            // Dos columnas que fluyen: el Grid clasico quedo obsoleto en QuestPDF.
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.RelativeColumn();
                columnas.RelativeColumn();
            });

            foreach (var (etiqueta, valor) in campos)
            {
                tabla.Cell().PaddingBottom(10).PaddingRight(12).Column(columna =>
                {
                    columna.Item().Text(etiqueta.ToUpperInvariant()).FontSize(8).FontColor(ColorSuave);
                    columna.Item().Text(string.IsNullOrWhiteSpace(valor) ? "———" : valor).FontSize(11);
                });
            }
        });

    // ─── Datos de la empresa activa ─────────────────────────────────────────

    private async Task<DatosEmpresa> ObtenerEmpresaAsync(CancellationToken cancelacion)
    {
        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion).ConfigureAwait(false);

        // Empresa NO lleva filtro global (ella misma es el inquilino): se busca
        // por el id de la empresa activa del contexto.
        var empresa = await contexto.Empresas
            .Where(e => e.Id == _contextoEmpresa.EmpresaActivaId)
            .Select(e => new DatosEmpresa(e.Nombre, e.Rtn, e.Direccion, e.Telefono))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return empresa ?? new DatosEmpresa(_contextoEmpresa.NombreEmpresaActiva, string.Empty, string.Empty, string.Empty);
    }

    private void ExigirEmpresaActiva()
    {
        if (!_contextoEmpresa.HayEmpresaActiva)
        {
            throw new InvalidOperationException("No se puede generar un reporte sin empresa activa.");
        }
    }

    private static string RutaNueva(string prefijo, string extension)
    {
        var nombre = Sanitizar(prefijo) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + extension;
        return Path.Combine(RutasRhManager.CarpetaExportaciones, nombre);
    }

    private static string Sanitizar(string texto)
    {
        var limpio = texto;
        foreach (var invalido in Path.GetInvalidFileNameChars())
        {
            limpio = limpio.Replace(invalido, '-');
        }

        return string.IsNullOrWhiteSpace(limpio) ? "reporte" : limpio;
    }

    /// <summary>Datos de cabecera de la empresa para los reportes.</summary>
    private sealed record DatosEmpresa(string Nombre, string Rtn, string Direccion, string Telefono);
}
