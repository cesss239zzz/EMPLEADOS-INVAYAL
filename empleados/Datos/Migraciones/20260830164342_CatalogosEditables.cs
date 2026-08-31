using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace empleados.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class CatalogosEditables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MesesVigencia",
                table: "tipo_documento",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DepartamentoId",
                table: "puesto",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MesesVigencia",
                table: "tipo_documento");

            migrationBuilder.AlterColumn<int>(
                name: "DepartamentoId",
                table: "puesto",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
