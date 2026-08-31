using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace empleados.Datos;

/// <summary>
/// Contexto de datos de RH Manager.
///
/// Se resuelve SIEMPRE por fabrica (<c>AddDbContextFactory</c>) y se usa dentro
/// de un <c>using</c>. Nunca se guarda como campo de un ViewModel: DbContext no
/// es seguro entre hilos y compartirlo produce fallos intermitentes que son el
/// peor tipo de error para depurar (CLAUDE.md, regla 2).
/// </summary>
public class ContextoRhManager : DbContext
{
    private readonly IContextoEmpresa _contextoEmpresa;

    public ContextoRhManager(DbContextOptions<ContextoRhManager> opciones, IContextoEmpresa contextoEmpresa)
        : base(opciones)
    {
        _contextoEmpresa = contextoEmpresa;
    }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioEmpresa> UsuariosEmpresas => Set<UsuarioEmpresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<TipoContrato> TiposContrato => Set<TipoContrato>();
    public DbSet<TipoDocumento> TiposDocumento => Set<TipoDocumento>();
    public DbSet<Colaborador> Colaboradores => Set<Colaborador>();
    public DbSet<ContactoEmergencia> ContactosEmergencia => Set<ContactoEmergencia>();
    public DbSet<MovimientoLaboral> MovimientosLaborales => Set<MovimientoLaboral>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<DocumentoDigitalizado> Documentos => Set<DocumentoDigitalizado>();
    public DbSet<ContenidoDocumento> ContenidosDocumento => Set<ContenidoDocumento>();
    public DbSet<MovimientoDocumento> MovimientosDocumento => Set<MovimientoDocumento>();
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
    public DbSet<Vacacion> Vacaciones => Set<Vacacion>();
    public DbSet<Aviso> Avisos => Set<Aviso>();

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        base.OnModelCreating(constructor);

        ConfigurarNombresYClaves(constructor);
        ConfigurarRelaciones(constructor);
        AplicarFiltrosDeEmpresa(constructor);
        AjustarTiposParaSqlite(constructor);

