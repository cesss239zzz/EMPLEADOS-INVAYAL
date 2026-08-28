using empleados.Datos.Entidades;
using Microsoft.EntityFrameworkCore;

namespace empleados.Datos;

/// <summary>
/// Datos de la demostracion, sembrados por migracion (CLAUDE.md, regla 14).
///
/// ─────────────────────────────────────────────────────────────────────────
/// ATENCION — DATOS PROVISIONALES
///
/// Los nombres, identidades, puestos y fechas de los 12 colaboradores debian
/// copiarse EXACTOS de RH Manager-prototipo.html. Ese archivo no estaba en el disco
/// al generar esta siembra, asi que lo que sigue es un juego de datos con la
/// estructura correcta (2 empresas, 3 sucursales, catalogos completos y 12
/// colaboradores con historial, contratos y documentos) pero con valores
/// inventados, salvo "Delmy Cardona", que si fue mencionada como caso de prueba
/// y conserva sus 5 movimientos laborales.
///
/// Cuando aparezca el prototipo: reemplazar los arreglos de este archivo,
/// borrar la migracion y regenerarla. No hay dato real que perder.
/// ─────────────────────────────────────────────────────────────────────────
/// </summary>
internal static class SiembraDemostracion
{
    /// <summary>Marca fija de creacion. Debe ser constante: un valor dinamico
    /// haria que cada migracion detecte un cambio inexistente.</summary>
    private static readonly DateTime Marca = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Hash BCrypt factor 11 de la contrasena del usuario semilla.
    /// La contrasena en claro NUNCA aparece en el codigo ni en la migracion
    /// (CLAUDE.md, regla 12). El usuario debe cambiarla al primer acceso.
    /// </summary>
    private const string HashContrasenaSemilla =
        "$2a$11$20vcf0UgN2TSI.LfZxBKZuZf3o6dRwumzQivpJPvjuBcku2hQMPpO";

    public static void Aplicar(ModelBuilder constructor)
    {
        SembrarEmpresas(constructor);
        SembrarUsuarios(constructor);
        SembrarSucursales(constructor);
        SembrarDepartamentos(constructor);
        SembrarPuestos(constructor);
        SembrarTiposContrato(constructor);
        SembrarTiposDocumento(constructor);
        SembrarColaboradores(constructor);
        SembrarContactosEmergencia(constructor);
        SembrarMovimientos(constructor);
        SembrarContratos(constructor);
        SembrarDocumentos(constructor);
    }

    private static void SembrarEmpresas(ModelBuilder c) => c.Entity<Empresa>().HasData(
        new Empresa
        {
            Id = 1, Nombre = "Inversiones Ayala Alvarenga S. de R.L.", NombreCorto = "Inversiones Ayala",
            Rtn = "08019995123456", Direccion = "Barrio El Centro, Tegucigalpa, Francisco Morazan",
            Telefono = "2234-5600", Correo = "administracion@invayal.hn",
            ColorPrimario = "#C0362C", Activa = true, FechaCreacion = Marca
        },
        new Empresa
        {
            Id = 2, Nombre = "Comercializadora Alvarenga S. de R.L.", NombreCorto = "Comercializadora",
            Rtn = "08019995654321", Direccion = "Colonia Palmira, Tegucigalpa, Francisco Morazan",
            Telefono = "2234-5700", Correo = "administracion@comalvarenga.hn",
            ColorPrimario = "#1F6F8B", Activa = true, FechaCreacion = Marca
        });

    private static void SembrarUsuarios(ModelBuilder c)
    {
        c.Entity<Usuario>().HasData(new Usuario
        {
            Id = 1, NombreUsuario = "cregalado", HashContrasena = HashContrasenaSemilla,
            NombreCompleto = "Cesar Regalado", Correo = "cregalado@invayal.hn",
            Perfil = PerfilUsuario.SuperAdministrador, Activo = true,
            DebeCambiarContrasena = true, IntentosFallidos = 0, FechaCreacion = Marca
        });

        // El SuperAdministrador entra a las dos empresas.
        c.Entity<UsuarioEmpresa>().HasData(
            new UsuarioEmpresa { Id = 1, UsuarioId = 1, EmpresaId = 1, FechaCreacion = Marca },
            new UsuarioEmpresa { Id = 2, UsuarioId = 1, EmpresaId = 2, FechaCreacion = Marca });
    }

