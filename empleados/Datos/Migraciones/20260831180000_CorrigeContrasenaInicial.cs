using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace empleados.Datos.Migraciones
{
    /// <summary>
    /// Corrige la contrasena del usuario inicial.
    ///
    /// El hash que sembro la migracion inicial no correspondia a la contrasena
    /// que se le entrego al cliente: se genero un hash y la clave en claro no
    /// quedo anotada en ninguna parte. El resultado es que "cregalado" no podia
    /// entrar en ninguna maquina, y como cada intento fallido suma, a los cinco
    /// la cuenta quedaba bloqueada quince minutos.
    ///
    /// Va en una migracion nueva y no tocando la inicial, que ya esta aplicada
    /// (CLAUDE.md, prohibiciones de proceso). Asi quedan arregladas por igual
    /// las bases que ya existen y las que se creen desde cero.
    ///
    /// La contrasena en claro sigue sin aparecer aca: solo su hash BCrypt de
    /// factor 11 (CLAUDE.md, regla 12).
    /// </summary>
    public partial class CorrigeContrasenaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "usuario",
                keyColumn: "Id",
                keyValue: 1,
                column: "HashContrasena",
                value: "$2a$11$l09yi7nbnHdRTs./Q8HWEOL4yotSx1COXRbV/LP7N.wuZZJekDU9C");

            // Los intentos fallidos acumulados y un bloqueo vigente dejarian la
            // cuenta trabada justo despues de arreglarle la contrasena.
            migrationBuilder.UpdateData(
                table: "usuario",
                keyColumn: "Id",
                keyValue: 1,
                column: "IntentosFallidos",
                value: 0);

            migrationBuilder.UpdateData(
                table: "usuario",
                keyColumn: "Id",
                keyValue: 1,
                column: "BloqueadoHasta",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "usuario",
                keyColumn: "Id",
                keyValue: 1,
                column: "HashContrasena",
                value: "$2a$11$20vcf0UgN2TSI.LfZxBKZuZf3o6dRwumzQivpJPvjuBcku2hQMPpO");
        }
    }
}
