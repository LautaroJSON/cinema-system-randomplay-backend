using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystemRandomPlay.Infrastructure.Migrations
{
    /// <summary>
    /// La reserva por email de la feature 006 pasa a ser el carrito anónimo de la feature 007. Las reservas existentes tienen
    /// un email como dueño y ningún token: nadie podría operarlas, así que se descartan de forma explícita (FR-024), junto con
    /// sus mensajes del outbox. Para no perder datos reales en silencio, la migración se niega a hacerlo si hay reservas y no
    /// se tomó la decisión con <c>CINEMA_PERMITIR_DESCARTE_RESERVAS=true</c>, igual que <see cref="AddCompradorEmailEnReservas"/>.
    /// También elimina la tabla <c>VerificacionesEmail</c>: guarda códigos de un solo uso de corta vida, no datos a preservar.
    /// </summary>
    public partial class ReemplazarReservasPorCarritos : Migration
    {
        public const string VariablePermiteDescarte = AddCompradorEmailEnReservas.VariablePermiteDescarte;

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
                            RAISE EXCEPTION 'Esta migración descarta todas las reservas existentes (tienen un email como dueño y ningún token de carrito). Para confirmarlo, definir {{VariablePermiteDescarte}}=true y volver a aplicarla.';
                        END IF;
                    END
                    $$;
                    """);
            }

            migrationBuilder.Sql("""DELETE FROM "OutboxMensajes" WHERE "Tipo" IN ('ReservaCreada', 'ReservaCancelada');""");

            migrationBuilder.DropTable(
                name: "ReservaAsientos");

            migrationBuilder.DropTable(
                name: "VerificacionesEmail");

            migrationBuilder.DropTable(
                name: "Reservas");

            migrationBuilder.CreateTable(
                name: "Carritos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FuncionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AbandonadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmailEntrega = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    PrecioUnitario_Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PrecioUnitario_Monto = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carritos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Carritos_Funciones_FuncionId",
                        column: x => x.FuncionId,
                        principalTable: "Funciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CarritoAsientos",
                columns: table => new
                {
                    Fila = table.Column<char>(type: "character(1)", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    CarritoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarritoAsientos", x => new { x.CarritoId, x.Fila, x.Numero });
                    table.ForeignKey(
                        name: "FK_CarritoAsientos_Carritos_CarritoId",
                        column: x => x.CarritoId,
                        principalTable: "Carritos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Carritos_FuncionId_Estado_VenceEn",
                table: "Carritos",
                columns: new[] { "FuncionId", "Estado", "VenceEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarritoAsientos");

            migrationBuilder.DropTable(
                name: "Carritos");

            migrationBuilder.CreateTable(
                name: "Reservas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanceladaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompradorEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FuncionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrecioUnitario_Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PrecioUnitario_Monto = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservas_Funciones_FuncionId",
                        column: x => x.FuncionId,
                        principalTable: "Funciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VerificacionesEmail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IntentosFallidos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    VenceEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificacionesEmail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReservaAsientos",
                columns: table => new
                {
                    ReservaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fila = table.Column<char>(type: "character(1)", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservaAsientos", x => new { x.ReservaId, x.Fila, x.Numero });
                    table.ForeignKey(
                        name: "FK_ReservaAsientos_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_CompradorEmail_CreadaEn",
                table: "Reservas",
                columns: new[] { "CompradorEmail", "CreadaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_FuncionId_Estado_VenceEn",
                table: "Reservas",
                columns: new[] { "FuncionId", "Estado", "VenceEn" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificacionesEmail_Email_CreadaEn",
                table: "VerificacionesEmail",
                columns: new[] { "Email", "CreadaEn" });

            migrationBuilder.CreateIndex(
                name: "UX_VerificacionesEmail_Email_Vigente",
                table: "VerificacionesEmail",
                column: "Email",
                unique: true,
                filter: "\"Estado\" = 'Vigente'");
        }
    }
}
