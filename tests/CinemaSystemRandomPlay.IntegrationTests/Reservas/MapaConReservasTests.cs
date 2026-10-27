using System.Net;
using System.Net.Http.Json;
using CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using static CinemaSystemRandomPlay.IntegrationTests.Reservas.ReservasTestHelpers;

namespace CinemaSystemRandomPlay.IntegrationTests.Reservas;

/// <summary>US2: el mapa público descuenta los asientos retenidos (FR-018 a FR-020).</summary>
public class MapaConReservasTests : IClassFixture<ReservasApiFactory>
{
    private static readonly Guid Sala = SalaEntityConfiguration.SalaSur1Id; // A-E, 8 asientos por fila = 40

    private readonly ReservasApiFactory _factory;

    public MapaConReservasTests(ReservasApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Ana() => _factory.CrearCliente(TokensDePrueba.Customer("ana"));

    private async Task<MapaAsientosResponse> ObtenerMapa(Guid funcionId)
    {
        var response = await _factory.CrearCliente().GetAsync($"/api/funciones/{funcionId}/asientos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MapaAsientosResponse>(JsonOptions))!;
    }

    private static IReadOnlyList<int> Fila(MapaAsientosResponse mapa, string fila) =>
        mapa.Filas.Single(f => f.Fila == fila).AsientosDisponibles;

    [Fact]
    public async Task Mapa_ConReservaVigente_NoListaLosAsientosRetenidosYDescuentaLaCantidad()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Ana().ReservarOk(funcionId, "B3", "B4");

        var mapa = await ObtenerMapa(funcionId);

        Assert.Equal(40, mapa.TotalAsientos);
        Assert.Equal(38, mapa.CantidadDisponibles);
        Assert.Equal([1, 2, 5, 6, 7, 8], Fila(mapa, "B"));
        Assert.Equal(8, Fila(mapa, "A").Count);
    }

    [Fact]
    public async Task Mapa_ConReservaVencida_VuelveAMostrarLosAsientos()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Ana().ReservarOk(funcionId, "B3", "B4");
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        var mapa = await ObtenerMapa(funcionId);

        Assert.Equal(40, mapa.CantidadDisponibles);
        Assert.Contains(3, Fila(mapa, "B"));
    }

    [Fact]
    public async Task Mapa_ConReservaCancelada_VuelveAMostrarLosAsientos()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var id = await Ana().ReservarOk(funcionId, "B3", "B4");
        await Ana().PostAsync($"/api/reservas/{id}/cancelacion", content: null);

        var mapa = await ObtenerMapa(funcionId);

        Assert.Equal(40, mapa.CantidadDisponibles);
        Assert.Equal(8, Fila(mapa, "B").Count);
    }

    [Fact]
    public async Task Mapa_DeOtraFuncionDeLaMismaSala_NoSeVeAfectado()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var otraFuncionId = await _factory.AgregarFuncion(Sala, TimeSpan.FromHours(5));
        await Ana().ReservarOk(funcionId, "B3");

        var mapaOtra = await ObtenerMapa(otraFuncionId);

        Assert.Equal(40, mapaOtra.CantidadDisponibles);
        Assert.Contains(3, Fila(mapaOtra, "B"));
    }

    [Fact]
    public async Task Mapa_SigueSiendoPublico_ConLaMismaForma()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await _factory.CrearCliente().GetAsync($"/api/funciones/{funcionId}/asientos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cuerpo = await response.LeerJson();
        Assert.Equal(
            ["funcionId", "horaInicio", "peliculaId", "peliculaTitulo", "sucursalId", "sucursalNombre", "salaId", "salaNombre", "totalAsientos", "cantidadDisponibles", "filas"],
            cuerpo.EnumerateObject().Select(p => p.Name));
    }
}
