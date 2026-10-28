using System.Net;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>US4 / FR-022: los asientos retenidos de una función no superan el 50% de la sala, sumando todos los carritos.</summary>
public class TopePorFuncionTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public TopePorFuncionTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    private static string[] Fila(char fila, int cantidad) => Enumerable.Range(1, cantidad).Select(n => $"{fila}{n}").ToArray();

    /// <summary>Sala Sur 1: 40 asientos, tope 20. Deja 20 retenidos con dos carritos (A1-A8 + B1-B2 y C1-C8 + D1-D2).</summary>
    private async Task<(Guid FuncionId, CarritoCreado Primero, CarritoCreado Segundo)> FuncionEnElTope()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaSur1Id, TimeSpan.FromHours(3));
        var primero = await _factory.CrearCarritoOk(funcionId, [.. Fila('A', 8), "B1", "B2"]);
        var segundo = await _factory.CrearCarritoOk(funcionId, [.. Fila('C', 8), "D1", "D2"]);
        return (funcionId, primero, segundo);
    }

    [Fact]
    public async Task Crear_EnElTope_Devuelve409SinDatosDeOtrosCarritos()
    {
        var (funcionId, _, _) = await FuncionEnElTope();

        var problema = await (await _factory.CrearCliente().CrearCarrito(funcionId, "E1")).AssertProblema(409, "funcion-sin-cupo");

        // Solo los campos estándar y el código: ni asientos ni cantidades de otros carritos.
        Assert.Subset(new HashSet<string> { "type", "title", "status", "codigo", "traceId" }, problema.EnumerateObject().Select(p => p.Name).ToHashSet());
        Assert.Equal(20, (await _factory.AsientosRetenidos(funcionId)).Count);
    }

    [Fact]
    public async Task Crear_TrasAbandonarUnCarrito_SeAcepta()
    {
        var (funcionId, primero, _) = await FuncionEnElTope();

        await _factory.ClienteDe(primero).Abandonar(primero.Id);

        Assert.Equal(HttpStatusCode.Created, (await _factory.CrearCliente().CrearCarrito(funcionId, "E1", "E2", "E3")).StatusCode);
    }

    [Fact]
    public async Task Crear_TrasVencerLosCarritos_SeAcepta()
    {
        var (funcionId, _, _) = await FuncionEnElTope();

        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(5));

        Assert.Equal(HttpStatusCode.Created, (await _factory.CrearCliente().CrearCarrito(funcionId, "E1")).StatusCode);
    }

    [Fact]
    public async Task Reemplazar_EnElTope_SeAceptaPorqueDescuentaLosPropios()
    {
        var (funcionId, primero, _) = await FuncionEnElTope();

        var response = await _factory.ClienteDe(primero).ReemplazarAsientos(primero.Id, Fila('E', 8));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(18, (await _factory.AsientosRetenidos(funcionId)).Count);
    }

    [Fact]
    public async Task Reemplazar_QueSuperariaElTope_Devuelve409YConservaLaSeleccion()
    {
        var (_, primero, _) = await FuncionEnElTope();
        var cliente = _factory.ClienteDe(primero);
        await cliente.ReemplazarAsientos(primero.Id, "A1");

        // Otro carrito ocupa el lugar liberado: 1 + 10 + 9 = 20.
        await _factory.CrearCarritoOk((await _factory.CarritosGuardados()).First().FuncionId, Fila('E', 8).Append("D3").ToArray());

        await (await cliente.ReemplazarAsientos(primero.Id, "A1", "A2")).AssertProblema(409, "funcion-sin-cupo");
        Assert.Equal(["A1"], (await (await cliente.ObtenerCarrito(primero.Id)).LeerJson()).Asientos());
    }
}
