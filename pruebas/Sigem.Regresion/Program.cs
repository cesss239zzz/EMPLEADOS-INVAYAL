using empleados.Datos;
using empleados.Datos.Entidades;
using empleados.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

// Ejecuta los servicios REALES contra SQLite temporal; nunca abre la base del usuario.
var carpeta = Path.Combine(Path.GetTempPath(), "sigem-regresion-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpeta);
var pruebas = new (string Nombre, Func<Task> Ejecutar)[]
{
    ("Migraciones en base nueva", Migraciones),
    ("Aislamiento de empresas, sucursales y contenido", Aislamiento),
    ("DTO de edición no revela salario a Consulta", Permisos),
    ("Catálogos de otra empresa y datos inválidos rechazados", Validaciones),
    ("Historial laboral atómico y sin duplicar guardados sin cambios", Historial),
    ("Vacaciones: cruces, límites, canceladas y estado del colaborador", Vacaciones),
    ("Dashboard: próximos, vencidos y total registrado", Resumen),
    ("Motor: idempotencia, claves de contrato y renovación", Alertas),
    ("Documentos: rollback ante fallo del segundo guardado", Documentos),
    ("Cumpleaños pasado no se marca vencimiento crítico", Efemerides),
    ("Respaldos globales rechazados para Administrador y Consulta", PermisosRespaldos)
};
int fallos = 0;
try
{
    foreach (var prueba in pruebas)
    {
        try { await prueba.Ejecutar(); Console.WriteLine("OK: " + prueba.Nombre); }
        catch (Exception ex) { fallos++; Console.WriteLine("FALLO: " + prueba.Nombre + "\n" + ex); }
    }
    Console.WriteLine($"Resultado: {pruebas.Length - fallos}/{pruebas.Length} grupos correctos.");
    Environment.ExitCode = fallos == 0 ? 0 : 1;
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(carpeta, recursive: true);
}

void Exigir(bool condicion, string motivo)
{
    if (!condicion) throw new InvalidOperationException(motivo);
}

async Task<Entorno> Preparar(FalloSegundoGuardado? fallo = null)
{
    var sesion = new SesionUsuario();
    sesion.Iniciar(new Usuario { Id = 1, NombreUsuario = "pruebas", Perfil = PerfilUsuario.SuperAdministrador });
    var empresa = new ContextoEmpresa();
    empresa.Establecer(101, "Pruebas A", "#000000");
    var constructor = new DbContextOptionsBuilder<ContextoRhManager>()
        .UseSqlite(new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(carpeta, Guid.NewGuid() + ".db"), Pooling = false }.ToString());
    if (fallo is not null) constructor.AddInterceptors(fallo);
    var fabrica = new Fabrica(constructor.Options, empresa, sesion);
    await using var db = fabrica.CreateDbContext();
    await db.Database.EnsureCreatedAsync();
    db.Empresas.AddRange(
        new Empresa { Id = 101, Nombre = "Pruebas A", NombreCorto = "A", Rtn = "A" },
        new Empresa { Id = 102, Nombre = "Pruebas B", NombreCorto = "B", Rtn = "B" });
    db.Sucursales.AddRange(
        new Sucursal { Id = 101, EmpresaId = 101, Codigo = "A1", Nombre = "A1" },
        new Sucursal { Id = 102, EmpresaId = 101, Codigo = "A2", Nombre = "A2" },
        new Sucursal { Id = 201, EmpresaId = 102, Codigo = "B1", Nombre = "B1" });
    db.Departamentos.Add(new Departamento { Id = 201, EmpresaId = 102, Nombre = "Otro departamento" });
    db.TiposDocumento.Add(new TipoDocumento { Id = 101, EmpresaId = 101, Nombre = "Prueba", RequiereVencimiento = true });
    db.TiposContrato.Add(new TipoContrato { Id = 101, EmpresaId = 101, Nombre = "Temporal" });
    db.Colaboradores.AddRange(Persona(101, 101, 101), Persona(102, 101, 102), Persona(201, 102, 201));
    await db.SaveChangesAsync();
    return new Entorno(fabrica, empresa, sesion);
}

