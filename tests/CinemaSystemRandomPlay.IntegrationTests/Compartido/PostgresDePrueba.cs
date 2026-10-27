using Npgsql;
using Testcontainers.PostgreSql;

namespace CinemaSystemRandomPlay.IntegrationTests.Compartido;

/// <summary>
/// Base Postgres descartable para un fixture. Por defecto levanta un contenedor con Testcontainers. Si existe la
/// variable de entorno <c>CINEMA_TEST_POSTGRES</c> (cadena de conexión a un Postgres ya corriendo), crea y descarta
/// una base propia en ese servidor: sirve para correr los tests donde no hay Docker.
/// </summary>
public sealed class PostgresDePrueba : IAsyncDisposable
{
    private const string VariableDeEntorno = "CINEMA_TEST_POSTGRES";

    private PostgreSqlContainer? _contenedor;
    private string? _servidor;
    private string? _nombreBase;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task Iniciar()
    {
        _servidor = Environment.GetEnvironmentVariable(VariableDeEntorno);

        if (string.IsNullOrWhiteSpace(_servidor))
        {
            _contenedor = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _contenedor.StartAsync();
            ConnectionString = _contenedor.GetConnectionString();
            return;
        }

        _nombreBase = $"cinema_test_{Guid.NewGuid():N}";

        await using var conexion = new NpgsqlConnection(_servidor);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand($"CREATE DATABASE \"{_nombreBase}\"", conexion);
        await comando.ExecuteNonQueryAsync();

        ConnectionString = new NpgsqlConnectionStringBuilder(_servidor) { Database = _nombreBase }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_contenedor is not null)
        {
            await _contenedor.DisposeAsync();
            return;
        }

        if (_nombreBase is null)
            return;

        NpgsqlConnection.ClearAllPools();
        await using var conexion = new NpgsqlConnection(_servidor);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_nombreBase}\" WITH (FORCE)", conexion);
        await comando.ExecuteNonQueryAsync();
    }
}
