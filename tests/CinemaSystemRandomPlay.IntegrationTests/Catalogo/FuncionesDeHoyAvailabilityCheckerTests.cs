using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.EntityFrameworkCore;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;

namespace CinemaSystemRandomPlay.IntegrationTests.Catalogo;

public class FuncionesDeHoyAvailabilityCheckerTests : IAsyncLifetime
{
    private readonly PostgresDePrueba _postgres = new();

    private CinemaDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _postgres.Iniciar();

        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        _dbContext = new CinemaDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static readonly DateTimeOffset Ahora = new(2026, 9, 21, 15, 0, 0, TimeSpan.FromHours(-3));

    private sealed class RelojFijo : IReloj
    {
        public DateTimeOffset Ahora => FuncionesDeHoyAvailabilityCheckerTests.Ahora;
    }

    private async Task<Pelicula> CrearPelicula()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis");
        _dbContext.Peliculas.Add(pelicula);
        await _dbContext.SaveChangesAsync();
        return pelicula;
    }

    private async Task<bool> TieneFuncionesProgramadas(Guid peliculaId)
        => await new FuncionesDeHoyAvailabilityChecker(_dbContext, new RelojFijo()).TieneFuncionesProgramadas(peliculaId);

    [Fact]
    public async Task ConFuncionHoyQueNoEmpezo_DevuelveTrue()
    {
        var pelicula = await CrearPelicula();
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, Ahora.AddHours(2), Dinero.Ars(5500m)));
        await _dbContext.SaveChangesAsync();

        Assert.True(await TieneFuncionesProgramadas(pelicula.Id));
    }

    [Fact]
    public async Task ConFuncionesSoloPasadasOManana_DevuelveFalse()
    {
        var pelicula = await CrearPelicula();
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, Ahora.AddHours(-1), Dinero.Ars(5500m)));
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaNorte1Id, Ahora.AddDays(1), Dinero.Ars(5500m)));
        await _dbContext.SaveChangesAsync();

        Assert.False(await TieneFuncionesProgramadas(pelicula.Id));
    }

    [Fact]
    public async Task ConFuncionHoyDeOtraPelicula_DevuelveFalse()
    {
        var pelicula = await CrearPelicula();
        var otra = await CrearPelicula();
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), otra.Id, SalaEntityConfiguration.SalaCentro1Id, Ahora.AddHours(2), Dinero.Ars(5500m)));
        await _dbContext.SaveChangesAsync();

        Assert.False(await TieneFuncionesProgramadas(pelicula.Id));
    }
}