    private static void SembrarSucursales(ModelBuilder c) => c.Entity<Sucursal>().HasData(
        new Sucursal { Id = 1, EmpresaId = 1, Nombre = "Casa Matriz", Codigo = "CM", Direccion = "Barrio El Centro, Tegucigalpa", Telefono = "2234-5600", Activa = true, FechaCreacion = Marca },
        new Sucursal { Id = 2, EmpresaId = 1, Nombre = "Sucursal Comayaguela", Codigo = "CMY", Direccion = "Mercado Zonal Belen, Comayaguela", Telefono = "2234-5610", Activa = true, FechaCreacion = Marca },
        new Sucursal { Id = 3, EmpresaId = 2, Nombre = "Bodega Central", Codigo = "BC", Direccion = "Anillo Periferico, Tegucigalpa", Telefono = "2234-5710", Activa = true, FechaCreacion = Marca });

    private static void SembrarDepartamentos(ModelBuilder c) => c.Entity<Departamento>().HasData(
        new Departamento { Id = 1, EmpresaId = 1, Nombre = "Administracion", Activo = true, FechaCreacion = Marca },
        new Departamento { Id = 2, EmpresaId = 1, Nombre = "Contabilidad", Activo = true, FechaCreacion = Marca },
        new Departamento { Id = 3, EmpresaId = 1, Nombre = "Ventas", Activo = true, FechaCreacion = Marca },
        new Departamento { Id = 4, EmpresaId = 1, Nombre = "Bodega", Activo = true, FechaCreacion = Marca },
        new Departamento { Id = 5, EmpresaId = 2, Nombre = "Administracion", Activo = true, FechaCreacion = Marca },
        new Departamento { Id = 6, EmpresaId = 2, Nombre = "Logistica", Activo = true, FechaCreacion = Marca });

    private static void SembrarPuestos(ModelBuilder c) => c.Entity<Puesto>().HasData(
        new Puesto { Id = 1, EmpresaId = 1, DepartamentoId = 1, Nombre = "Gerente General", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 2, EmpresaId = 1, DepartamentoId = 1, Nombre = "Asistente Administrativa", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 3, EmpresaId = 1, DepartamentoId = 2, Nombre = "Contador General", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 4, EmpresaId = 1, DepartamentoId = 2, Nombre = "Auxiliar Contable", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 5, EmpresaId = 1, DepartamentoId = 3, Nombre = "Jefe de Ventas", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 6, EmpresaId = 1, DepartamentoId = 3, Nombre = "Vendedor", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 7, EmpresaId = 1, DepartamentoId = 4, Nombre = "Jefe de Bodega", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 8, EmpresaId = 1, DepartamentoId = 4, Nombre = "Auxiliar de Bodega", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 9, EmpresaId = 2, DepartamentoId = 5, Nombre = "Administrador", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 10, EmpresaId = 2, DepartamentoId = 6, Nombre = "Coordinador de Logistica", Activo = true, FechaCreacion = Marca },
        new Puesto { Id = 11, EmpresaId = 2, DepartamentoId = 6, Nombre = "Motorista", Activo = true, FechaCreacion = Marca });

