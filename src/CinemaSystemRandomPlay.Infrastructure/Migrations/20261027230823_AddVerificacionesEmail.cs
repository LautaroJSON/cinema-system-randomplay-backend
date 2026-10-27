using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystemRandomPlay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificacionesEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VerificacionesEmail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CodigoHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificacionesEmail", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificacionesEmail");
        }
    }
}
