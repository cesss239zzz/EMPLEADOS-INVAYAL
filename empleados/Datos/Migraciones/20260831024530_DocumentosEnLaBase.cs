using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace empleados.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class DocumentosEnLaBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RutaRelativa",
                table: "documento_digitalizado");

            migrationBuilder.AddColumn<string>(
                name: "EscalaAviso",
                table: "tipo_documento",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "documento_digitalizado",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Extension",
                table: "documento_digitalizado",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "contenido_documento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Bytes = table.Column<byte[]>(type: "BLOB", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contenido_documento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contenido_documento_documento_digitalizado_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documento_digitalizado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movimiento_documento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ColaboradorId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentoId = table.Column<int>(type: "INTEGER", nullable: true),
                    NombreDocumento = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Accion = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    NombreUsuario = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento_documento", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contenido_documento_DocumentoId",
                table: "contenido_documento",
                column: "DocumentoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_documento_EmpresaId_ColaboradorId_Fecha",
                table: "movimiento_documento",
                columns: new[] { "EmpresaId", "ColaboradorId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contenido_documento");

            migrationBuilder.DropTable(
                name: "movimiento_documento");

            migrationBuilder.DropColumn(
                name: "EscalaAviso",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "documento_digitalizado");

            migrationBuilder.DropColumn(
                name: "Extension",
                table: "documento_digitalizado");

            migrationBuilder.AddColumn<string>(
                name: "RutaRelativa",
                table: "documento_digitalizado",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
