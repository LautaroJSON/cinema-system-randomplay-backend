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

public class MapaAsientosControllerTests : IClassFixture<FuncionesApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FuncionesApiFactory _factory;

    public MapaAsientosControllerTests(FuncionesApiFactory factory)
    {
        _factory = factory;
    }

    // Offsets en minutos (no en horas) contra el reloj real, para no cruzar medianoche
    // (ver specs/002-horarios-sucursal-hoy/tasks.md, T027).
    private async Task<Guid> CrearFuncion(Guid salaId, TimeSpan desdeAhora, bool peliculaActiva = true)
    {
        await _factory.LimpiarPeliculasYFunciones();
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis", peliculaActiva);
        var funcion = new Funcion(Guid.NewGuid(), pelicula.Id, salaId, DateTimeOffset.Now.Add(desdeAhora));
        dbContext.Peliculas.Add(pelicula);
        dbContext.Funciones.Add(funcion);
        await dbContext.SaveChangesAsync();
        return funcion.Id;
    }

    [Fact]
    public async Task GetAsientos_FuncionValida_DevuelveLaDisposicionIrregularDeLaSala()
    {
        var funcionId = await CrearFuncion(SalaEntityConfiguration.SalaCentro2Id, TimeSpan.FromMinutes(10));

        var response = await _factory.CreateClient().GetAsync($"/api/funciones/{funcionId}/asientos");

        response.EnsureSuccessStatusCode();
        var mapa = await response.Content.ReadFromJsonAsync<MapaAsientosResponse>(JsonOptions);
        Assert.NotNull(mapa);
        Assert.Equal(70, mapa!.TotalAsientos);
        Assert.Equal(["A", "B", "C", "D", "E", "F"], mapa.Filas.Select(f => f.Fila));
        Assert.Equal([8, 10, 12, 12, 14, 14], mapa.Filas.Select(f => f.CantidadAsientos));
    }

    [Fact]
    public async Task GetAsientos_FuncionValida_DevuelveElContextoDeLaFuncion()
    {
        var funcionId = await CrearFuncion(SalaEntityConfiguration.SalaCentro2Id, TimeSpan.FromMinutes(10));

        var response = await _factory.CreateClient().GetAsync($"/api/funciones/{funcionId}/asientos");

        response.EnsureSuccessStatusCode();
        var mapa = await response.Content.ReadFromJsonAsync<MapaAsientosResponse>(JsonOptions);
        Assert.NotNull(mapa);
        Assert.Equal(funcionId, mapa!.FuncionId);
        Assert.Equal("Interstellar", mapa.PeliculaTitulo);
        Assert.Equal(SucursalEntityConfiguration.SucursalCentroId, mapa.SucursalId);
        Assert.Equal("Sucursal Centro", mapa.SucursalNombre);
        Assert.Equal(SalaEntityConfiguration.SalaCentro2Id, mapa.SalaId);
        Assert.Equal("Sala 2", mapa.SalaNombre);
    }

    [Fact]
    public async Task GetAsientos_SinReservas_DevuelveTodosLosAsientosDisponibles()
    {
        var funcionId = await CrearFuncion(SalaEntityConfiguration.SalaCentro2Id, TimeSpan.FromMinutes(10));

        var response = await _factory.CreateClient().GetAsync($"/api/funciones/{funcionId}/asientos");

        response.EnsureSuccessStatusCode();
        var mapa = await response.Content.ReadFromJsonAsync<MapaAsientosResponse>(JsonOptions);
        Assert.NotNull(mapa);
        foreach (var fila in mapa!.Filas)
            Assert.Equal(Enumerable.Range(1, fila.CantidadAsientos), fila.AsientosDisponibles);
        Assert.Equal(mapa.TotalAsientos, mapa.CantidadDisponibles);
        Assert.Equal(mapa.CantidadDisponibles, mapa.Filas.Sum(f => f.AsientosDisponibles.Count));
    }

    [Fact]
    public async Task GetAsientos_FuncionInexistente_Devuelve404()
    {
        await _factory.LimpiarPeliculasYFunciones();

        var response = await _factory.CreateClient().GetAsync($"/api/funciones/{Guid.NewGuid()}/asientos");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAsientos_FuncionDePeliculaInactiva_Devuelve404()
    {
        var funcionId = await CrearFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromMinutes(10), peliculaActiva: false);

        var response = await _factory.CreateClient().GetAsync($"/api/funciones/{funcionId}/asientos");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAsientos_FuncionYaComenzada_Devuelve410()
    {
        var funcionId = await CrearFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromMinutes(-10));

        var response = await _factory.CreateClient().GetAsync($"/api/funciones/{funcionId}/asientos");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }
}
