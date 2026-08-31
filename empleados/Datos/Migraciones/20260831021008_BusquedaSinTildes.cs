using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace empleados.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class BusquedaSinTildes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextoBusqueda",
                table: "colaborador",
                type: "TEXT",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_colaborador_EmpresaId_TextoBusqueda",
                table: "colaborador",
                columns: new[] { "EmpresaId", "TextoBusqueda" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_colaborador_EmpresaId_TextoBusqueda",
                table: "colaborador");

            migrationBuilder.DropColumn(
                name: "TextoBusqueda",
                table: "colaborador");
        }
    }
}
