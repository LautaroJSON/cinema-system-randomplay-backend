using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Catalogo;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CinemaSystemRandomPlay.IntegrationTests.Catalogo;

public class PeliculaRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private CinemaDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        _dbContext = new CinemaDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task ListarActivas_DevuelveSoloPeliculasActivas_OrdenadasPorTitulo()
    {
        var activa1 = new Pelicula(Guid.NewGuid(), "Zodiac", 90, Clasificacion.Mas16, "Sinopsis");
        var activa2 = new Pelicula(Guid.NewGuid(), "Amelie", 100, Clasificacion.ATP, "Sinopsis");
        var inactiva = new Pelicula(Guid.NewGuid(), "Vieja", 80, Clasificacion.ATP, "Sinopsis", activa: false);

        _dbContext.Peliculas.AddRange(activa1, activa2, inactiva);
        await _dbContext.SaveChangesAsync();

        var repository = new PeliculaRepository(_dbContext);

        var resultado = await repository.ListarActivas();

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Amelie", resultado[0].Titulo);
        Assert.Equal("Zodiac", resultado[1].Titulo);
        Assert.DoesNotContain(resultado, p => p.Id == inactiva.Id);
    }

    [Fact]
    public async Task ObtenerPorId_ConIdExistente_DevuelveLaPelicula()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Amelie", 100, Clasificacion.ATP, "Sinopsis");
        _dbContext.Peliculas.Add(pelicula);
        await _dbContext.SaveChangesAsync();

        var repository = new PeliculaRepository(_dbContext);

        var resultado = await repository.ObtenerPorId(pelicula.Id);

        Assert.NotNull(resultado);
        Assert.Equal("Amelie", resultado!.Titulo);
    }

    [Fact]
    public async Task ObtenerPorId_ConIdInexistente_DevuelveNull()
    {
        var repository = new PeliculaRepository(_dbContext);

        var resultado = await repository.ObtenerPorId(Guid.NewGuid());

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ListarActivas_ConTitulo_FiltraPorCoincidenciaParcialCaseInsensitive()
    {
        var amelie = new Pelicula(Guid.NewGuid(), "Amelie", 100, Clasificacion.ATP, "Sinopsis");
        var amadeus = new Pelicula(Guid.NewGuid(), "Amadeus", 160, Clasificacion.Mas13, "Sinopsis");
        var zodiac = new Pelicula(Guid.NewGuid(), "Zodiac", 90, Clasificacion.Mas16, "Sinopsis");

        _dbContext.Peliculas.AddRange(amelie, amadeus, zodiac);
        await _dbContext.SaveChangesAsync();

        var repository = new PeliculaRepository(_dbContext);

        var resultado = await repository.ListarActivas(titulo: "AM");

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, p => p.Id == amelie.Id);
        Assert.Contains(resultado, p => p.Id == amadeus.Id);
        Assert.DoesNotContain(resultado, p => p.Id == zodiac.Id);
    }

    [Fact]
    public async Task ListarActivas_ConTituloSinCoincidencias_DevuelveListaVacia()
    {
        var amelie = new Pelicula(Guid.NewGuid(), "Amelie", 100, Clasificacion.ATP, "Sinopsis");
        _dbContext.Peliculas.Add(amelie);
        await _dbContext.SaveChangesAsync();

        var repository = new PeliculaRepository(_dbContext);

        var resultado = await repository.ListarActivas(titulo: "no-existe");

        Assert.Empty(resultado);
    }
}
