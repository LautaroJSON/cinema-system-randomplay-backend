using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystemRandomPlay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPagoDeCarritos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FallidoEn",
                table: "OutboxMensajes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Intentos",
                table: "OutboxMensajes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProximoIntentoEn",
                table: "OutboxMensajes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimoError",
                table: "OutboxMensajes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EntregaEnviadaEn",
                table: "Carritos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PagadoEn",
                table: "Carritos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PagoAprobadoEn",
                table: "Carritos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PagoId",
                table: "Carritos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PagoIniciadoEn",
                table: "Carritos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferenciaPagoId",
                table: "Carritos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlPago",
                table: "Carritos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Devoluciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PagoId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CarritoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SolicitadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResueltaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UltimoError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devoluciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entradas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    CarritoId = table.Column<Guid>(type: "uuid", nullable: false),
                    FuncionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fila = table.Column<char>(type: "character(1)", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    PeliculaTitulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SucursalNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SalaNombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HoraInicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EmitidaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrecioUnitario_Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PrecioUnitario_Monto = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entradas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entradas_Carritos_CarritoId",
                        column: x => x.CarritoId,
                        principalTable: "Carritos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PagosProcesados",
                columns: table => new
                {
                    PagoId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CarritoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AprobadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProcesadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosProcesados", x => x.PagoId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMensajes_ProcesadoEn_FallidoEn_ProximoIntentoEn",
                table: "OutboxMensajes",
                columns: new[] { "ProcesadoEn", "FallidoEn", "ProximoIntentoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_CarritoId",
                table: "Devoluciones",
                column: "CarritoId");

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_PagoId",
                table: "Devoluciones",
                column: "PagoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entradas_CarritoId",
                table: "Entradas",
                column: "CarritoId");

            migrationBuilder.CreateIndex(
                name: "IX_Entradas_Codigo",
                table: "Entradas",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entradas_FuncionId_Fila_Numero",
                table: "Entradas",
                columns: new[] { "FuncionId", "Fila", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagosProcesados_CarritoId",
                table: "PagosProcesados",
                column: "CarritoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Devoluciones");

            migrationBuilder.DropTable(
                name: "Entradas");

            migrationBuilder.DropTable(
                name: "PagosProcesados");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMensajes_ProcesadoEn_FallidoEn_ProximoIntentoEn",
                table: "OutboxMensajes");

            migrationBuilder.DropColumn(
                name: "FallidoEn",
                table: "OutboxMensajes");

            migrationBuilder.DropColumn(
                name: "Intentos",
                table: "OutboxMensajes");

            migrationBuilder.DropColumn(
                name: "ProximoIntentoEn",
                table: "OutboxMensajes");

            migrationBuilder.DropColumn(
                name: "UltimoError",
                table: "OutboxMensajes");

            migrationBuilder.DropColumn(
                name: "EntregaEnviadaEn",
                table: "Carritos");

            migrationBuilder.DropColumn(
                name: "PagadoEn",
                table: "Carritos");

            migrationBuilder.DropColumn(
                name: "PagoAprobadoEn",
                table: "Carritos");

            migrationBuilder.DropColumn(
                name: "PagoId",
                table: "Carritos");

            migrationBuilder.DropColumn(
                name: "PagoIniciadoEn",
                table: "Carritos");

            migrationBuilder.DropColumn(
                name: "PreferenciaPagoId",
                table: "Carritos");

            migrationBuilder.DropColumn(
                name: "UrlPago",
                table: "Carritos");
        }
    }
}
