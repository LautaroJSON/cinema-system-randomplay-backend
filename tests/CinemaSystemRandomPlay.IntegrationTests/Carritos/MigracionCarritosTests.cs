using CinemaSystemRandomPlay.Infrastructure.Migrations;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>Las migraciones que descartan reservas leen la misma variable de entorno del proceso: no deben correr en paralelo.</summary>
[CollectionDefinition(Nombre)]
public class VariableDeDescarteCollection
{
    public const string Nombre = "Variable de descarte de reservas";
}

/// <summary>
/// FR-024: la migración que reemplaza las reservas por carritos descarta las reservas existentes solo si alguien lo decidió de
/// forma explícita; sin esa decisión se niega a tocar datos. Además elimina la tabla de verificaciones de email de la 006.
/// </summary>
[Collection(VariableDeDescarteCollection.Nombre)]
public class MigracionCarritosTests : IAsyncLifetime
{
    private const string MigracionPrevia = "20261027230823_AddVerificacionesEmail";
    private const string Variable = ReemplazarReservasPorCarritos.VariablePermiteDescarte;

    private readonly PostgresDePrueba _postgres = new();
    private string? _valorOriginal;

    public async Task InitializeAsync()
    {
        _valorOriginal = Environment.GetEnvironmentVariable(Variable);
        Environment.SetEnvironmentVariable(Variable, null);
        await _postgres.Iniciar();
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(Variable, _valorOriginal);
        await _postgres.DisposeAsync();
    }

    private async Task MigrarHasta(string? migracion = null)
    {
        await using var dbContext = new CinemaDbContext(new DbContextOptionsBuilder<CinemaDbContext>().UseNpgsql(_postgres.ConnectionString).Options);
        await dbContext.GetService<IMigrator>().MigrateAsync(migracion);
    }

    private async Task<long> Escalar(string sql)
    {
        await using var conexion = new NpgsqlConnection(_postgres.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return Convert.ToInt64(await comando.ExecuteScalarAsync());
    }

    private Task<long> CantidadDeTablas(string tabla) =>
        Escalar($"SELECT count(*) FROM information_schema.tables WHERE table_name = '{tabla}'");

    private async Task SembrarReservaVerificacionYOutbox()
    {
        var sala = SalaEntityConfiguration.SalaCentro1Id;
        const string json = "'{}'::jsonb";

        await using var conexion = new NpgsqlConnection(_postgres.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand($"""
            INSERT INTO "Peliculas" ("Id", "Titulo", "DuracionMinutos", "Clasificacion", "Sinopsis", "Activa")
            VALUES ('aaaaaaaa-0000-0000-0000-000000000001', 'Peli', 100, 'ATP', 'Sinopsis', true);
            INSERT INTO "Funciones" ("Id", "PeliculaId", "SalaId", "FechaHoraInicio", "PrecioEntrada_Monto", "PrecioEntrada_Moneda")
            VALUES ('bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000001', '{sala}', now() + interval '3 hours', 5500.00, 'ARS');
            INSERT INTO "Reservas" ("Id", "FuncionId", "CompradorEmail", "Estado", "CreadaEn", "VenceEn", "PrecioUnitario_Monto", "PrecioUnitario_Moneda")
            VALUES ('cccccccc-0000-0000-0000-000000000001', 'bbbbbbbb-0000-0000-0000-000000000001', 'ana@example.com', 'PendienteDePago', now(), now() + interval '10 minutes', 5500.00, 'ARS');
            INSERT INTO "ReservaAsientos" ("ReservaId", "Fila", "Numero")
            VALUES ('cccccccc-0000-0000-0000-000000000001', 'A', 1);
            INSERT INTO "VerificacionesEmail" ("Id", "Email", "CodigoHash", "Estado", "CreadaEn", "VenceEn", "IntentosFallidos")
            VALUES ('eeeeeeee-0000-0000-0000-000000000001', 'ana@example.com', 'hash', 'Vigente', now(), now() + interval '10 minutes', 0);
            INSERT INTO "OutboxMensajes" ("Id", "Tipo", "Payload", "OcurridoEn")
            VALUES ('dddddddd-0000-0000-0000-000000000001', 'ReservaCreada', {json}, now()),
                   ('dddddddd-0000-0000-0000-000000000002', 'ReservaCancelada', {json}, now()),
                   ('dddddddd-0000-0000-0000-000000000003', 'OtroEvento', {json}, now());
            """, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task SinDecisionExplicitaYConReservas_LaMigracionFallaYNoBorraNada()
    {
        await MigrarHasta(MigracionPrevia);
        await SembrarReservaVerificacionYOutbox();

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => MigrarHasta());

        Assert.Contains(Variable, ex.ToString());
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM "Reservas" """));
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM "ReservaAsientos" """));
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM "VerificacionesEmail" """));
        Assert.Equal(3, await Escalar("""SELECT count(*) FROM "OutboxMensajes" """));
        Assert.Equal(0, await CantidadDeTablas("Carritos"));
    }

    [Fact]
    public async Task ConDecisionExplicita_DescartaReservasVerificacionesYSusMensajesYCreaLosCarritos()
    {
        await MigrarHasta(MigracionPrevia);
        await SembrarReservaVerificacionYOutbox();
        Environment.SetEnvironmentVariable(Variable, "true");

        await MigrarHasta();

        Assert.Equal(0, await CantidadDeTablas("Reservas"));
        Assert.Equal(0, await CantidadDeTablas("ReservaAsientos"));
        Assert.Equal(0, await CantidadDeTablas("VerificacionesEmail"));
        Assert.Equal(0, await Escalar("""SELECT count(*) FROM "Carritos" """));
        Assert.Equal(0, await Escalar("""SELECT count(*) FROM "CarritoAsientos" """));
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM "OutboxMensajes" WHERE "Tipo" = 'OtroEvento'"""));
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM "OutboxMensajes" """));
    }

    [Fact]
    public async Task SobreUnaBaseSinReservas_PasaSinNecesitarLaDecision()
    {
        await MigrarHasta(MigracionPrevia);

        await MigrarHasta();

        Assert.Equal(1, await CantidadDeTablas("Carritos"));
        Assert.Equal(1, await CantidadDeTablas("CarritoAsientos"));
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM information_schema.columns WHERE table_name = 'Carritos' AND column_name = 'EmailEntrega' AND is_nullable = 'YES'"""));
    }
}
