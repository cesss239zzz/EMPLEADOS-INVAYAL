using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace empleados.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "departamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "empresa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    NombreCorto = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Rtn = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", nullable: false),
                    Correo = table.Column<string>(type: "TEXT", nullable: false),
                    ColorPrimario = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_contrato",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    RequiereVencimiento = table.Column<bool>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipo_contrato", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_documento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    RequiereVencimiento = table.Column<bool>(type: "INTEGER", nullable: false),
                    DiasAvisoAnticipado = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipo_documento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreUsuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    HashContrasena = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Correo = table.Column<string>(type: "TEXT", nullable: false),
                    Perfil = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    DebeCambiarContrasena = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "INTEGER", nullable: false),
                    BloqueadoHasta = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UltimoAcceso = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SucursalId = table.Column<int>(type: "INTEGER", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "puesto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DepartamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puesto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_puesto_departamento_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "departamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sucursal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", nullable: false),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sucursal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sucursal_empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_empresa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_empresa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuario_empresa_empresa_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuario_empresa_usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "colaborador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Identidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PrimerNombre = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    SegundoNombre = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    PrimerApellido = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    SegundoApellido = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Sexo = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaSalida = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", nullable: false),
                    Correo = table.Column<string>(type: "TEXT", nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", nullable: false),
                    SalarioBase = table.Column<long>(type: "INTEGER", nullable: false),
                    SucursalId = table.Column<int>(type: "INTEGER", nullable: false),
                    DepartamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PuestoId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_colaborador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_colaborador_departamento_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "departamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_colaborador_puesto_PuestoId",
                        column: x => x.PuestoId,
                        principalTable: "puesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_colaborador_sucursal_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "aviso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    ColaboradorId = table.Column<int>(type: "INTEGER", nullable: true),
                    Titulo = table.Column<string>(type: "TEXT", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaReferencia = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClaveIdempotencia = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aviso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_aviso_colaborador_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "colaborador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contacto_emergencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ColaboradorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Parentesco = table.Column<string>(type: "TEXT", nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", nullable: false),
                    TelefonoAlterno = table.Column<string>(type: "TEXT", nullable: false),
                    EsPrincipal = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacto_emergencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contacto_emergencia_colaborador_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "colaborador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contrato",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ColaboradorId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoContratoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<string>(type: "TEXT", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SalarioAcordado = table.Column<long>(type: "INTEGER", nullable: false),
                    Vigente = table.Column<bool>(type: "INTEGER", nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrato", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contrato_colaborador_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "colaborador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contrato_tipo_contrato_TipoContratoId",
                        column: x => x.TipoContratoId,
                        principalTable: "tipo_contrato",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_digitalizado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ColaboradorId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoDocumentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    NombreArchivo = table.Column<string>(type: "TEXT", nullable: false),
                    RutaRelativa = table.Column<string>(type: "TEXT", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TamanoBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_digitalizado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documento_digitalizado_colaborador_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "colaborador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_documento_digitalizado_tipo_documento_TipoDocumentoId",
                        column: x => x.TipoDocumentoId,
                        principalTable: "tipo_documento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimiento_laboral",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ColaboradorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PuestoAnteriorId = table.Column<int>(type: "INTEGER", nullable: true),
                    PuestoNuevoId = table.Column<int>(type: "INTEGER", nullable: true),
                    SucursalAnteriorId = table.Column<int>(type: "INTEGER", nullable: true),
                    SucursalNuevaId = table.Column<int>(type: "INTEGER", nullable: true),
                    SalarioAnterior = table.Column<long>(type: "INTEGER", nullable: true),
                    SalarioNuevo = table.Column<long>(type: "INTEGER", nullable: true),
                    Observacion = table.Column<string>(type: "TEXT", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento_laboral", x => x.Id);
                    table.ForeignKey(
                        name: "FK_movimiento_laboral_colaborador_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "colaborador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "departamento",
                columns: new[] { "Id", "Activo", "EmpresaId", "FechaCreacion", "Nombre" },
                values: new object[,]
                {
                    { 1, true, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Administracion" },
                    { 2, true, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Contabilidad" },
                    { 3, true, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ventas" },
                    { 4, true, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bodega" },
                    { 5, true, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Administracion" },
                    { 6, true, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Logistica" }
                });

            migrationBuilder.InsertData(
                table: "empresa",
                columns: new[] { "Id", "Activa", "ColorPrimario", "Correo", "Direccion", "FechaCreacion", "Nombre", "NombreCorto", "Rtn", "Telefono" },
                values: new object[,]
                {
                    { 1, true, "#C0362C", "administracion@invayal.hn", "Barrio El Centro, Tegucigalpa, Francisco Morazan", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Inversiones Ayala Alvarenga S. de R.L.", "Inversiones Ayala", "08019995123456", "2234-5600" },
                    { 2, true, "#1F6F8B", "administracion@comalvarenga.hn", "Colonia Palmira, Tegucigalpa, Francisco Morazan", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Comercializadora Alvarenga S. de R.L.", "Comercializadora", "08019995654321", "2234-5700" }
                });

            migrationBuilder.InsertData(
                table: "tipo_contrato",
                columns: new[] { "Id", "Activo", "EmpresaId", "FechaCreacion", "Nombre", "RequiereVencimiento" },
                values: new object[,]
                {
                    { 1, true, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Indefinido", false },
                    { 2, true, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Temporal", true },
                    { 3, true, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Por obra o servicio", true },
                    { 4, true, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Indefinido", false },
                    { 5, true, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Temporal", true }
                });

            migrationBuilder.InsertData(
                table: "tipo_documento",
                columns: new[] { "Id", "Activo", "DiasAvisoAnticipado", "EmpresaId", "FechaCreacion", "Nombre", "RequiereVencimiento" },
                values: new object[,]
                {
                    { 1, true, 30, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tarjeta de Identidad", false },
                    { 2, true, 30, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "RTN Numerico", false },
                    { 3, true, 45, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Constancia Policial", true },
                    { 4, true, 30, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Certificado de Salud", true },
                    { 5, true, 30, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Titulo Academico", false },
                    { 6, true, 30, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tarjeta de Identidad", false },
                    { 7, true, 60, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Licencia de Conducir", true },
                    { 8, true, 30, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Certificado de Salud", true }
                });

            migrationBuilder.InsertData(
                table: "usuario",
                columns: new[] { "Id", "Activo", "BloqueadoHasta", "Correo", "DebeCambiarContrasena", "FechaCreacion", "HashContrasena", "IntentosFallidos", "NombreCompleto", "NombreUsuario", "Perfil", "SucursalId", "UltimoAcceso" },
                values: new object[] { 1, true, null, "cregalado@invayal.hn", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "$2a$11$20vcf0UgN2TSI.LfZxBKZuZf3o6dRwumzQivpJPvjuBcku2hQMPpO", 0, "Cesar Regalado", "cregalado", 1, null, null });

            migrationBuilder.InsertData(
                table: "puesto",
                columns: new[] { "Id", "Activo", "DepartamentoId", "EmpresaId", "FechaCreacion", "Nombre" },
                values: new object[,]
                {
                    { 1, true, 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Gerente General" },
                    { 2, true, 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Asistente Administrativa" },
                    { 3, true, 2, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Contador General" },
                    { 4, true, 2, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Auxiliar Contable" },
                    { 5, true, 3, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Jefe de Ventas" },
                    { 6, true, 3, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Vendedor" },
                    { 7, true, 4, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Jefe de Bodega" },
                    { 8, true, 4, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Auxiliar de Bodega" },
                    { 9, true, 5, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Administrador" },
                    { 10, true, 6, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Coordinador de Logistica" },
                    { 11, true, 6, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Motorista" }
                });

            migrationBuilder.InsertData(
                table: "sucursal",
                columns: new[] { "Id", "Activa", "Codigo", "Direccion", "EmpresaId", "FechaCreacion", "Nombre", "Telefono" },
                values: new object[,]
                {
                    { 1, true, "CM", "Barrio El Centro, Tegucigalpa", 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Casa Matriz", "2234-5600" },
                    { 2, true, "CMY", "Mercado Zonal Belen, Comayaguela", 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sucursal Comayaguela", "2234-5610" },
                    { 3, true, "BC", "Anillo Periferico, Tegucigalpa", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bodega Central", "2234-5710" }
                });

            migrationBuilder.InsertData(
                table: "usuario_empresa",
                columns: new[] { "Id", "EmpresaId", "FechaCreacion", "UsuarioId" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1 }
                });

            migrationBuilder.InsertData(
                table: "colaborador",
                columns: new[] { "Id", "Codigo", "Correo", "DepartamentoId", "Direccion", "EmpresaId", "Estado", "FechaCreacion", "FechaIngreso", "FechaNacimiento", "FechaSalida", "Identidad", "PrimerApellido", "PrimerNombre", "PuestoId", "SalarioBase", "SegundoApellido", "SegundoNombre", "Sexo", "SucursalId", "Telefono" },
                values: new object[,]
                {
                    { 1, "EMP-001", "delmy.cardona@invayal.hn", 2, "Colonia Kennedy, Tegucigalpa", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2018, 2, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1985, 3, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1985-04521", "Cardona", "Delmy", 3, 3250000L, "Rivera", "Suyapa", 2, 1, "9912-4478" },
                    { 2, "EMP-002", "carlos.ayala@invayal.hn", 1, "Residencial El Trapiche, Tegucigalpa", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2015, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1979, 8, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1979-01188", "Ayala", "Carlos", 1, 6800000L, "Alvarenga", "Roberto", 1, 1, "9988-1122" },
                    { 3, "EMP-003", "marlon.discua@invayal.hn", 2, "Barrio La Granja, Comayaguela", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2019, 6, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1990, 11, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1990-07733", "Discua", "Marlon", 4, 1850000L, "Medina", "Josue", 1, 1, "9945-3311" },
                    { 4, "EMP-004", "karla.pineda@invayal.hn", 1, "Colonia Miraflores, Tegucigalpa", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2017, 9, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1988, 5, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1988-05590", "Pineda", "Karla", 2, 1680000L, "Zelaya", "Yolanda", 2, 1, "9922-7788" },
                    { 5, "EMP-005", "jose.mejia@invayal.hn", 3, "Colonia Las Uvas, Tegucigalpa", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2016, 4, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1983, 1, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1983-03310", "Mejia", "Jose", 5, 2900000L, "Fuentes", "Luis", 1, 1, "9933-6644" },
                    { 6, "EMP-006", "andrea.sanchez@invayal.hn", 3, "Barrio Belen, Comayaguela", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1995, 7, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1995-09912", "Sanchez", "Andrea", 6, 1320000L, "Bonilla", "Nicole", 2, 2, "9977-2255" },
                    { 7, "EMP-007", "oscar.flores@invayal.hn", 3, "Colonia Villa Nueva, Comayaguela", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1992, 12, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1992-06654", "Flores", "Oscar", 6, 1320000L, "Cruz", "Danilo", 1, 2, "9966-4433" },
                    { 8, "EMP-008", "wilmer.zuniga@invayal.hn", 4, "Barrio El Manchen, Tegucigalpa", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2018, 11, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1987, 6, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1987-02245", "Zuniga", "Wilmer", 7, 2100000L, "Lopez", "Antonio", 1, 2, "9955-8877" },
                    { 9, "EMP-009", "gabriela.romero@invayal.hn", 4, "Colonia San Miguel, Comayaguela", 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 5, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1998, 9, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1998-08876", "Romero", "Gabriela", 8, 1150000L, "Castillo", "Michelle", 2, 2, "9944-1199" },
                    { 10, "COM-001", "reina.alvarenga@comalvarenga.hn", 5, "Colonia Palmira, Tegucigalpa", 2, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2019, 2, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1981, 4, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1981-04432", "Alvarenga", "Reina", 9, 4500000L, "Portillo", "Isabel", 2, 3, "9911-3366" },
                    { 11, "COM-002", "edwin.turcios@comalvarenga.hn", 6, "Colonia Cerro Grande, Tegucigalpa", 2, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1993, 2, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1993-07701", "Turcios", "Edwin", 10, 2400000L, "Maradiaga", "Alexander", 1, 3, "9900-5544" },
                    { 12, "COM-003", "nelson.padilla@comalvarenga.hn", 6, "Barrio Concepcion, Comayaguela", 2, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 7, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1986, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "0801-1986-01123", "Padilla", "Nelson", 11, 1580000L, "Erazo", "Ramon", 1, 3, "9899-6677" }
                });

            migrationBuilder.InsertData(
                table: "contacto_emergencia",
                columns: new[] { "Id", "ColaboradorId", "EmpresaId", "EsPrincipal", "FechaCreacion", "Nombre", "Parentesco", "Telefono", "TelefonoAlterno" },
                values: new object[,]
                {
                    { 1, 1, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Hector Cardona", "Padre", "9812-4400", "" },
                    { 2, 1, 1, false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Lesly Rivera", "Hermana", "9813-5511", "" },
                    { 3, 2, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sonia Alvarenga", "Esposa", "9814-6622", "" },
                    { 4, 3, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rosa Medina", "Madre", "9815-7733", "" },
                    { 5, 4, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Julio Pineda", "Esposo", "9816-8844", "" },
                    { 6, 5, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ana Fuentes", "Madre", "9817-9955", "" },
                    { 7, 6, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Mario Bonilla", "Padre", "9818-1166", "" },
                    { 8, 7, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Iris Cruz", "Madre", "9819-2277", "" },
                    { 9, 8, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Dina Lopez", "Esposa", "9820-3388", "" },
                    { 10, 9, 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sandra Castillo", "Madre", "9821-4499", "" },
                    { 11, 10, 2, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Mario Portillo", "Hermano", "9822-5500", "" },
                    { 12, 11, 2, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Cindy Maradiaga", "Esposa", "9823-6611", "" },
                    { 13, 12, 2, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Elsa Erazo", "Madre", "9824-7722", "" }
                });

            migrationBuilder.InsertData(
                table: "contrato",
                columns: new[] { "Id", "ColaboradorId", "EmpresaId", "FechaCreacion", "FechaFin", "FechaInicio", "Numero", "Observacion", "SalarioAcordado", "TipoContratoId", "Vigente" },
                values: new object[,]
                {
                    { 1, 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2018, 2, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2018-001", "", 3250000L, 1, true },
                    { 2, 2, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2015, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2015-001", "", 6800000L, 1, true },
                    { 3, 3, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2019, 6, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2019-004", "", 1850000L, 1, true },
                    { 4, 4, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2017, 9, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2017-009", "", 1680000L, 1, true },
                    { 5, 5, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2016, 4, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2016-002", "", 2900000L, 1, true },
                    { 6, 6, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2021, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2021-011", "", 1320000L, 2, true },
                    { 7, 7, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2020, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2020-018", "", 1320000L, 2, true },
                    { 8, 8, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2018, 11, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2018-021", "", 2100000L, 1, true },
                    { 9, 9, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2022, 5, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "CT-2022-006", "", 1150000L, 2, true },
                    { 10, 10, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2019, 2, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), "CC-2019-001", "", 4500000L, 4, true },
                    { 11, 11, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2020, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "CC-2020-003", "", 2400000L, 4, true },
                    { 12, 12, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2021, 7, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), "CC-2021-007", "", 1580000L, 5, true }
                });

            migrationBuilder.InsertData(
                table: "documento_digitalizado",
                columns: new[] { "Id", "ColaboradorId", "EmpresaId", "FechaCreacion", "FechaEmision", "FechaVencimiento", "NombreArchivo", "RutaRelativa", "TamanoBytes", "TipoDocumentoId" },
                values: new object[,]
                {
                    { 1, 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2015, 6, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "identidad-delmy-cardona.pdf", "1/1/identidad-delmy-cardona.pdf", 184320L, 1 },
                    { 2, 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2007, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "titulo-perito-mercantil.pdf", "1/1/titulo-perito-mercantil.pdf", 512000L, 5 },
                    { 3, 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "certificado-salud-2025.pdf", "1/1/certificado-salud-2025.pdf", 96256L, 4 },
                    { 4, 2, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2014, 3, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "identidad-carlos-ayala.pdf", "1/2/identidad-carlos-ayala.pdf", 178176L, 1 },
                    { 5, 3, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2016, 8, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "identidad-marlon-discua.pdf", "1/3/identidad-marlon-discua.pdf", 169984L, 1 },
                    { 6, 3, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 7, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), "constancia-policial-2025.pdf", "1/3/constancia-policial-2025.pdf", 88064L, 3 },
                    { 7, 6, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "certificado-salud-andrea.pdf", "1/6/certificado-salud-andrea.pdf", 91136L, 4 },
                    { 8, 8, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2013, 5, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "identidad-wilmer-zuniga.pdf", "1/8/identidad-wilmer-zuniga.pdf", 175104L, 1 },
                    { 9, 10, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2012, 9, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "identidad-reina-alvarenga.pdf", "2/10/identidad-reina-alvarenga.pdf", 182272L, 6 },
                    { 10, 12, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2023, 10, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "licencia-nelson-padilla.pdf", "2/12/licencia-nelson-padilla.pdf", 76800L, 7 }
                });

            migrationBuilder.InsertData(
                table: "movimiento_laboral",
                columns: new[] { "Id", "ColaboradorId", "EmpresaId", "Fecha", "FechaCreacion", "Observacion", "PuestoAnteriorId", "PuestoNuevoId", "SalarioAnterior", "SalarioNuevo", "SucursalAnteriorId", "SucursalNuevaId", "Tipo" },
                values: new object[,]
                {
                    { 1, 1, 1, new DateTime(2018, 2, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Auxiliar Contable en Casa Matriz.", null, 4, null, 1400000L, null, 1, 1 },
                    { 2, 1, 1, new DateTime(2019, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ajuste anual por desempeno.", null, null, 1400000L, 1750000L, null, null, 3 },
                    { 3, 1, 1, new DateTime(2020, 7, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Promocion a Contador General.", 4, 3, 1750000L, 2600000L, null, null, 2 },
                    { 4, 1, 1, new DateTime(2022, 4, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ajuste por antiguedad.", null, null, 2600000L, 2980000L, null, null, 3 },
                    { 5, 1, 1, new DateTime(2024, 2, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Revision salarial 2024.", null, null, 2980000L, 3250000L, null, null, 3 },
                    { 6, 2, 1, new DateTime(2015, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Gerente General.", null, 1, null, 5500000L, null, 1, 1 },
                    { 7, 2, 1, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Revision de la gerencia.", null, null, 5500000L, 6800000L, null, null, 3 },
                    { 8, 3, 1, new DateTime(2019, 6, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Auxiliar Contable.", null, 4, null, 1500000L, null, 1, 1 },
                    { 9, 3, 1, new DateTime(2023, 6, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ajuste anual.", null, null, 1500000L, 1850000L, null, null, 3 },
                    { 10, 4, 1, new DateTime(2017, 9, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Asistente Administrativa.", null, 2, null, 1680000L, null, 1, 1 },
                    { 11, 5, 1, new DateTime(2016, 4, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Vendedor.", null, 6, null, 1200000L, null, 1, 1 },
                    { 12, 5, 1, new DateTime(2019, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Promocion a Jefe de Ventas.", 6, 5, 1200000L, 2900000L, null, null, 2 },
                    { 13, 6, 1, new DateTime(2021, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Vendedora.", null, 6, null, 1320000L, null, 2, 1 },
                    { 14, 7, 1, new DateTime(2020, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Vendedor.", null, 6, null, 1320000L, null, 2, 1 },
                    { 15, 8, 1, new DateTime(2018, 11, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Auxiliar de Bodega.", null, 8, null, 1100000L, null, 2, 1 },
                    { 16, 8, 1, new DateTime(2022, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Promocion a Jefe de Bodega.", 8, 7, 1100000L, 2100000L, null, null, 2 },
                    { 17, 9, 1, new DateTime(2022, 5, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Auxiliar de Bodega.", null, 8, null, 1150000L, null, 2, 1 },
                    { 18, 10, 2, new DateTime(2019, 2, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Administradora.", null, 9, null, 3800000L, null, 3, 1 },
                    { 19, 10, 2, new DateTime(2023, 2, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ajuste por antiguedad.", null, null, 3800000L, 4500000L, null, null, 3 },
                    { 20, 11, 2, new DateTime(2020, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Coordinador de Logistica.", null, 10, null, 2400000L, null, 3, 1 },
                    { 21, 12, 2, new DateTime(2021, 7, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ingreso como Motorista.", null, 11, null, 1580000L, null, 3, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_aviso_ColaboradorId",
                table: "aviso",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_aviso_EmpresaId_ClaveIdempotencia",
                table: "aviso",
                columns: new[] { "EmpresaId", "ClaveIdempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_colaborador_DepartamentoId",
                table: "colaborador",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_colaborador_EmpresaId_Codigo",
                table: "colaborador",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_colaborador_EmpresaId_Identidad",
                table: "colaborador",
                columns: new[] { "EmpresaId", "Identidad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_colaborador_PuestoId",
                table: "colaborador",
                column: "PuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_colaborador_SucursalId",
                table: "colaborador",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_contacto_emergencia_ColaboradorId",
                table: "contacto_emergencia",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_ColaboradorId",
                table: "contrato",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_TipoContratoId",
                table: "contrato",
                column: "TipoContratoId");

            migrationBuilder.CreateIndex(
                name: "IX_departamento_EmpresaId_Nombre",
                table: "departamento",
                columns: new[] { "EmpresaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documento_digitalizado_ColaboradorId",
                table: "documento_digitalizado",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_documento_digitalizado_TipoDocumentoId",
                table: "documento_digitalizado",
                column: "TipoDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_empresa_Rtn",
                table: "empresa",
                column: "Rtn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_laboral_ColaboradorId",
                table: "movimiento_laboral",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_puesto_DepartamentoId",
                table: "puesto",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_sucursal_EmpresaId_Codigo",
                table: "sucursal",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_NombreUsuario",
                table: "usuario",
                column: "NombreUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_empresa_EmpresaId",
                table: "usuario_empresa",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_empresa_UsuarioId_EmpresaId",
                table: "usuario_empresa",
                columns: new[] { "UsuarioId", "EmpresaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aviso");

            migrationBuilder.DropTable(
                name: "contacto_emergencia");

            migrationBuilder.DropTable(
                name: "contrato");

            migrationBuilder.DropTable(
                name: "documento_digitalizado");

            migrationBuilder.DropTable(
                name: "movimiento_laboral");

            migrationBuilder.DropTable(
                name: "usuario_empresa");

            migrationBuilder.DropTable(
                name: "tipo_contrato");

            migrationBuilder.DropTable(
                name: "tipo_documento");

            migrationBuilder.DropTable(
                name: "colaborador");

            migrationBuilder.DropTable(
                name: "usuario");

            migrationBuilder.DropTable(
                name: "puesto");

            migrationBuilder.DropTable(
                name: "sucursal");

            migrationBuilder.DropTable(
                name: "departamento");

            migrationBuilder.DropTable(
                name: "empresa");
        }
    }
}