    private static void SembrarTiposContrato(ModelBuilder c) => c.Entity<TipoContrato>().HasData(
        new TipoContrato { Id = 1, EmpresaId = 1, Nombre = "Indefinido", RequiereVencimiento = false, Activo = true, FechaCreacion = Marca },
        new TipoContrato { Id = 2, EmpresaId = 1, Nombre = "Temporal", RequiereVencimiento = true, Activo = true, FechaCreacion = Marca },
        new TipoContrato { Id = 3, EmpresaId = 1, Nombre = "Por obra o servicio", RequiereVencimiento = true, Activo = true, FechaCreacion = Marca },
        new TipoContrato { Id = 4, EmpresaId = 2, Nombre = "Indefinido", RequiereVencimiento = false, Activo = true, FechaCreacion = Marca },
        new TipoContrato { Id = 5, EmpresaId = 2, Nombre = "Temporal", RequiereVencimiento = true, Activo = true, FechaCreacion = Marca });

    private static void SembrarTiposDocumento(ModelBuilder c) => c.Entity<TipoDocumento>().HasData(
        new TipoDocumento { Id = 1, EmpresaId = 1, Nombre = "Tarjeta de Identidad", RequiereVencimiento = false, DiasAvisoAnticipado = 30, Activo = true, FechaCreacion = Marca },
        new TipoDocumento { Id = 2, EmpresaId = 1, Nombre = "RTN Numerico", RequiereVencimiento = false, DiasAvisoAnticipado = 30, Activo = true, FechaCreacion = Marca },
        new TipoDocumento { Id = 3, EmpresaId = 1, Nombre = "Constancia Policial", RequiereVencimiento = true, DiasAvisoAnticipado = 45, Activo = true, FechaCreacion = Marca },
        new TipoDocumento { Id = 4, EmpresaId = 1, Nombre = "Certificado de Salud", RequiereVencimiento = true, DiasAvisoAnticipado = 30, Activo = true, FechaCreacion = Marca },
        new TipoDocumento { Id = 5, EmpresaId = 1, Nombre = "Titulo Academico", RequiereVencimiento = false, DiasAvisoAnticipado = 30, Activo = true, FechaCreacion = Marca },
        new TipoDocumento { Id = 6, EmpresaId = 2, Nombre = "Tarjeta de Identidad", RequiereVencimiento = false, DiasAvisoAnticipado = 30, Activo = true, FechaCreacion = Marca },
        new TipoDocumento { Id = 7, EmpresaId = 2, Nombre = "Licencia de Conducir", RequiereVencimiento = true, DiasAvisoAnticipado = 60, Activo = true, FechaCreacion = Marca },
        new TipoDocumento { Id = 8, EmpresaId = 2, Nombre = "Certificado de Salud", RequiereVencimiento = true, DiasAvisoAnticipado = 30, Activo = true, FechaCreacion = Marca });

