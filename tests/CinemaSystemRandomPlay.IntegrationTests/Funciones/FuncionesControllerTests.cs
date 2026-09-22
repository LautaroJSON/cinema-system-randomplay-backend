using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Funciones;

public class FuncionesControllerTests : IClassFixture<FuncionesApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FuncionesApiFactory _factory;

    public FuncionesControllerTests(FuncionesApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSucursalesHoy_ConFuncionHoyNoPasada_DevuelveLaSucursal()
    {
        await _factory.LimpiarPeliculasYFunciones();
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis");
            dbContext.Peliculas.Add(pelicula);
            dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, DateTimeOffset.Now.AddMinutes(5)));
            await dbContext.SaveChangesAsync();
            peliculaId = pelicula.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/funciones/peliculas/{peliculaId}/sucursales-hoy");

        response.EnsureSuccessStatusCode();
        var sucursales = await response.Content.ReadFromJsonAsync<List<SucursalConFuncionesHoyResponse>>(JsonOptions);
        Assert.NotNull(sucursales);
        var sucursal = Assert.Single(sucursales);
        Assert.Equal(SucursalEntityConfiguration.SucursalCentroId, sucursal.Id);
    }

    [Fact]
    public async Task GetSucursalesHoy_PeliculaSinFuncionesHoy_DevuelveListaVacia()
    {
        await _factory.LimpiarPeliculasYFunciones();
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var pelicula = new Pelicula(Guid.NewGuid(), "Amelie", 100, Clasificacion.ATP, "Sinopsis");
            dbContext.Peliculas.Add(pelicula);
            await dbContext.SaveChangesAsync();
            peliculaId = pelicula.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/funciones/peliculas/{peliculaId}/sucursales-hoy");

        response.EnsureSuccessStatusCode();
        var sucursales = await response.Content.ReadFromJsonAsync<List<SucursalConFuncionesHoyResponse>>(JsonOptions);
        Assert.NotNull(sucursales);
        Assert.Empty(sucursales);
    }

    [Fact]
    public async Task GetSucursalesHoy_PeliculaInexistente_Devuelve404()
    {
        await _factory.LimpiarPeliculasYFunciones();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/funciones/peliculas/{Guid.NewGuid()}/sucursales-hoy");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSucursalesHoy_PeliculaInactiva_Devuelve404()
    {
        await _factory.LimpiarPeliculasYFunciones();
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var pelicula = new Pelicula(Guid.NewGuid(), "Vieja", 80, Clasificacion.ATP, "Sinopsis", activa: false);
            dbContext.Peliculas.Add(pelicula);
            await dbContext.SaveChangesAsync();
            peliculaId = pelicula.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/funciones/peliculas/{peliculaId}/sucursales-hoy");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHorariosHoy_SucursalValidaConHorarios_DevuelveOrdenadosConSala()
    {
        await _factory.LimpiarPeliculasYFunciones();
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis");
            dbContext.Peliculas.Add(pelicula);
            dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro2Id, DateTimeOffset.Now.AddMinutes(6)));
            dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, DateTimeOffset.Now.AddMinutes(3)));
            await dbContext.SaveChangesAsync();
            peliculaId = pelicula.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/funciones/peliculas/{peliculaId}/sucursales/{SucursalEntityConfiguration.SucursalCentroId}/horarios-hoy");

        response.EnsureSuccessStatusCode();
        var horarios = await response.Content.ReadFromJsonAsync<List<HorarioFuncionResponse>>(JsonOptions);
        Assert.NotNull(horarios);
        Assert.Equal(2, horarios!.Count);
        Assert.Equal("Sala 1", horarios[0].SalaNombre);
        Assert.Equal("Sala 2", horarios[1].SalaNombre);
    }

    [Fact]
    public async Task GetHorariosHoy_SucursalValidaSinHorariosRestantes_DevuelveListaVacia()
    {
        await _factory.LimpiarPeliculasYFunciones();
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis");
            dbContext.Peliculas.Add(pelicula);
            dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, DateTimeOffset.Now.AddMinutes(-2)));
            await dbContext.SaveChangesAsync();
            peliculaId = pelicula.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/funciones/peliculas/{peliculaId}/sucursales/{SucursalEntityConfiguration.SucursalCentroId}/horarios-hoy");

        response.EnsureSuccessStatusCode();
        var horarios = await response.Content.ReadFromJsonAsync<List<HorarioFuncionResponse>>(JsonOptions);
        Assert.NotNull(horarios);
        Assert.Empty(horarios!);
    }

    [Fact]
    public async Task GetHorariosHoy_SucursalNoValidaParaLaPelicula_Devuelve404()
    {
        await _factory.LimpiarPeliculasYFunciones();
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis");
            dbContext.Peliculas.Add(pelicula);
            dbContext.Funciones.Add(new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, DateTimeOffset.Now.AddMinutes(3)));
            await dbContext.SaveChangesAsync();
            peliculaId = pelicula.Id;
        }

        var client = _factory.CreateClient();
        // SucursalSur no tiene ninguna función hoy para esta película
        var response = await client.GetAsync($"/api/funciones/peliculas/{peliculaId}/sucursales/{SucursalEntityConfiguration.SucursalSurId}/horarios-hoy");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHorariosHoy_PeliculaInexistente_Devuelve404()
    {
        await _factory.LimpiarPeliculasYFunciones();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/funciones/peliculas/{Guid.NewGuid()}/sucursales/{SucursalEntityConfiguration.SucursalCentroId}/horarios-hoy");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
