using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CinemaSystemRandomPlay.IntegrationTests.Funciones;

public class FuncionRepositoryTests : IAsyncLifetime
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

    private static readonly DateTimeOffset Ahora = new(2026, 9, 21, 15, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora.Date);

    private async Task<Pelicula> CrearPelicula()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis");
        _dbContext.Peliculas.Add(pelicula);
        await _dbContext.SaveChangesAsync();
        return pelicula;
    }

    [Fact]
    public async Task ListarSucursalesConFuncionesDisponibles_DevuelveSoloSucursalesConFuncionHoyNoPasada()
    {
        var pelicula = await CrearPelicula();

        // Sucursal Centro: función hoy, todavía no pasó -> debe aparecer
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, Ahora.AddHours(2)));

        // Sucursal Norte: función hoy, pero ya pasó -> NO debe aparecer
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaNorte1Id, Ahora.AddHours(-1)));

        // Sucursal Sur: función programada para mañana, no hoy -> NO debe aparecer
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaSur1Id, Ahora.AddDays(1)));

        await _dbContext.SaveChangesAsync();

        var repository = new FuncionRepository(_dbContext);

        var resultado = await repository.ListarSucursalesConFuncionesDisponibles(pelicula.Id, Hoy, Ahora);

        var sucursal = Assert.Single(resultado);
        Assert.Equal(SucursalEntityConfiguration.SucursalCentroId, sucursal.Id);
    }

    [Fact]
    public async Task ListarSucursalesConFuncionesDisponibles_SinFuncionesHoy_DevuelveListaVacia()
    {
        var pelicula = await CrearPelicula();

        var repository = new FuncionRepository(_dbContext);

        var resultado = await repository.ListarSucursalesConFuncionesDisponibles(pelicula.Id, Hoy, Ahora);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSucursalesConFuncionesDisponibles_ConFuncionesEnDosSucursales_DevuelveOrdenadoPorNombre()
    {
        var pelicula = await CrearPelicula();

        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaNorte1Id, Ahora.AddHours(1)));
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, Ahora.AddHours(3)));
        await _dbContext.SaveChangesAsync();

        var repository = new FuncionRepository(_dbContext);

        var resultado = await repository.ListarSucursalesConFuncionesDisponibles(pelicula.Id, Hoy, Ahora);

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Sucursal Centro", resultado[0].Nombre);
        Assert.Equal("Sucursal Norte", resultado[1].Nombre);
    }

    [Fact]
    public async Task ExisteFuncionHoy_ConFuncionHoyEnEsaSucursal_DevuelveTrue()
    {
        var pelicula = await CrearPelicula();
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, Ahora.AddHours(-1)));
        await _dbContext.SaveChangesAsync();

        var repository = new FuncionRepository(_dbContext);

        var resultado = await repository.ExisteFuncionHoy(pelicula.Id, SucursalEntityConfiguration.SucursalCentroId, Hoy);

        Assert.True(resultado);
    }

    [Fact]
    public async Task ExisteFuncionHoy_SinNingunaFuncionHoyEnEsaSucursal_DevuelveFalse()
    {
        var pelicula = await CrearPelicula();
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaSur1Id, Ahora.AddDays(1)));
        await _dbContext.SaveChangesAsync();

        var repository = new FuncionRepository(_dbContext);

        var resultado = await repository.ExisteFuncionHoy(pelicula.Id, SucursalEntityConfiguration.SucursalCentroId, Hoy);

        Assert.False(resultado);
    }

    [Fact]
    public async Task ListarHorariosDisponibles_DevuelveSoloLosNoPasados_OrdenadosCronologicamenteConSala()
    {
        var pelicula = await CrearPelicula();

        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro2Id, Ahora.AddHours(5)));
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, Ahora.AddHours(1)));
        _dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, Ahora.AddHours(-1))); // ya pasó
        await _dbContext.SaveChangesAsync();

        var repository = new FuncionRepository(_dbContext);

        var resultado = await repository.ListarHorariosDisponibles(pelicula.Id, SucursalEntityConfiguration.SucursalCentroId, Hoy, Ahora);

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Sala 1", resultado[0].Sala!.Nombre);
        Assert.Equal("Sala 2", resultado[1].Sala!.Nombre);
        Assert.True(resultado[0].FechaHoraInicio < resultado[1].FechaHoraInicio);
    }

    [Fact]
    public async Task ObtenerConSala_FuncionExistente_CargaSalaConFilasYSucursal()
    {
        var pelicula = await CrearPelicula();
        var funcion = new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro2Id, Ahora.AddHours(2));
        _dbContext.Funciones.Add(funcion);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var repository = new FuncionRepository(_dbContext);

        var resultado = await repository.ObtenerConSala(funcion.Id);

        Assert.NotNull(resultado);
        var sala = Assert.IsType<Sala>(resultado!.Sala);
        Assert.Equal("Sala 2", sala.Nombre);
        Assert.Equal("Sucursal Centro", sala.Sucursal!.Nombre);
        Assert.Equal(['A', 'B', 'C', 'D', 'E', 'F'], sala.Filas.Select(f => f.Letra));
        Assert.Equal([8, 10, 12, 12, 14, 14], sala.Filas.Select(f => f.CantidadAsientos));
        Assert.Equal(70, sala.TotalAsientos);
    }

    [Fact]
    public async Task ObtenerConSala_FuncionInexistente_DevuelveNull()
    {
        var repository = new FuncionRepository(_dbContext);

        var resultado = await repository.ObtenerConSala(Guid.NewGuid());

        Assert.Null(resultado);
    }
}