    private static void SembrarColaboradores(ModelBuilder c) => c.Entity<Colaborador>().HasData(
        Crear(1, 1, "EMP-001", "0801-1985-04521", "Delmy", "Suyapa", "Cardona", "Rivera", Sexo.Femenino, new(1985, 3, 14), new(2018, 2, 5), 1, 2, 3, 32500m, "9912-4478", "delmy.cardona@invayal.hn", "Colonia Kennedy, Tegucigalpa"),
        Crear(2, 1, "EMP-002", "0801-1979-01188", "Carlos", "Roberto", "Ayala", "Alvarenga", Sexo.Masculino, new(1979, 8, 22), new(2015, 1, 12), 1, 1, 1, 68000m, "9988-1122", "carlos.ayala@invayal.hn", "Residencial El Trapiche, Tegucigalpa"),
        Crear(3, 1, "EMP-003", "0801-1990-07733", "Marlon", "Josue", "Discua", "Medina", Sexo.Masculino, new(1990, 11, 3), new(2019, 6, 17), 1, 2, 4, 18500m, "9945-3311", "marlon.discua@invayal.hn", "Barrio La Granja, Comayaguela"),
        Crear(4, 1, "EMP-004", "0801-1988-05590", "Karla", "Yolanda", "Pineda", "Zelaya", Sexo.Femenino, new(1988, 5, 27), new(2017, 9, 4), 1, 1, 2, 16800m, "9922-7788", "karla.pineda@invayal.hn", "Colonia Miraflores, Tegucigalpa"),
        Crear(5, 1, "EMP-005", "0801-1983-03310", "Jose", "Luis", "Mejia", "Fuentes", Sexo.Masculino, new(1983, 1, 9), new(2016, 4, 20), 1, 3, 5, 29000m, "9933-6644", "jose.mejia@invayal.hn", "Colonia Las Uvas, Tegucigalpa"),
        Crear(6, 1, "EMP-006", "0801-1995-09912", "Andrea", "Nicole", "Sanchez", "Bonilla", Sexo.Femenino, new(1995, 7, 30), new(2021, 3, 1), 2, 3, 6, 13200m, "9977-2255", "andrea.sanchez@invayal.hn", "Barrio Belen, Comayaguela"),
        Crear(7, 1, "EMP-007", "0801-1992-06654", "Oscar", "Danilo", "Flores", "Cruz", Sexo.Masculino, new(1992, 12, 18), new(2020, 8, 10), 2, 3, 6, 13200m, "9966-4433", "oscar.flores@invayal.hn", "Colonia Villa Nueva, Comayaguela"),
        Crear(8, 1, "EMP-008", "0801-1987-02245", "Wilmer", "Antonio", "Zuniga", "Lopez", Sexo.Masculino, new(1987, 6, 6), new(2018, 11, 26), 2, 4, 7, 21000m, "9955-8877", "wilmer.zuniga@invayal.hn", "Barrio El Manchen, Tegucigalpa"),
        Crear(9, 1, "EMP-009", "0801-1998-08876", "Gabriela", "Michelle", "Romero", "Castillo", Sexo.Femenino, new(1998, 9, 12), new(2022, 5, 16), 2, 4, 8, 11500m, "9944-1199", "gabriela.romero@invayal.hn", "Colonia San Miguel, Comayaguela"),
        Crear(10, 2, "COM-001", "0801-1981-04432", "Reina", "Isabel", "Alvarenga", "Portillo", Sexo.Femenino, new(1981, 4, 25), new(2019, 2, 11), 3, 5, 9, 45000m, "9911-3366", "reina.alvarenga@comalvarenga.hn", "Colonia Palmira, Tegucigalpa"),
        Crear(11, 2, "COM-002", "0801-1993-07701", "Edwin", "Alexander", "Turcios", "Maradiaga", Sexo.Masculino, new(1993, 2, 8), new(2020, 10, 5), 3, 6, 10, 24000m, "9900-5544", "edwin.turcios@comalvarenga.hn", "Colonia Cerro Grande, Tegucigalpa"),
        Crear(12, 2, "COM-003", "0801-1986-01123", "Nelson", "Ramon", "Padilla", "Erazo", Sexo.Masculino, new(1986, 10, 2), new(2021, 7, 19), 3, 6, 11, 15800m, "9899-6677", "nelson.padilla@comalvarenga.hn", "Barrio Concepcion, Comayaguela"));

    private static Colaborador Crear(
        int id, int empresaId, string codigo, string identidad,
        string primerNombre, string segundoNombre, string primerApellido, string segundoApellido,
        Sexo sexo, DateTime nacimiento, DateTime ingreso,
        int sucursalId, int departamentoId, int puestoId, decimal salario,
        string telefono, string correo, string direccion) => new()
    {
        Id = id, EmpresaId = empresaId, Codigo = codigo, Identidad = identidad,
        PrimerNombre = primerNombre, SegundoNombre = segundoNombre,
        PrimerApellido = primerApellido, SegundoApellido = segundoApellido,
        Sexo = sexo, FechaNacimiento = nacimiento, FechaIngreso = ingreso,
        Estado = EstadoColaborador.Activo,
        SucursalId = sucursalId, DepartamentoId = departamentoId, PuestoId = puestoId,
        SalarioBase = salario, Telefono = telefono, Correo = correo, Direccion = direccion,
        FechaCreacion = Marca
    };

