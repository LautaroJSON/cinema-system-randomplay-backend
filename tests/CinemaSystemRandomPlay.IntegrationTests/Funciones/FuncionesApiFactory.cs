using CinemaSystemRandomPlay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;

namespace CinemaSystemRandomPlay.IntegrationTests.Funciones;

public class FuncionesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresDePrueba _postgres = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (no "Development") para que Program.cs no dispare el sembrado de datos de
        // desarrollo (DevDataSeeder) contra la base descartable de estos tests.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CinemaDb"] = _postgres.ConnectionString
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.Iniciar();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public async Task LimpiarPeliculasYFunciones()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        dbContext.Funciones.RemoveRange(dbContext.Funciones);
        dbContext.Peliculas.RemoveRange(dbContext.Peliculas);
        await dbContext.SaveChangesAsync();
    }
}
