using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystemRandomPlay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrecios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrecioUnitario_Moneda",
                table: "Reservas",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioUnitario_Monto",
                table: "Reservas",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PrecioEntrada_Moneda",
                table: "Funciones",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioEntrada_Monto",
                table: "Funciones",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 5500.00m);

            // Las reservas existentes congelan el precio actual de su función.
            migrationBuilder.Sql("""
                UPDATE "Reservas" r
                SET "PrecioUnitario_Monto" = f."PrecioEntrada_Monto",
                    "PrecioUnitario_Moneda" = f."PrecioEntrada_Moneda"
                FROM "Funciones" f
                WHERE r."FuncionId" = f."Id";
                """);

            // Los defaults eran solo para rellenar filas existentes: las filas nuevas deben traer su precio.
            migrationBuilder.Sql("""
                ALTER TABLE "Funciones" ALTER COLUMN "PrecioEntrada_Monto" DROP DEFAULT;
                ALTER TABLE "Funciones" ALTER COLUMN "PrecioEntrada_Moneda" DROP DEFAULT;
                ALTER TABLE "Reservas" ALTER COLUMN "PrecioUnitario_Monto" DROP DEFAULT;
                ALTER TABLE "Reservas" ALTER COLUMN "PrecioUnitario_Moneda" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecioUnitario_Moneda",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "PrecioUnitario_Monto",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "PrecioEntrada_Moneda",
                table: "Funciones");

            migrationBuilder.DropColumn(
                name: "PrecioEntrada_Monto",
                table: "Funciones");
        }
    }
}