Colaborador Persona(int id, int empresa, int sucursal) => new()
{
    Id = id, EmpresaId = empresa, SucursalId = sucursal, Codigo = "E" + id,
    PrimerNombre = "Persona", PrimerApellido = id.ToString(), FechaIngreso = DateTime.Today.AddYears(-2),
    Estado = EstadoColaborador.Activo, SalarioBase = 10000m
};
ServicioColaboradores Colaboradores(Entorno e) => new(e.Fabrica, e.Empresa, e.Sesion, NullLogger<ServicioColaboradores>.Instance);
ServicioDocumentos Docs(Entorno e) => new(e.Fabrica, e.Empresa, e.Sesion, NullLogger<ServicioDocumentos>.Instance);
ServicioAlertas Motor(Entorno e) => new(e.Fabrica, e.Empresa, NullLogger<ServicioAlertas>.Instance, e.Sesion);

async Task Migraciones()
{
    var sesion = new SesionUsuario();
    var empresa = new ContextoEmpresa();
    var opciones = new DbContextOptionsBuilder<ContextoRhManager>().UseSqlite(
        "Data Source=" + Path.Combine(carpeta, "migraciones.db") + ";Pooling=False").Options;
    await using var db = new ContextoRhManager(opciones, empresa, sesion);
    await db.Database.MigrateAsync();
    Exigir(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Quedan migraciones pendientes");
    Exigir(await db.Usuarios.AnyAsync(), "Falta el usuario inicial");
}

async Task Aislamiento()
{
    var e = await Preparar();
    await using (var db = e.Fabrica.CreateDbContext())
    {
        foreach (var id in new[] { 101, 102 })
        {
            db.Documentos.Add(new DocumentoDigitalizado { Id = id, EmpresaId = 101, ColaboradorId = id,
                TipoDocumentoId = 101, NombreArchivo = "prueba.pdf", Extension = ".pdf", FechaEmision = DateTime.Today,
                Contenido = new ContenidoDocumento { EmpresaId = 101, Bytes = [1, 2] } });
            db.Incidencias.Add(new Incidencia { EmpresaId = 101, ColaboradorId = id, Titulo = "Prueba" });
            db.MovimientosDocumento.Add(new MovimientoDocumento { EmpresaId = 101, ColaboradorId = id, NombreDocumento = "prueba" });
            db.Avisos.Add(new Aviso { EmpresaId = 101, ColaboradorId = id, ClaveIdempotencia = "prueba:" + id });
        }
        await db.SaveChangesAsync();
        Exigir(await db.Colaboradores.CountAsync() == 2, "La empresa A ve colaboradores de B");
    }
    e.Sesion.Iniciar(new Usuario { Id = 2, Perfil = PerfilUsuario.SupervisorSucursal, SucursalId = 101 });
    await using (var db = e.Fabrica.CreateDbContext())
    {
        Exigir(await db.Colaboradores.CountAsync() == 1, "Supervisor ve otra sucursal");
        Exigir(await db.Documentos.CountAsync() == 1 && await db.ContenidosDocumento.CountAsync() == 1, "Fuga en documentos/bytes");
        Exigir(await db.Incidencias.CountAsync() == 1 && await db.MovimientosDocumento.CountAsync() == 1, "Fuga en novedades/historial");
        Exigir(await db.Avisos.CountAsync() == 1, "Fuga en avisos");
    }
    Exigir(await Docs(e).ObtenerContenidoAsync(102) is null, "Puede descargar documento de otra sucursal");
    var filas = await Colaboradores(e).ObtenerAsync(new FiltroColaboradores(SucursalId: 102));
    Exigir(filas.Count == 0, "Filtro del usuario amplía permiso del supervisor");
    var ficha = new ServicioFicha(e.Fabrica, e.Empresa, e.Sesion, NullLogger<ServicioFicha>.Instance);
    Exigir(await ficha.ObtenerAsync(102) is null, "Acceso a ficha por ID elude sucursal");
    e.Sesion.Iniciar(new Usuario { Id = 2, Perfil = PerfilUsuario.SupervisorSucursal });
    await using (var db = e.Fabrica.CreateDbContext())
        Exigir(await db.Colaboradores.CountAsync() == 0, "Supervisor sin sucursal obtiene datos");
    e.Sesion.Cerrar();
    await using (var db = e.Fabrica.CreateDbContext())
        Exigir(await db.Colaboradores.CountAsync() == 0, "Sesión cerrada conserva acceso");
}

async Task Permisos()
{
    var e = await Preparar();
    e.Sesion.Iniciar(new Usuario { Id = 2, Perfil = PerfilUsuario.Consulta });
    Exigir(await Colaboradores(e).ObtenerParaEdicionAsync(101) is null, "Consulta obtiene DTO salarial");
    var filas = await Colaboradores(e).ObtenerAsync(new FiltroColaboradores());
    Exigir(filas.All(f => f.Salario is null), "Consulta recibe salario en directorio");
    Exigir(!(await Colaboradores(e).GuardarAsync(new DatosEdicionColaborador())).Exito, "Consulta puede guardar");
}

async Task Validaciones()
{
    var e = await Preparar(); var servicio = Colaboradores(e);
    var datos = (await servicio.ObtenerParaEdicionAsync(101))!;
    datos.DepartamentoId = 201;
    Exigir(!(await servicio.GuardarAsync(datos)).Exito, "Acepta catálogo de otra empresa");
    datos.DepartamentoId = null; datos.SalarioBase = -1;
    Exigir(!(await servicio.GuardarAsync(datos)).Exito, "Acepta salario negativo");
    datos.SalarioBase = 100; datos.PrimerNombre = " ";
    Exigir(!(await servicio.GuardarAsync(datos)).Exito, "Acepta nombre vacío desde el servicio");
}

async Task Historial()
{
    var e = await Preparar(); var servicio = Colaboradores(e);
    var alta = await servicio.GuardarAsync(new DatosEdicionColaborador
    { Codigo = "NUEVO", PrimerNombre = "Ana", PrimerApellido = "Prueba", FechaIngreso = DateTime.Today, SucursalId = 101 });
    Exigir(alta.Exito, alta.Error ?? "Alta falló");
    var datos = (await servicio.ObtenerParaEdicionAsync(alta.Id))!;
    datos.SalarioBase = 12345.678m; datos.SucursalId = 102; datos.Estado = EstadoColaborador.Suspendido;
    Exigir((await servicio.GuardarAsync(datos)).Exito, "Edición falló");
    Exigir((await servicio.GuardarAsync(datos)).Exito, "Segundo guardado falló");
    await using var db = e.Fabrica.CreateDbContext();
    var movimientos = await db.MovimientosLaborales.Where(m => m.ColaboradorId == alta.Id).ToListAsync();
    Exigir(movimientos.Count == 4, "Faltan movimientos o se duplican sin cambios");
    Exigir(movimientos.Single(m => m.Tipo == TipoMovimiento.CambioSalario).SalarioNuevo == 12345.68m, "Historial sin redondear");
    Exigir(movimientos.All(m => m.Observacion.Contains("pruebas")), "Historial sin autor");
}

async Task Vacaciones()
{
    var e = await Preparar();
    var servicio = new ServicioNovedades(e.Fabrica, e.Empresa, e.Sesion, NullLogger<ServicioNovedades>.Instance);
    var inicio = DateTime.Today.AddDays(10);
    Task<ResultadoGuardado> Guardar(int persona, DateTime desde, DateTime hasta) => servicio.ProgramarVacacionesAsync(
        new DatosVacaciones { ColaboradorId = persona, FechaInicio = desde, FechaFin = hasta });
    Exigir((await Guardar(101, inicio, inicio.AddDays(4))).Exito, "No crea período de 5 días");
    Exigir(!(await Guardar(101, inicio.AddDays(4), inicio.AddDays(6))).Exito, "Acepta solape en último día");
    Exigir(!(await Guardar(101, inicio.AddDays(-1), inicio.AddDays(6))).Exito, "Acepta período que contiene otro");
    Exigir((await Guardar(101, inicio.AddDays(5), inicio.AddDays(6))).Exito, "Rechaza días adyacentes libres");
    Exigir((await Guardar(102, inicio, inicio.AddDays(4))).Exito, "Impide vacaciones de distinta persona");
    await using (var db = e.Fabrica.CreateDbContext())
    {
        var v = await db.Vacaciones.FirstAsync(v => v.ColaboradorId == 101);
        Exigir(v.Dias == 5, "Cálculo inclusivo erróneo"); v.Estado = EstadoVacacion.Cancelada;
        (await db.Colaboradores.SingleAsync(c => c.Id == 102)).Estado = EstadoColaborador.Inactivo;
        await db.SaveChangesAsync();
    }
    Exigir((await Guardar(101, inicio, inicio.AddDays(4))).Exito, "Canceladas bloquean nuevo período");
    Exigir(!(await Guardar(102, inicio.AddDays(20), inicio.AddDays(21))).Exito, "Acepta empleado inactivo");
}

async Task Resumen()
{
    var e = await Preparar(); var hoy = DateTime.Today;
    await using (var db = e.Fabrica.CreateDbContext())
    {
        (await db.Colaboradores.SingleAsync(c => c.Id == 102)).Estado = EstadoColaborador.Inactivo;
        foreach (var fecha in new[] { hoy.AddDays(-1), hoy.AddDays(30).AddHours(23), hoy.AddDays(31) })
            db.Documentos.Add(new DocumentoDigitalizado { EmpresaId = 101, ColaboradorId = 101, TipoDocumentoId = 101,
                NombreArchivo = "prueba.pdf", Extension = ".pdf", FechaEmision = hoy.AddYears(-1), FechaVencimiento = fecha });
        db.Contratos.Add(new Contrato { EmpresaId = 101, ColaboradorId = 101, TipoContratoId = 101, Numero = "V", FechaFin = hoy.AddDays(-1) });
        await db.SaveChangesAsync();
    }
    var r = await new ServicioResumen(e.Fabrica, e.Empresa, NullLogger<ServicioResumen>.Instance).ObtenerAsync();
    Exigir(r.ColaboradoresRegistrados == 2 && r.ColaboradoresActivos == 1, "Total/activos equivocados");
    Exigir(r.DocumentosVencidos == 1 && r.DocumentosPorVencer == 1, "Ventana de vencimiento equivocada");
    Exigir(r.ContratosVencidos == 1 && r.HayContratosPorVencer, "Oculta contrato vencido");
}

async Task Alertas()
{
    var e = await Preparar(); var hoy = DateTime.Today;
    await using (var db = e.Fabrica.CreateDbContext())
    {
        foreach (var id in new[] { 101, 102 })
            db.Contratos.Add(new Contrato { EmpresaId = 101, ColaboradorId = id, TipoContratoId = 101,
                Numero = "MISMO", FechaFin = hoy.AddDays(3) });
        db.Documentos.Add(new DocumentoDigitalizado { Id = 101, EmpresaId = 101, ColaboradorId = 101, TipoDocumentoId = 101,
            NombreArchivo = "prueba.pdf", Extension = ".pdf", FechaEmision = hoy.AddYears(-1), FechaVencimiento = hoy.AddDays(3) });
        await db.SaveChangesAsync();
    }
    var motor = Motor(e);
    var primera = await motor.GenerarAsync(); var segunda = await motor.GenerarAsync();
    var pendientes = await motor.ObtenerPendientesAsync();
    Exigir(primera.Generados >= 3 && segunda.Generados == 0, "Duplicación de avisos");
    Exigir(pendientes.Count(a => a.Tipo == TipoAviso.VencimientoContrato) == 2
        && pendientes.Count(a => a.Tipo == TipoAviso.VencimientoDocumento) == 1, "Colisión entre contratos");
    await using (var db = e.Fabrica.CreateDbContext())
    {
        (await db.Documentos.SingleAsync()).FechaVencimiento = hoy.AddYears(1);
        await db.SaveChangesAsync();
    }
    await motor.GenerarAsync();
    Exigir((await motor.ObtenerPendientesAsync()).All(a => a.Tipo != TipoAviso.VencimientoDocumento), "Renovación deja aviso obsoleto");
}

async Task Documentos()
{
    var fallo = new FalloSegundoGuardado(); var e = await Preparar(fallo);
    var archivo = Path.Combine(carpeta, "prueba.pdf"); await File.WriteAllTextAsync(archivo, "%PDF-1.4\nprueba");
    var datos = new DatosDocumento { ColaboradorId = 101, TipoDocumentoId = 101, RutaOrigen = archivo,
        FechaEmision = DateTime.Today, FechaVencimiento = DateTime.Today.AddMonths(1) };
    fallo.Activar();
    var resultado = await Docs(e).GuardarAsync(datos);
    Exigir(!resultado.Exito, "La inyección de fallo no se propagó como rechazo");
    await using (var db = e.Fabrica.CreateDbContext())
        Exigir(!await db.Documentos.AnyAsync() && !await db.ContenidosDocumento.AnyAsync(), "Guardado parcial tras fallar bytes");
    resultado = await Docs(e).GuardarAsync(datos);
    Exigir(resultado.Exito, resultado.Error ?? "Guardado normal falló");
    var contenidoGuardado = await Docs(e).ObtenerContenidoAsync(resultado.Id);
    var contenidoOriginal = await File.ReadAllBytesAsync(archivo);
    Exigir(contenidoGuardado is not null && contenidoGuardado.SequenceEqual(contenidoOriginal), "Bytes distintos");
    await Motor(e).GenerarAsync();
    Exigir((await Docs(e).EliminarAsync(resultado.Id)).Exito, "Eliminación falló");
    await using var final = e.Fabrica.CreateDbContext();
    Exigir(!await final.Documentos.AnyAsync() && !await final.ContenidosDocumento.AnyAsync(), "No eliminó contenido");
    Exigir(await final.MovimientosDocumento.AnyAsync(m => m.Accion == AccionDocumento.Eliminacion), "Eliminación sin historial");
}

async Task PermisosRespaldos()
{
    var e = await Preparar();
    foreach (var perfil in new[] { PerfilUsuario.Administrador, PerfilUsuario.Consulta })
    {
        e.Sesion.Iniciar(new Usuario { Id = 2, Perfil = perfil });
        var servicio = new ServicioRespaldos(e.Fabrica, NullLogger<ServicioRespaldos>.Instance, e.Sesion);
        Exigir(!(await servicio.RespaldarAsync(MotivoRespaldo.Manual)).Exito, "Permite respaldo global a " + perfil);
        Exigir(!(await servicio.RestaurarAsync("no-existe.db")).Exito, "Permite restaurar a " + perfil);
    }
}

Task Efemerides()
{
    var aviso = new LineaAviso(1, TipoAviso.Cumpleanos, EstadoAviso.Pendiente, "Cumpleaños", "", DateTime.Today.AddDays(-1), "", DateTime.UtcNow);
    Exigir(!aviso.EsCritico && !aviso.EsAdvertencia && aviso.PlazoTexto.StartsWith("Fue"), "Cumpleaños figura vencido");
    return Task.CompletedTask;
}

sealed record Entorno(Fabrica Fabrica, ContextoEmpresa Empresa, SesionUsuario Sesion);
sealed class Fabrica(DbContextOptions<ContextoRhManager> opciones, ContextoEmpresa empresa, SesionUsuario sesion)
    : IDbContextFactory<ContextoRhManager>
{
    public ContextoRhManager CreateDbContext() => new(opciones, empresa, sesion);
    public Task<ContextoRhManager> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
sealed class FalloSegundoGuardado : SaveChangesInterceptor
{
    private int _restantes;
    public void Activar() => _restantes = 2;
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_restantes > 0 && --_restantes == 0)
            throw new DbUpdateException("Fallo deliberado del guardado de contenido");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
