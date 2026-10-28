using System.Net;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>FR-014: el mapa público excluye los asientos de los carritos activos y los devuelve al vencer o abandonarse.</summary>
public class MapaConCarritosTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public MapaConCarritosTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CarritoActivo_ExcluyeSusAsientosDelMapa()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));

        await _factory.CrearCarritoOk(funcionId, "C5", "C6");

        var disponibles = await _factory.Disponibles(funcionId);
        Assert.DoesNotContain("C5", disponibles);
        Assert.DoesNotContain("C6", disponibles);
        Assert.Contains("C7", disponibles);
        Assert.Equal(94, disponibles.Count);
    }

    [Fact]
    public async Task AlVencer_LosAsientosVuelvenAlMapaYElCarritoFiguraExpirado()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");

        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        Assert.Contains("C5", await _factory.Disponibles(funcionId));
        var response = await _factory.ClienteDe(creado).ObtenerCarrito(creado.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Expirado", (await response.LeerJson()).GetProperty("estado").GetString());
    }
}