    private static void SembrarContactosEmergencia(ModelBuilder c) => c.Entity<ContactoEmergencia>().HasData(
        new ContactoEmergencia { Id = 1, EmpresaId = 1, ColaboradorId = 1, Nombre = "Hector Cardona", Parentesco = "Padre", Telefono = "9812-4400", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 2, EmpresaId = 1, ColaboradorId = 1, Nombre = "Lesly Rivera", Parentesco = "Hermana", Telefono = "9813-5511", EsPrincipal = false, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 3, EmpresaId = 1, ColaboradorId = 2, Nombre = "Sonia Alvarenga", Parentesco = "Esposa", Telefono = "9814-6622", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 4, EmpresaId = 1, ColaboradorId = 3, Nombre = "Rosa Medina", Parentesco = "Madre", Telefono = "9815-7733", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 5, EmpresaId = 1, ColaboradorId = 4, Nombre = "Julio Pineda", Parentesco = "Esposo", Telefono = "9816-8844", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 6, EmpresaId = 1, ColaboradorId = 5, Nombre = "Ana Fuentes", Parentesco = "Madre", Telefono = "9817-9955", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 7, EmpresaId = 1, ColaboradorId = 6, Nombre = "Mario Bonilla", Parentesco = "Padre", Telefono = "9818-1166", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 8, EmpresaId = 1, ColaboradorId = 7, Nombre = "Iris Cruz", Parentesco = "Madre", Telefono = "9819-2277", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 9, EmpresaId = 1, ColaboradorId = 8, Nombre = "Dina Lopez", Parentesco = "Esposa", Telefono = "9820-3388", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 10, EmpresaId = 1, ColaboradorId = 9, Nombre = "Sandra Castillo", Parentesco = "Madre", Telefono = "9821-4499", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 11, EmpresaId = 2, ColaboradorId = 10, Nombre = "Mario Portillo", Parentesco = "Hermano", Telefono = "9822-5500", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 12, EmpresaId = 2, ColaboradorId = 11, Nombre = "Cindy Maradiaga", Parentesco = "Esposa", Telefono = "9823-6611", EsPrincipal = true, FechaCreacion = Marca },
        new ContactoEmergencia { Id = 13, EmpresaId = 2, ColaboradorId = 12, Nombre = "Elsa Erazo", Parentesco = "Madre", Telefono = "9824-7722", EsPrincipal = true, FechaCreacion = Marca });

