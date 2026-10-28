using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystemRandomPlay.Infrastructure.Migrations
{
    /// <summary>
    /// La reserva pasa de pertenecer a un identificador de usuario (<c>ClienteId</c>) a un email normalizado
    /// (<c>CompradorEmail</c>). Las reservas existentes no tienen forma de asignarse a un email, así que se descartan
    /// de forma explícita (FR-021). Para no perder datos reales en silencio, la migración se niega a hacerlo si hay
    /// reservas y no se tomó la decisión con <c>CINEMA_PERMITIR_DESCARTE_RESERVAS=true</c>.
    /// </summary>
    public partial class AddCompradorEmailEnReservas : Migration
    {
        public const string VariablePermiteDescarte = "CINEMA_PERMITIR_DESCARTE_RESERVAS";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var descarteAutorizado = string.Equals(
                Environment.GetEnvironmentVariable(VariablePermiteDescarte), "true", StringComparison.OrdinalIgnoreCase);

            if (!descarteAutorizado)
            {
                migrationBuilder.Sql($$"""
                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM "Reservas") THEN
                            RAISE EXCEPTION 'Esta migración descarta todas las reservas existentes (pertenecen a identificadores de usuario y no a emails). Para confirmarlo, definir {{VariablePermiteDescarte}}=true y volver a aplicarla.';
                        END IF;
                    END
                    $$;
                    """);
            }

            migrationBuilder.Sql("""DELETE FROM "ReservaAsientos";""");
            migrationBuilder.Sql("""DELETE FROM "Reservas";""");
            migrationBuilder.Sql("""DELETE FROM "OutboxMensajes" WHERE "Tipo" IN ('ReservaCreada', 'ReservaCancelada');""");

            migrationBuilder.RenameColumn(
                name: "ClienteId",
                table: "Reservas",
                newName: "CompradorEmail");

            migrationBuilder.AlterColumn<string>(
                name: "CompradorEmail",
                table: "Reservas",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.RenameIndex(
                name: "IX_Reservas_ClienteId_CreadaEn",
                table: "Reservas",
                newName: "IX_Reservas_CompradorEmail_CreadaEn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Reservas_CompradorEmail_CreadaEn",
                table: "Reservas",
                newName: "IX_Reservas_ClienteId_CreadaEn");

            migrationBuilder.AlterColumn<string>(
                name: "CompradorEmail",
                table: "Reservas",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.RenameColumn(
                name: "CompradorEmail",
                table: "Reservas",
                newName: "ClienteId");
        }
    }
}
