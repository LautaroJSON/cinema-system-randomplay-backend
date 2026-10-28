using System.Net;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>
/// FR-019 / SC-005: sin un token de carrito válido, toda operación responde 401 igual exista o no el carrito; con el token de
/// otro carrito, 404 igual que un carrito inexistente.
/// </summary>
public class CarritosAccesoTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public CarritosAccesoTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    private static IEnumerable<Func<HttpClient, Guid, Task<HttpResponseMessage>>> Operaciones() =>
    [
        (c, id) => c.ObtenerCarrito(id),
        (c, id) => c.ReemplazarAsientos(id, "H1"),
        (c, id) => c.Abandonar(id),
        (c, id) => c.IndicarEmailEntrega(id, "ana@example.com")
    ];

    public static TheoryData<string> TokensInvalidos => new()
    {
        "sin-token",
        "vencido",
        "otra-clave",
        "otro-emisor",
        "otro-tipo",
        "sin-claim-carrito",
        "personal",
        "basura"
    };

    private static string? Token(string caso, Guid carritoId) => caso switch
    {
        "sin-token" => null,
        "vencido" => TokensDePrueba.Carrito(carritoId, vencido: true),
        "otra-clave" => TokensDePrueba.Carrito(carritoId, claveFirma: "otra-clave-de-firma-de-al-menos-32-bytes-0123456789"),
        "otro-emisor" => TokensDePrueba.Carrito(carritoId, emisor: "https://otro.emisor"),
        "otro-tipo" => TokensDePrueba.Carrito(carritoId, tipo: "invitado-verificado"),
        "sin-claim-carrito" => TokensDePrueba.Carrito(carritoId, conClaimCarrito: false),
        "personal" => TokensDePrueba.Crear("sub-del-personal", ["Admin"]),
        _ => "esto-no-es-un-jwt"
    };

    [Theory]
    [MemberData(nameof(TokensInvalidos))]
    public async Task SinTokenValido_Responde401ExistaONoElCarrito(string caso)
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var existente = await _factory.CrearCarritoOk(funcionId, "C5");
        var inexistente = Guid.NewGuid();

        foreach (var operacion in Operaciones())
        {
            foreach (var id in new[] { existente.Id, inexistente })
            {
                var response = await operacion(_factory.CrearCliente(Token(caso, id)), id);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }

        var carrito = Assert.Single(await _factory.CarritosGuardados());
        Assert.Equal(CinemaSystemRandomPlay.Domain.Carritos.EstadoCarrito.Activo, carrito.Estado);
        Assert.Null(carrito.EmailEntrega);
    }

    [Fact]
    public async Task ConElTokenDeOtroCarrito_Responde404IgualQueUnCarritoInexistente()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var deAna = await _factory.CrearCarritoOk(funcionId, "C5");
        var deBeto = await _factory.CrearCarritoOk(funcionId, "D5");
        var clienteDeAna = _factory.ClienteDe(deAna);

        foreach (var operacion in Operaciones())
        {
            var ajeno = await operacion(clienteDeAna, deBeto.Id);
            var inexistente = await operacion(clienteDeAna, Guid.NewGuid());

            var cuerpoAjeno = await ajeno.AssertProblema(404, "carrito-no-encontrado");
            var cuerpoInexistente = await inexistente.AssertProblema(404, "carrito-no-encontrado");
            Assert.Equal(cuerpoInexistente.ToString(), cuerpoAjeno.ToString());
        }

        var carritoDeBeto = (await _factory.CarritosGuardados()).Single(c => c.Id == deBeto.Id);
        Assert.Equal(CinemaSystemRandomPlay.Domain.Carritos.EstadoCarrito.Activo, carritoDeBeto.Estado);
        Assert.Equal([new CinemaSystemRandomPlay.Domain.Funciones.Asiento('D', 5)], carritoDeBeto.Asientos);
        Assert.Null(carritoDeBeto.EmailEntrega);
    }
}