    /// <summary>
    /// Historial laboral. Delmy Cardona lleva 5 movimientos porque es el caso de
    /// prueba declarado para E6.
    /// </summary>
    private static void SembrarMovimientos(ModelBuilder c) => c.Entity<MovimientoLaboral>().HasData(
        new MovimientoLaboral { Id = 1, EmpresaId = 1, ColaboradorId = 1, Tipo = TipoMovimiento.Ingreso, Fecha = new(2018, 2, 5), PuestoNuevoId = 4, SucursalNuevaId = 1, SalarioNuevo = 14000m, Observacion = "Ingreso como Auxiliar Contable en Casa Matriz.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 2, EmpresaId = 1, ColaboradorId = 1, Tipo = TipoMovimiento.CambioSalario, Fecha = new(2019, 3, 1), SalarioAnterior = 14000m, SalarioNuevo = 17500m, Observacion = "Ajuste anual por desempeno.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 3, EmpresaId = 1, ColaboradorId = 1, Tipo = TipoMovimiento.CambioPuesto, Fecha = new(2020, 7, 15), PuestoAnteriorId = 4, PuestoNuevoId = 3, SalarioAnterior = 17500m, SalarioNuevo = 26000m, Observacion = "Promocion a Contador General.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 4, EmpresaId = 1, ColaboradorId = 1, Tipo = TipoMovimiento.CambioSalario, Fecha = new(2022, 4, 1), SalarioAnterior = 26000m, SalarioNuevo = 29800m, Observacion = "Ajuste por antiguedad.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 5, EmpresaId = 1, ColaboradorId = 1, Tipo = TipoMovimiento.CambioSalario, Fecha = new(2024, 2, 1), SalarioAnterior = 29800m, SalarioNuevo = 32500m, Observacion = "Revision salarial 2024.", FechaCreacion = Marca },

        new MovimientoLaboral { Id = 6, EmpresaId = 1, ColaboradorId = 2, Tipo = TipoMovimiento.Ingreso, Fecha = new(2015, 1, 12), PuestoNuevoId = 1, SucursalNuevaId = 1, SalarioNuevo = 55000m, Observacion = "Ingreso como Gerente General.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 7, EmpresaId = 1, ColaboradorId = 2, Tipo = TipoMovimiento.CambioSalario, Fecha = new(2021, 1, 1), SalarioAnterior = 55000m, SalarioNuevo = 68000m, Observacion = "Revision de la gerencia.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 8, EmpresaId = 1, ColaboradorId = 3, Tipo = TipoMovimiento.Ingreso, Fecha = new(2019, 6, 17), PuestoNuevoId = 4, SucursalNuevaId = 1, SalarioNuevo = 15000m, Observacion = "Ingreso como Auxiliar Contable.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 9, EmpresaId = 1, ColaboradorId = 3, Tipo = TipoMovimiento.CambioSalario, Fecha = new(2023, 6, 1), SalarioAnterior = 15000m, SalarioNuevo = 18500m, Observacion = "Ajuste anual.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 10, EmpresaId = 1, ColaboradorId = 4, Tipo = TipoMovimiento.Ingreso, Fecha = new(2017, 9, 4), PuestoNuevoId = 2, SucursalNuevaId = 1, SalarioNuevo = 16800m, Observacion = "Ingreso como Asistente Administrativa.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 11, EmpresaId = 1, ColaboradorId = 5, Tipo = TipoMovimiento.Ingreso, Fecha = new(2016, 4, 20), PuestoNuevoId = 6, SucursalNuevaId = 1, SalarioNuevo = 12000m, Observacion = "Ingreso como Vendedor.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 12, EmpresaId = 1, ColaboradorId = 5, Tipo = TipoMovimiento.CambioPuesto, Fecha = new(2019, 10, 1), PuestoAnteriorId = 6, PuestoNuevoId = 5, SalarioAnterior = 12000m, SalarioNuevo = 29000m, Observacion = "Promocion a Jefe de Ventas.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 13, EmpresaId = 1, ColaboradorId = 6, Tipo = TipoMovimiento.Ingreso, Fecha = new(2021, 3, 1), PuestoNuevoId = 6, SucursalNuevaId = 2, SalarioNuevo = 13200m, Observacion = "Ingreso como Vendedora.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 14, EmpresaId = 1, ColaboradorId = 7, Tipo = TipoMovimiento.Ingreso, Fecha = new(2020, 8, 10), PuestoNuevoId = 6, SucursalNuevaId = 2, SalarioNuevo = 13200m, Observacion = "Ingreso como Vendedor.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 15, EmpresaId = 1, ColaboradorId = 8, Tipo = TipoMovimiento.Ingreso, Fecha = new(2018, 11, 26), PuestoNuevoId = 8, SucursalNuevaId = 2, SalarioNuevo = 11000m, Observacion = "Ingreso como Auxiliar de Bodega.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 16, EmpresaId = 1, ColaboradorId = 8, Tipo = TipoMovimiento.CambioPuesto, Fecha = new(2022, 1, 15), PuestoAnteriorId = 8, PuestoNuevoId = 7, SalarioAnterior = 11000m, SalarioNuevo = 21000m, Observacion = "Promocion a Jefe de Bodega.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 17, EmpresaId = 1, ColaboradorId = 9, Tipo = TipoMovimiento.Ingreso, Fecha = new(2022, 5, 16), PuestoNuevoId = 8, SucursalNuevaId = 2, SalarioNuevo = 11500m, Observacion = "Ingreso como Auxiliar de Bodega.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 18, EmpresaId = 2, ColaboradorId = 10, Tipo = TipoMovimiento.Ingreso, Fecha = new(2019, 2, 11), PuestoNuevoId = 9, SucursalNuevaId = 3, SalarioNuevo = 38000m, Observacion = "Ingreso como Administradora.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 19, EmpresaId = 2, ColaboradorId = 10, Tipo = TipoMovimiento.CambioSalario, Fecha = new(2023, 2, 1), SalarioAnterior = 38000m, SalarioNuevo = 45000m, Observacion = "Ajuste por antiguedad.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 20, EmpresaId = 2, ColaboradorId = 11, Tipo = TipoMovimiento.Ingreso, Fecha = new(2020, 10, 5), PuestoNuevoId = 10, SucursalNuevaId = 3, SalarioNuevo = 24000m, Observacion = "Ingreso como Coordinador de Logistica.", FechaCreacion = Marca },
        new MovimientoLaboral { Id = 21, EmpresaId = 2, ColaboradorId = 12, Tipo = TipoMovimiento.Ingreso, Fecha = new(2021, 7, 19), PuestoNuevoId = 11, SucursalNuevaId = 3, SalarioNuevo = 15800m, Observacion = "Ingreso como Motorista.", FechaCreacion = Marca });

