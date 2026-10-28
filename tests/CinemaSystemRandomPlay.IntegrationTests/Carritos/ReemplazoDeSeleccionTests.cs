using System.Net;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>US2: reemplazar la selección es atómico, todo o nada y no extiende el vencimiento (FR-006, FR-008 a FR-011).</summary>
public class ReemplazoDeSeleccionTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public ReemplazoDeSeleccionTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reemplazar_LiberaLosViejosRetieneLosNuevosYNoCambiaElVencimiento()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5", "C6");
        var cliente = _factory.ClienteDe(creado);
        var venceEn = (await (await cliente.ObtenerCarrito(creado.Id)).LeerJson()).GetProperty("venceEn").GetDateTimeOffset();
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(3));

        var response = await cliente.ReemplazarAsientos(creado.Id, "D2", "D1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var carrito = await response.LeerJson();
        Assert.Equal(["D1", "D2"], carrito.Asientos());
        Assert.Equal(venceEn, carrito.GetProperty("venceEn").GetDateTimeOffset());
        var disponibles = await _factory.Disponibles(funcionId);
        Assert.Contains("C5", disponibles);
        Assert.Contains("C6", disponibles);
        Assert.DoesNotContain("D1", disponibles);
        Assert.DoesNotContain("D2", disponibles);
        Assert.Equal(["CarritoCreado", "SeleccionReemplazada"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Reemplazar_ConAsientosComunes_LosMantieneRetenidos()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5", "C6");

        var response = await _factory.ClienteDe(creado).ReemplazarAsientos(creado.Id, "C6", "C7");

        Assert.Equal(["C6", "C7"], (await response.LeerJson()).Asientos());
        var disponibles = await _factory.Disponibles(funcionId);
        Assert.Contains("C5", disponibles);
        Assert.DoesNotContain("C6", disponibles);
        Assert.DoesNotContain("C7", disponibles);
    }

    [Fact]
    public async Task Reemplazar_ConUnAsientoDeOtroCarrito_Devuelve409YConservaLaSeleccionAnterior()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5", "C6");
        await _factory.CrearCarritoOk(funcionId, "D2");
        var cliente = _factory.ClienteDe(creado);

        var problema = await (await cliente.ReemplazarAsientos(creado.Id, "D1", "D2")).AssertProblema(409, "asientos-no-disponibles");

        Assert.Equal(["D2"], problema.Asientos());
        Assert.Equal(["C5", "C6"], (await (await cliente.ObtenerCarrito(creado.Id)).LeerJson()).Asientos());
        Assert.Contains("D1", await _factory.Disponibles(funcionId));
        Assert.Equal(["CarritoCreado", "CarritoCreado"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Reemplazar_PedidoInvalidoOAsientosInexistentes_NoCambiaElCarrito()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");
        var cliente = _factory.ClienteDe(creado);

        await (await cliente.ReemplazarAsientos(creado.Id)).AssertProblema(400, "pedido-invalido");
        await (await cliente.ReemplazarAsientos(creado.Id, "A1", "A1")).AssertProblema(400, "pedido-invalido");
        var problema = await (await cliente.ReemplazarAsientos(creado.Id, "Z1")).AssertProblema(422, "asientos-inexistentes");

        Assert.Equal(["Z1"], problema.Asientos());
        Assert.Equal(["C5"], (await (await cliente.ObtenerCarrito(creado.Id)).LeerJson()).Asientos());
    }

    [Fact]
    public async Task Reemplazar_CarritoExpirado_Devuelve409CarritoNoActivo()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(5));

        var problema = await (await _factory.ClienteDe(creado).ReemplazarAsientos(creado.Id, "C6")).AssertProblema(409, "carrito-no-activo");

        Assert.Equal("Expirado", problema.GetProperty("estadoActual").GetString());
        Assert.DoesNotContain("SeleccionReemplazada", await _factory.TiposDeOutbox());
    }
}
