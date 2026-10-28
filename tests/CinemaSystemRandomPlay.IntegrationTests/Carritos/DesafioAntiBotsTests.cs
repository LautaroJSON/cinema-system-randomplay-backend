using System.Net;
using System.Net.Http.Json;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>Desafío que solo aprueba la respuesta "humano".</summary>
public sealed class DesafioQueExigeHumano : IDesafioAntiBots
{
    public Task<bool> Verificar(string? respuesta, string? origen, CancellationToken cancellationToken = default) =>
        Task.FromResult(respuesta == "humano");
}

public class DesafioAntiBotsApiFactory : CarritosApiFactory
{
    protected override void ConfigurarServiciosAdicionales(IServiceCollection services)
    {
        services.RemoveAll<IDesafioAntiBots>();
        services.AddSingleton<IDesafioAntiBots, DesafioQueExigeHumano>();
    }
}

/// <summary>US4 / FR-023: la creación pasa por el punto de extensión anti-bots; si rechaza, no se crea nada.</summary>
public class DesafioAntiBotsTests : IClassFixture<DesafioAntiBotsApiFactory>
{
    private readonly DesafioAntiBotsApiFactory _factory;

    public DesafioAntiBotsTests(DesafioAntiBotsApiFactory factory)
    {
        _factory = factory;
    }

    private static object Pedido(string? desafio) => new { asientos = new[] { new { fila = "A", numero = 1 } }, desafio };

    [Fact]
    public async Task Crear_ConElDesafioRechazado_Devuelve403SinCrearNada()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var cliente = _factory.CrearCliente();

        await (await cliente.PostAsJsonAsync($"/api/funciones/{funcionId}/carritos", Pedido(null))).AssertProblema(403, "desafio-rechazado");
        await (await cliente.PostAsJsonAsync($"/api/funciones/{funcionId}/carritos", Pedido("bot"))).AssertProblema(403, "desafio-rechazado");

        Assert.Empty(await _factory.CarritosGuardados());
        Assert.Empty(await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Crear_ConElDesafioAprobado_CreaElCarrito()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));

        var response = await _factory.CrearCliente().PostAsJsonAsync($"/api/funciones/{funcionId}/carritos", Pedido("humano"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

/// <summary>Con la implementación por defecto, el campo <c>desafio</c> se ignora.</summary>
public class DesafioPorDefectoTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public DesafioPorDefectoTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Crear_ConCualquierDesafio_CreaElCarrito()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));

        var response = await _factory.CrearCliente().PostAsJsonAsync($"/api/funciones/{funcionId}/carritos",
            new { asientos = new[] { new { fila = "A", numero = 1 } }, desafio = "cualquier-cosa" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