    private static void SembrarContratos(ModelBuilder c) => c.Entity<Contrato>().HasData(
        new Contrato { Id = 1, EmpresaId = 1, ColaboradorId = 1, TipoContratoId = 1, Numero = "CT-2018-001", FechaInicio = new(2018, 2, 5), SalarioAcordado = 32500m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 2, EmpresaId = 1, ColaboradorId = 2, TipoContratoId = 1, Numero = "CT-2015-001", FechaInicio = new(2015, 1, 12), SalarioAcordado = 68000m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 3, EmpresaId = 1, ColaboradorId = 3, TipoContratoId = 1, Numero = "CT-2019-004", FechaInicio = new(2019, 6, 17), SalarioAcordado = 18500m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 4, EmpresaId = 1, ColaboradorId = 4, TipoContratoId = 1, Numero = "CT-2017-009", FechaInicio = new(2017, 9, 4), SalarioAcordado = 16800m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 5, EmpresaId = 1, ColaboradorId = 5, TipoContratoId = 1, Numero = "CT-2016-002", FechaInicio = new(2016, 4, 20), SalarioAcordado = 29000m, Vigente = true, FechaCreacion = Marca },
        // Contratos temporales: son los que alimentan los avisos de vencimiento en E7.
        new Contrato { Id = 6, EmpresaId = 1, ColaboradorId = 6, TipoContratoId = 2, Numero = "CT-2021-011", FechaInicio = new(2021, 3, 1), FechaFin = new(2026, 9, 30), SalarioAcordado = 13200m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 7, EmpresaId = 1, ColaboradorId = 7, TipoContratoId = 2, Numero = "CT-2020-018", FechaInicio = new(2020, 8, 10), FechaFin = new(2026, 10, 31), SalarioAcordado = 13200m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 8, EmpresaId = 1, ColaboradorId = 8, TipoContratoId = 1, Numero = "CT-2018-021", FechaInicio = new(2018, 11, 26), SalarioAcordado = 21000m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 9, EmpresaId = 1, ColaboradorId = 9, TipoContratoId = 2, Numero = "CT-2022-006", FechaInicio = new(2022, 5, 16), FechaFin = new(2026, 9, 15), SalarioAcordado = 11500m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 10, EmpresaId = 2, ColaboradorId = 10, TipoContratoId = 4, Numero = "CC-2019-001", FechaInicio = new(2019, 2, 11), SalarioAcordado = 45000m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 11, EmpresaId = 2, ColaboradorId = 11, TipoContratoId = 4, Numero = "CC-2020-003", FechaInicio = new(2020, 10, 5), SalarioAcordado = 24000m, Vigente = true, FechaCreacion = Marca },
        new Contrato { Id = 12, EmpresaId = 2, ColaboradorId = 12, TipoContratoId = 5, Numero = "CC-2021-007", FechaInicio = new(2021, 7, 19), FechaFin = new(2026, 11, 30), SalarioAcordado = 15800m, Vigente = true, FechaCreacion = Marca });

