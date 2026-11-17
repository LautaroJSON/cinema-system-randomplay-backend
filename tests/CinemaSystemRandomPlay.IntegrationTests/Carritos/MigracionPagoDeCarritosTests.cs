using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>La migración de la feature 008 solo agrega: los carritos existentes siguen intactos.</summary>
public class MigracionPagoDeCarritosTests : IAsyncLifetime
{
    private const string MigracionPrevia = "20261101105854_AddImagenUrlAPeliculas";
    private const string Migracion = "AgregarPagoDeCarritos";

    private readonly PostgresDePrueba _postgres = new();

    public Task InitializeAsync() => _postgres.Iniciar();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private async Task MigrarHasta(string? migracion = null)
    {
        await using var dbContext = new CinemaDbContext(new DbContextOptionsBuilder<CinemaDbContext>().UseNpgsql(_postgres.ConnectionString).Options);
        var migrador = dbContext.GetService<IMigrator>();
        var nombre = migracion is null ? null : dbContext.Database.GetMigrations().Single(m => m.EndsWith(migracion));
        await migrador.MigrateAsync(nombre);
    }

    private async Task<object?> Escalar(string sql)
    {
        await using var conexion = new NpgsqlConnection(_postgres.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return await comando.ExecuteScalarAsync();
    }

    [Fact]
    public async Task ConservaLosCarritosExistentesYCreaLosIndicesUnicos()
    {
        await MigrarHasta(MigracionPrevia);
        await Escalar($"""
            INSERT INTO "Peliculas" ("Id", "Titulo", "DuracionMinutos", "Clasificacion", "Sinopsis", "Activa")
            VALUES ('aaaaaaaa-0000-0000-0000-000000000001', 'Peli', 100, 'ATP', 'Sinopsis', true);
            INSERT INTO "Funciones" ("Id", "PeliculaId", "SalaId", "FechaHoraInicio", "PrecioEntrada_Monto", "PrecioEntrada_Moneda")
            VALUES ('bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000001', '{SalaEntityConfiguration.SalaCentro1Id}', now() + interval '3 hours', 5500.00, 'ARS');
            INSERT INTO "Carritos" ("Id", "FuncionId", "Estado", "CreadoEn", "VenceEn", "EmailEntrega", "PrecioUnitario_Monto", "PrecioUnitario_Moneda")
            VALUES ('cccccccc-0000-0000-0000-000000000001', 'bbbbbbbb-0000-0000-0000-000000000001', 'Activo', now(), now() + interval '5 minutes', 'ana@example.com', 5500.00, 'ARS');
            INSERT INTO "CarritoAsientos" ("CarritoId", "Fila", "Numero") VALUES ('cccccccc-0000-0000-0000-000000000001', 'A', 1);
            """);

        await MigrarHasta(Migracion);

        Assert.Equal(1L, await Escalar("""SELECT count(*) FROM "Carritos" WHERE "Estado" = 'Activo' AND "UrlPago" IS NULL AND "PagadoEn" IS NULL"""));
        Assert.Equal(1L, await Escalar("""SELECT count(*) FROM "CarritoAsientos" """));
        foreach (var indice in new[] { "IX_Entradas_Codigo", "IX_Entradas_FuncionId_Fila_Numero", "IX_Devoluciones_PagoId" })
            Assert.Equal(true, await Escalar($"SELECT indexdef LIKE 'CREATE UNIQUE%' FROM pg_indexes WHERE indexname = '{indice}'"));
        Assert.Equal(1L, await Escalar("""SELECT count(*) FROM information_schema.tables WHERE table_name = 'PagosProcesados'"""));
    }
}
