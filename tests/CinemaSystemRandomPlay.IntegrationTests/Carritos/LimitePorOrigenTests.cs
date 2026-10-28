using System.Net;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

public class LimitePorOrigenApiFactory : CarritosApiFactory
{
    protected override IReadOnlyDictionary<string, string?> ConfiguracionAdicional => new Dictionary<string, string?>
    {
        ["Carritos:CreacionesPorOrigen"] = "3"
    };
}

/// <summary>US4 / FR-021: los pedidos de creación de carritos se limitan por origen; las demás operaciones no.</summary>
public class LimitePorOrigenTests : IClassFixture<LimitePorOrigenApiFactory>
{
    private readonly LimitePorOrigenApiFactory _factory;

    public LimitePorOrigenTests(LimitePorOrigenApiFactory factory)
    {
        _factory = factory;
    }

    // Un solo test: el límite es por origen y todos los tests de la clase comparten el mismo origen y la misma ventana.
    [Fact]
    public async Task Crear_ElCuartoPedidoDesdeElMismoOrigen_Devuelve429ConRetryAfter_YLasDemasOperacionesSiguenFuncionando()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var cliente = _factory.CrearCliente();

        var primero = await cliente.CrearCarrito(funcionId, "A1");
        var rechazadoPorAsiento = await cliente.CrearCarrito(funcionId, "A1");
        var tercero = await cliente.CrearCarrito(funcionId, "A2");
        var cuarto = await cliente.CrearCarrito(funcionId, "A3");

        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, rechazadoPorAsiento.StatusCode);
        Assert.Equal(HttpStatusCode.Created, tercero.StatusCode);
        await cuarto.AssertProblema(429, "demasiados-pedidos");
        Assert.True(cuarto.Headers.Contains("Retry-After"));
        Assert.Equal(2, (await _factory.CarritosGuardados()).Count);

        var cuerpo = (await primero.LeerJson());
        var creado = new CarritoCreado(cuerpo.GetProperty("carrito").GetProperty("id").GetGuid(), cuerpo.GetProperty("token").GetString()!);
        var portador = _factory.ClienteDe(creado);
        Assert.Equal(HttpStatusCode.OK, (await portador.ObtenerCarrito(creado.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await portador.ReemplazarAsientos(creado.Id, "A4")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await portador.IndicarEmailEntrega(creado.Id, "ana@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await portador.Abandonar(creado.Id)).StatusCode);
    }
}