    private static void SembrarDocumentos(ModelBuilder c) => c.Entity<DocumentoDigitalizado>().HasData(
        new DocumentoDigitalizado { Id = 1, EmpresaId = 1, ColaboradorId = 1, TipoDocumentoId = 1, NombreArchivo = "identidad-delmy-cardona.pdf", RutaRelativa = "1/1/identidad-delmy-cardona.pdf", FechaEmision = new(2015, 6, 10), TamanoBytes = 184320, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 2, EmpresaId = 1, ColaboradorId = 1, TipoDocumentoId = 5, NombreArchivo = "titulo-perito-mercantil.pdf", RutaRelativa = "1/1/titulo-perito-mercantil.pdf", FechaEmision = new(2007, 11, 20), TamanoBytes = 512000, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 3, EmpresaId = 1, ColaboradorId = 1, TipoDocumentoId = 4, NombreArchivo = "certificado-salud-2025.pdf", RutaRelativa = "1/1/certificado-salud-2025.pdf", FechaEmision = new(2025, 9, 1), FechaVencimiento = new(2026, 9, 1), TamanoBytes = 96256, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 4, EmpresaId = 1, ColaboradorId = 2, TipoDocumentoId = 1, NombreArchivo = "identidad-carlos-ayala.pdf", RutaRelativa = "1/2/identidad-carlos-ayala.pdf", FechaEmision = new(2014, 3, 3), TamanoBytes = 178176, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 5, EmpresaId = 1, ColaboradorId = 3, TipoDocumentoId = 1, NombreArchivo = "identidad-marlon-discua.pdf", RutaRelativa = "1/3/identidad-marlon-discua.pdf", FechaEmision = new(2016, 8, 15), TamanoBytes = 169984, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 6, EmpresaId = 1, ColaboradorId = 3, TipoDocumentoId = 3, NombreArchivo = "constancia-policial-2025.pdf", RutaRelativa = "1/3/constancia-policial-2025.pdf", FechaEmision = new(2025, 7, 14), FechaVencimiento = new(2026, 7, 14), TamanoBytes = 88064, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 7, EmpresaId = 1, ColaboradorId = 6, TipoDocumentoId = 4, NombreArchivo = "certificado-salud-andrea.pdf", RutaRelativa = "1/6/certificado-salud-andrea.pdf", FechaEmision = new(2025, 10, 5), FechaVencimiento = new(2026, 10, 5), TamanoBytes = 91136, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 8, EmpresaId = 1, ColaboradorId = 8, TipoDocumentoId = 1, NombreArchivo = "identidad-wilmer-zuniga.pdf", RutaRelativa = "1/8/identidad-wilmer-zuniga.pdf", FechaEmision = new(2013, 5, 22), TamanoBytes = 175104, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 9, EmpresaId = 2, ColaboradorId = 10, TipoDocumentoId = 6, NombreArchivo = "identidad-reina-alvarenga.pdf", RutaRelativa = "2/10/identidad-reina-alvarenga.pdf", FechaEmision = new(2012, 9, 30), TamanoBytes = 182272, FechaCreacion = Marca },
        new DocumentoDigitalizado { Id = 10, EmpresaId = 2, ColaboradorId = 12, TipoDocumentoId = 7, NombreArchivo = "licencia-nelson-padilla.pdf", RutaRelativa = "2/12/licencia-nelson-padilla.pdf", FechaEmision = new(2023, 10, 12), FechaVencimiento = new(2026, 10, 12), TamanoBytes = 76800, FechaCreacion = Marca });
}
