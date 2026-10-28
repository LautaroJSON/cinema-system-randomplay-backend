using CinemaSystemRandomPlay.Infrastructure.Migrations;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>
/// FR-021: la migración que pasa la reserva de <c>ClienteId</c> a <c>CompradorEmail</c> descarta las reservas
/// existentes solo si alguien lo decidió de forma explícita; sin esa decisión se niega a tocar datos.
/// </summary>
[Collection(VariableDeDescarteCollection.Nombre)]
public class MigracionCompradorEmailTests : IAsyncLifetime
{
    private const string MigracionPrevia = "20261027111431_AddPrecios";

    // Se migra hasta esta migración y no hasta la última: la de la feature 007 elimina la tabla Reservas.
    private const string MigracionProbada = "20261027230102_AddCompradorEmailEnReservas";
    private const string Variable = AddCompradorEmailEnReservas.VariablePermiteDescarte;

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

    private CinemaDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<CinemaDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    private async Task MigrarHasta(string? migracion = null)
    {
        await using var dbContext = CrearContexto();
        await dbContext.GetService<IMigrator>().MigrateAsync(migracion);
    }

    private async Task<long> Escalar(string sql)
    {
        await using var conexion = new NpgsqlConnection(_postgres.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return Convert.ToInt64(await comando.ExecuteScalarAsync());
    }

    private async Task SembrarReservaConAsientoYOutbox()
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
            INSERT INTO "Reservas" ("Id", "FuncionId", "ClienteId", "Estado", "CreadaEn", "VenceEn", "PrecioUnitario_Monto", "PrecioUnitario_Moneda")
            VALUES ('cccccccc-0000-0000-0000-000000000001', 'bbbbbbbb-0000-0000-0000-000000000001', 'sub-de-un-usuario', 'PendienteDePago', now(), now() + interval '10 minutes', 5500.00, 'ARS');
            INSERT INTO "ReservaAsientos" ("ReservaId", "Fila", "Numero")
            VALUES ('cccccccc-0000-0000-0000-000000000001', 'A', 1);
            INSERT INTO "OutboxMensajes" ("Id", "Tipo", "Payload", "OcurridoEn")
            VALUES ('dddddddd-0000-0000-0000-000000000001', 'ReservaCreada', {json}, now()),
                   ('dddddddd-0000-0000-0000-000000000002', 'OtroEvento', {json}, now());
            """, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task SinDecisionExplicitaYConReservas_LaMigracionFallaYNoBorraNada()
    {
        await MigrarHasta(MigracionPrevia);
        await SembrarReservaConAsientoYOutbox();

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => MigrarHasta(MigracionProbada));

        Assert.Contains(Variable, ex.ToString());
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM "Reservas" """));
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM "ReservaAsientos" """));
        Assert.Equal(2, await Escalar("""SELECT count(*) FROM "OutboxMensajes" """));
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM information_schema.columns WHERE table_name = 'Reservas' AND column_name = 'ClienteId'"""));
    }

    [Fact]
    public async Task ConDecisionExplicita_DescartaLasReservasYSusMensajesYRenombraLaColumna()
    {
        await MigrarHasta(MigracionPrevia);
        await SembrarReservaConAsientoYOutbox();
        Environment.SetEnvironmentVariable(Variable, "true");

        await MigrarHasta(MigracionProbada);

        Assert.Equal(0, await Escalar("""SELECT count(*) FROM "Reservas" """));
        Assert.Equal(0, await Escalar("""SELECT count(*) FROM "ReservaAsientos" """));
        Assert.Equal(["OtroEvento"], await TiposDeOutbox());
        Assert.Equal(1, await Escalar("""SELECT count(*) FROM information_schema.columns WHERE table_name = 'Reservas' AND column_name = 'CompradorEmail'"""));
        Assert.Equal(0, await Escalar("""SELECT count(*) FROM information_schema.columns WHERE table_name = 'Reservas' AND column_name = 'ClienteId'"""));
    }

    [Fact]
    public async Task SobreUnaBaseSinReservas_PasaSinNecesitarLaDecision()
    {
        await MigrarHasta(MigracionPrevia);

        await MigrarHasta(MigracionProbada);

        Assert.Equal(1, await Escalar("""SELECT count(*) FROM information_schema.columns WHERE table_name = 'Reservas' AND column_name = 'CompradorEmail'"""));
    }

    private async Task<List<string>> TiposDeOutbox()
    {
        await using var conexion = new NpgsqlConnection(_postgres.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand("""SELECT "Tipo" FROM "OutboxMensajes" ORDER BY "Tipo" """, conexion);
        await using var lector = await comando.ExecuteReaderAsync();
        var tipos = new List<string>();
        while (await lector.ReadAsync())
            tipos.Add(lector.GetString(0));
        return tipos;
    }
}