        SiembraSistema.Aplicar(constructor);
    }

    /// <summary>Tablas y columnas en snake_case espanol (CLAUDE.md).</summary>
    private static void ConfigurarNombresYClaves(ModelBuilder constructor)
    {
        constructor.Entity<Empresa>(e =>
        {
            e.ToTable("empresa");
            e.HasIndex(x => x.Rtn).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
            e.Property(x => x.NombreCorto).HasMaxLength(60).IsRequired();
            e.Property(x => x.Rtn).HasMaxLength(20).IsRequired();
            e.Property(x => x.ColorPrimario).HasMaxLength(7).IsRequired();
        });

        constructor.Entity<Usuario>(e =>
        {
            e.ToTable("usuario");
            e.HasIndex(x => x.NombreUsuario).IsUnique();
            e.Property(x => x.NombreUsuario).HasMaxLength(60).IsRequired();
            e.Property(x => x.HashContrasena).HasMaxLength(100).IsRequired();
            e.Property(x => x.NombreCompleto).HasMaxLength(160).IsRequired();
        });

        constructor.Entity<UsuarioEmpresa>(e =>
        {
            e.ToTable("usuario_empresa");
            e.HasIndex(x => new { x.UsuarioId, x.EmpresaId }).IsUnique();
        });

        constructor.Entity<Sucursal>(e =>
        {
            e.ToTable("sucursal");
            e.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
        });

        constructor.Entity<Departamento>(e =>
        {
            e.ToTable("departamento");
            e.HasIndex(x => new { x.EmpresaId, x.Nombre }).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        });

        constructor.Entity<Puesto>(e =>
        {
            e.ToTable("puesto");
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        });

        constructor.Entity<TipoContrato>(e =>
        {
            e.ToTable("tipo_contrato");
            e.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        });

        constructor.Entity<TipoDocumento>(e =>
        {
            e.ToTable("tipo_documento");
            e.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        });

        constructor.Entity<Colaborador>(e =>
        {
            e.ToTable("colaborador");
            // La identidad sigue siendo unica por empresa, pero ahora es opcional
            // (CR-04). SQLite no considera iguales dos NULL en un indice unico,
            // asi que pueden coexistir varios expedientes sin identidad todavia
            // y en cuanto se capture una, se sigue impidiendo el duplicado.
            e.HasIndex(x => new { x.EmpresaId, x.Identidad }).IsUnique();
            e.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Identidad).HasMaxLength(20);
            e.Property(x => x.PrimerNombre).HasMaxLength(60).IsRequired();
            e.Property(x => x.SegundoNombre).HasMaxLength(60);
            e.Property(x => x.PrimerApellido).HasMaxLength(60).IsRequired();
            e.Property(x => x.SegundoApellido).HasMaxLength(60);

            // El buscador consulta por esta columna, asi que lleva indice.
            e.Property(x => x.TextoBusqueda).HasMaxLength(320).IsRequired();
            e.HasIndex(x => new { x.EmpresaId, x.TextoBusqueda });

            // Propiedad calculada: se arma en memoria, no existe como columna.
            e.Ignore(x => x.NombreCompleto);
        });

        constructor.Entity<ContactoEmergencia>().ToTable("contacto_emergencia");
        constructor.Entity<MovimientoLaboral>().ToTable("movimiento_laboral");
        constructor.Entity<Contrato>().ToTable("contrato");

        constructor.Entity<DocumentoDigitalizado>(e =>
        {
            e.ToTable("documento_digitalizado");
            e.Property(x => x.NombreArchivo).HasMaxLength(260).IsRequired();
            e.Property(x => x.Extension).HasMaxLength(16).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(500);
        });

        constructor.Entity<ContenidoDocumento>(e =>
        {
            e.ToTable("contenido_documento");

            // Un contenido por documento, garantizado por la base y no por la
            // buena voluntad del servicio.
            e.HasIndex(x => x.DocumentoId).IsUnique();
            e.Property(x => x.Bytes).IsRequired();
        });

        constructor.Entity<MovimientoDocumento>(e =>
        {
            e.ToTable("movimiento_documento");
            e.Property(x => x.NombreDocumento).HasMaxLength(260).IsRequired();
            e.Property(x => x.NombreUsuario).HasMaxLength(160).IsRequired();
            e.Property(x => x.Detalle).HasMaxLength(500);
            e.HasIndex(x => new { x.EmpresaId, x.ColaboradorId, x.Fecha });
        });

        constructor.Entity<Incidencia>(e =>
        {
            e.ToTable("incidencia");
            e.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(1000);
        });

        constructor.Entity<Vacacion>(e =>
        {
            e.ToTable("vacacion");
            e.Property(x => x.Observacion).HasMaxLength(1000);
        });

        constructor.Entity<Aviso>(e =>
        {
            e.ToTable("aviso");

            // La idempotencia del motor de alertas no se confia a un if en C#:
            // se garantiza con un indice unico en la base (prueba de E7).
            e.HasIndex(x => new { x.EmpresaId, x.ClaveIdempotencia }).IsUnique();
            e.Property(x => x.ClaveIdempotencia).HasMaxLength(160).IsRequired();
        });
    }

    private static void ConfigurarRelaciones(ModelBuilder constructor)
    {
        constructor.Entity<UsuarioEmpresa>()
            .HasOne(x => x.Usuario).WithMany(u => u!.Empresas)
            .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<UsuarioEmpresa>()
            .HasOne(x => x.Empresa).WithMany()
            .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<Sucursal>()
            .HasOne(x => x.Empresa).WithMany(e => e!.Sucursales)
            .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<Puesto>()
            .HasOne(x => x.Departamento).WithMany()
            .HasForeignKey(x => x.DepartamentoId).OnDelete(DeleteBehavior.Restrict);

        // Un colaborador no se borra en cascada al tocar un catalogo: perder un
        // expediente por borrar un puesto seria un desastre silencioso.
        constructor.Entity<Colaborador>()
            .HasOne(x => x.Sucursal).WithMany()
            .HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);

        constructor.Entity<Colaborador>()
            .HasOne(x => x.Departamento).WithMany()
            .HasForeignKey(x => x.DepartamentoId).OnDelete(DeleteBehavior.Restrict);

        constructor.Entity<Colaborador>()
            .HasOne(x => x.Puesto).WithMany()
            .HasForeignKey(x => x.PuestoId).OnDelete(DeleteBehavior.Restrict);

        constructor.Entity<ContactoEmergencia>()
            .HasOne(x => x.Colaborador).WithMany(c => c!.ContactosEmergencia)
            .HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<MovimientoLaboral>()
            .HasOne(x => x.Colaborador).WithMany(c => c!.Movimientos)
            .HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<Contrato>()
            .HasOne(x => x.Colaborador).WithMany(c => c!.Contratos)
            .HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<Contrato>()
            .HasOne(x => x.TipoContrato).WithMany()
            .HasForeignKey(x => x.TipoContratoId).OnDelete(DeleteBehavior.Restrict);

        constructor.Entity<DocumentoDigitalizado>()
            .HasOne(x => x.Colaborador).WithMany(c => c!.Documentos)
            .HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<DocumentoDigitalizado>()
            .HasOne(x => x.TipoDocumento).WithMany()
            .HasForeignKey(x => x.TipoDocumentoId).OnDelete(DeleteBehavior.Restrict);

        // Borrar el documento se lleva sus bytes: no tiene sentido conservarlos.
        constructor.Entity<ContenidoDocumento>()
            .HasOne(x => x.Documento).WithOne(d => d!.Contenido)
            .HasForeignKey<ContenidoDocumento>(x => x.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<Incidencia>()
            .HasOne(x => x.Colaborador).WithMany()
            .HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<Vacacion>()
            .HasOne(x => x.Colaborador).WithMany()
            .HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);

        constructor.Entity<Aviso>()
            .HasOne(x => x.Colaborador).WithMany()
            .HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Filtro global por empresa. Es la unica frontera de aislamiento entre
    /// empresas y es un control de seguridad, no una comodidad: por eso esta
    /// aca y no repartido en Where sueltos por los repositorios.
    ///
    /// La expresion lee <c>_contextoEmpresa</c>, que es un campo de esta
    /// instancia, asi que EF lo trata como parametro y lo reevalua en cada
    /// consulta. Si se capturara el valor al construir el modelo, cambiar de
    /// empresa dejaria de tener efecto.
    /// </summary>
    private void AplicarFiltrosDeEmpresa(ModelBuilder constructor)
    {
        Filtrar<Sucursal>(constructor);
        Filtrar<Departamento>(constructor);
        Filtrar<Puesto>(constructor);
        Filtrar<TipoContrato>(constructor);
        Filtrar<TipoDocumento>(constructor);
        Filtrar<Colaborador>(constructor);
        Filtrar<ContactoEmergencia>(constructor);
        Filtrar<MovimientoLaboral>(constructor);
        Filtrar<Contrato>(constructor);
        Filtrar<DocumentoDigitalizado>(constructor);
        Filtrar<ContenidoDocumento>(constructor);
        Filtrar<MovimientoDocumento>(constructor);
        Filtrar<Incidencia>(constructor);
        Filtrar<Vacacion>(constructor);
        Filtrar<Aviso>(constructor);
    }

    private void Filtrar<T>(ModelBuilder constructor) where T : EntidadEmpresa
        => constructor.Entity<T>().HasQueryFilter(fila => fila.EmpresaId == _contextoEmpresa.EmpresaActivaId);

    /// <summary>
    /// Lo unico que cambia entre SQLite y MySQL. Al volver a MySQL despues de la
    /// presentacion se borra este metodo entero y se restauran las columnas
    /// DECIMAL(12,2) y DATETIME(6) nativas.
    ///
    /// SQLite no tiene tipo decimal: EF lo guardaria como texto, y entonces
    /// ordenar o sumar importes daria resultados lexicograficos, es decir mal.
    /// Se guardan como entero de centavos, que es exacto y ordenable. La
    /// propiedad en C# sigue siendo decimal, tal como exige la regla 10.
    /// </summary>
    private static void AjustarTiposParaSqlite(ModelBuilder constructor)
    {
        var aCentavos = new ValueConverter<decimal, long>(
            importe => (long)Math.Round(importe * 100m, MidpointRounding.AwayFromZero),
            centavos => centavos / 100m);

        var aCentavosNulos = new ValueConverter<decimal?, long?>(
            importe => importe == null ? null : (long)Math.Round(importe.Value * 100m, MidpointRounding.AwayFromZero),
            centavos => centavos == null ? null : centavos.Value / 100m);

        foreach (var entidad in constructor.Model.GetEntityTypes())
        {
            foreach (var propiedad in entidad.GetProperties())
            {
                if (propiedad.ClrType == typeof(decimal))
                {
                    propiedad.SetValueConverter(aCentavos);
                }
                else if (propiedad.ClrType == typeof(decimal?))
                {
                    propiedad.SetValueConverter(aCentavosNulos);
                }
            }
        }
    }
}
