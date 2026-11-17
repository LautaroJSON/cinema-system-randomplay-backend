using System.Net;
using System.Net.Http.Json;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using CinemaSystemRandomPlay.IntegrationTests.Pagos;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>
/// FR-020: con la autenticación y el fallback cerrado activos, las consultas existentes y la creación de carritos responden
/// sin credencial. Los endpoints de reservas y de verificación de email de las features 004 y 006 ya no existen.
/// </summary>
public class EndpointsPublicosTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public EndpointsPublicosTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConsultasExistentes_SinToken_SiguenSiendoPublicas()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromMinutes(30));
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            peliculaId = dbContext.Funciones.Single(f => f.Id == funcionId).PeliculaId;
        }

        var cliente = _factory.CrearCliente();
        var rutas = new[]
        {
            "/api/catalogo/peliculas",
            $"/api/catalogo/peliculas/{peliculaId}",
            $"/api/funciones/peliculas/{peliculaId}/sucursales-hoy",
            $"/api/funciones/peliculas/{peliculaId}/sucursales/{SucursalEntityConfiguration.SucursalCentroId}/horarios-hoy",
            $"/api/funciones/{funcionId}/asientos"
        };

        foreach (var ruta in rutas)
        {
            var response = await cliente.GetAsync(ruta);
            Assert.True(response.StatusCode is HttpStatusCode.OK, $"{ruta} respondió {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task CrearCarrito_SinToken_EsPublico()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));

        Assert.Equal(HttpStatusCode.Created, (await _factory.CrearCliente().CrearCarrito(funcionId, "A1")).StatusCode);
    }

    [Fact]
    public async Task RutaInexistente_SinToken_FallaCerradoCon401()
    {
        // El fallback cerrado aplica también a rutas sin endpoint: un anónimo no puede distinguir qué rutas existen.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CrearCliente().GetAsync("/api/reservas")).StatusCode);
    }

    [Fact]
    public async Task EndpointsRetirados_NoExisten()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        // Con un usuario autenticado el fallback no interviene y se ve que la ruta no existe.
        var cliente = _factory.CrearCliente(TokensDePrueba.Crear("sub-del-personal", ["Admin"]));
        var reservaId = Guid.NewGuid();

        var respuestas = new[]
        {
            await cliente.GetAsync("/api/reservas"),
            await cliente.GetAsync($"/api/reservas/{reservaId}"),
            await cliente.PostAsync($"/api/reservas/{reservaId}/cancelacion", null),
            await cliente.PostAsJsonAsync($"/api/funciones/{funcionId}/reservas", new { asientos = new[] { new { fila = "A", numero = 1 } } }),
            await cliente.PostAsJsonAsync("/api/verificaciones-email", new { email = "ana@example.com" }),
            await cliente.PostAsJsonAsync("/api/verificaciones-email/confirmacion", new { email = "ana@example.com", codigo = "000000" })
        };

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Fact]
    public async Task AvisoDeMercadoPago_SinToken_EsPublicoYLoProtegeLaFirma()
    {
        var cliente = _factory.CrearCliente();

        Assert.Equal(HttpStatusCode.OK, (await cliente.EnviarAviso("no-existe")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.EnviarAviso("no-existe", firma: string.Empty)).StatusCode);
    }

    [Fact]
    public async Task IniciarPago_SinToken_Responde401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CrearCliente().IniciarPago(Guid.NewGuid())).StatusCode);
    }
}

/// <summary>Con Mercado Pago real (credenciales falsas: nunca se llama), la simulación de pagos no existe.</summary>
public class ModoRealApiFactory : CarritosApiFactory
{
    protected override IReadOnlyDictionary<string, string?> ConfiguracionAdicional => new Dictionary<string, string?>
    {
        ["MercadoPago:Modo"] = "Real",
        ["MercadoPago:AccessToken"] = "TEST-token-falso",
        ["MercadoPago:UrlAvisos"] = "https://tunel.example/api/pagos/mercadopago/avisos"
    };
}

public class SimulacionDePagosTests : IClassFixture<ModoRealApiFactory>
{
    private readonly ModoRealApiFactory _factory;

    public SimulacionDePagosTests(ModoRealApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConMercadoPagoReal_ElEndpointDeSimulacionNoExiste()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var carrito = await _factory.CrearCarritoOk(funcionId, "A1");

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CrearCliente().SimularPago(carrito.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CrearCliente().GetAsync("/api/dev/pagos-simulados/sim-pref-x")).StatusCode);
    }
}
