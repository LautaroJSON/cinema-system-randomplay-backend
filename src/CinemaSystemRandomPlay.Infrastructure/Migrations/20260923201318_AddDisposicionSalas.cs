using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CinemaSystemRandomPlay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisposicionSalas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalaFilas",
                columns: table => new
                {
                    Letra = table.Column<char>(type: "character(1)", nullable: false),
                    SalaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CantidadAsientos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaFilas", x => new { x.SalaId, x.Letra });
                    table.ForeignKey(
                        name: "FK_SalaFilas_Salas_SalaId",
                        column: x => x.SalaId,
                        principalTable: "Salas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SalaFilas",
                columns: new[] { "Letra", "SalaId", "CantidadAsientos" },
                values: new object[,]
                {
                    { 'A', new Guid("22222222-0000-0000-0000-000000000001"), 12 },
                    { 'B', new Guid("22222222-0000-0000-0000-000000000001"), 12 },
                    { 'C', new Guid("22222222-0000-0000-0000-000000000001"), 12 },
                    { 'D', new Guid("22222222-0000-0000-0000-000000000001"), 12 },
                    { 'E', new Guid("22222222-0000-0000-0000-000000000001"), 12 },
                    { 'F', new Guid("22222222-0000-0000-0000-000000000001"), 12 },
                    { 'G', new Guid("22222222-0000-0000-0000-000000000001"), 12 },
                    { 'H', new Guid("22222222-0000-0000-0000-000000000001"), 12 },
                    { 'A', new Guid("22222222-0000-0000-0000-000000000002"), 8 },
                    { 'B', new Guid("22222222-0000-0000-0000-000000000002"), 10 },
                    { 'C', new Guid("22222222-0000-0000-0000-000000000002"), 12 },
                    { 'D', new Guid("22222222-0000-0000-0000-000000000002"), 12 },
                    { 'E', new Guid("22222222-0000-0000-0000-000000000002"), 14 },
                    { 'F', new Guid("22222222-0000-0000-0000-000000000002"), 14 },
                    { 'A', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'B', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'C', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'D', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'E', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'F', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'G', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'H', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'I', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'J', new Guid("22222222-0000-0000-0000-000000000003"), 10 },
                    { 'A', new Guid("22222222-0000-0000-0000-000000000004"), 8 },
                    { 'B', new Guid("22222222-0000-0000-0000-000000000004"), 8 },
                    { 'C', new Guid("22222222-0000-0000-0000-000000000004"), 8 },
                    { 'D', new Guid("22222222-0000-0000-0000-000000000004"), 8 },
                    { 'E', new Guid("22222222-0000-0000-0000-000000000004"), 8 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalaFilas");
        }
    }
}
