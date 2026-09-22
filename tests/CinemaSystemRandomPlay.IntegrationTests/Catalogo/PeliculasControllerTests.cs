using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystemRandomPlay.Api.Modulos.Catalogo.Contracts;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Catalogo;

public class PeliculasControllerTests : IClassFixture<CatalogoApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CatalogoApiFactory _factory;

    public PeliculasControllerTests(CatalogoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetListado_SinPeliculasActivas_DevuelveListaVacia()
    {
        await _factory.LimpiarPeliculas();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/catalogo/peliculas");

        response.EnsureSuccessStatusCode();
        var peliculas = await response.Content.ReadFromJsonAsync<List<PeliculaListItemResponse>>(JsonOptions);
        Assert.NotNull(peliculas);
        Assert.Empty(peliculas);
    }

    [Fact]
    public async Task GetListado_ConPeliculaActiva_LaDevuelveMarcadaSinFunciones()
    {
        await _factory.LimpiarPeliculas();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            dbContext.Peliculas.Add(new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis"));
            await dbContext.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/catalogo/peliculas");

        response.EnsureSuccessStatusCode();
        var peliculas = await response.Content.ReadFromJsonAsync<List<PeliculaListItemResponse>>(JsonOptions);
        Assert.NotNull(peliculas);
        var pelicula = Assert.Single(peliculas);
        Assert.Equal("Interstellar", pelicula.Titulo);
        Assert.True(pelicula.SinFuncionesDisponibles);
    }

    [Fact]
    public async Task GetDetalle_ConPeliculaActiva_DevuelveElDetalle()
    {
        await _factory.LimpiarPeliculas();
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var pelicula = new Pelicula(Guid.NewGuid(), "Amelie", 100, Clasificacion.ATP, "Una sinopsis.");
            dbContext.Peliculas.Add(pelicula);
            await dbContext.SaveChangesAsync();
            peliculaId = pelicula.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/catalogo/peliculas/{peliculaId}");

        response.EnsureSuccessStatusCode();
        var detalle = await response.Content.ReadFromJsonAsync<PeliculaDetalleResponse>(JsonOptions);
        Assert.NotNull(detalle);
        Assert.Equal("Amelie", detalle!.Titulo);
        Assert.Equal("Una sinopsis.", detalle.Sinopsis);
    }

    [Fact]
    public async Task GetDetalle_ConPeliculaInactiva_Devuelve404()
    {
        await _factory.LimpiarPeliculas();
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var pelicula = new Pelicula(Guid.NewGuid(), "Vieja", 80, Clasificacion.ATP, "Sinopsis.", activa: false);
            dbContext.Peliculas.Add(pelicula);
            await dbContext.SaveChangesAsync();
            peliculaId = pelicula.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/catalogo/peliculas/{peliculaId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDetalle_ConIdInexistente_Devuelve404()
    {
        await _factory.LimpiarPeliculas();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/catalogo/peliculas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetListado_ConTituloQueCoincide_DevuelveSoloLasCoincidencias()
    {
        await _factory.LimpiarPeliculas();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            dbContext.Peliculas.AddRange(
                new Pelicula(Guid.NewGuid(), "Amelie", 100, Clasificacion.ATP, "Sinopsis"),
                new Pelicula(Guid.NewGuid(), "Zodiac", 90, Clasificacion.Mas16, "Sinopsis"));
            await dbContext.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/catalogo/peliculas?titulo=amel");

        response.EnsureSuccessStatusCode();
        var peliculas = await response.Content.ReadFromJsonAsync<List<PeliculaListItemResponse>>(JsonOptions);
        Assert.NotNull(peliculas);
        var pelicula = Assert.Single(peliculas);
        Assert.Equal("Amelie", pelicula.Titulo);
    }

    [Fact]
    public async Task GetListado_ConTituloSinCoincidencias_DevuelveListaVacia()
    {
        await _factory.LimpiarPeliculas();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            dbContext.Peliculas.Add(new Pelicula(Guid.NewGuid(), "Amelie", 100, Clasificacion.ATP, "Sinopsis"));
            await dbContext.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/catalogo/peliculas?titulo=no-existe");

        response.EnsureSuccessStatusCode();
        var peliculas = await response.Content.ReadFromJsonAsync<List<PeliculaListItemResponse>>(JsonOptions);
        Assert.NotNull(peliculas);
        Assert.Empty(peliculas);
    }
}
