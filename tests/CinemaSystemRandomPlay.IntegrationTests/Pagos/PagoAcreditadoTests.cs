using System.Net;
using System.Text.RegularExpressions;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Carritos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

/// <summary>Pago aprobado de punta a punta con la pasarela simulada (spec 008, US1: FR-010 a FR-012, FR-015, FR-022 a FR-024).</summary>
public class PagoAcreditadoTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public PagoAcreditadoTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PagoAprobadoPorElTotal_DejaElCarritoPagadoConUnaEntradaPorAsiento()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3), precio: 4500m);
        var (carrito, _) = await _factory.CrearCarritoEnPago(funcionId, "C5", "C6");

        var simulado = await (await _factory.CrearCliente().SimularPago(carrito.Id)).LeerJson();

        Assert.Equal("Pagado", simulado.GetProperty("estadoCarrito").GetString());
        var pagado = await _factory.CarritoSegunPortador(carrito);
        Assert.Equal("Pagado", pagado.GetProperty("estado").GetString());
        Assert.NotEqual(JsonValueKindNull, pagado.GetProperty("pago").GetProperty("pagadoEn").ValueKind);
        var entradas = pagado.GetProperty("entradas").EnumerateArray().ToList();
        Assert.Equal(2, entradas.Count);
        Assert.Equal(["C5", "C6"], entradas.Select(e => $"{e.GetProperty("asiento").GetProperty("fila").GetString()}{e.GetProperty("asiento").GetProperty("numero").GetInt32()}"));
        Assert.All(entradas, e =>
        {
            Assert.Matches(new Regex("^[0-9A-Z]{5}-[0-9A-Z]{5}$"), e.GetProperty("codigo").GetString()!);
            Assert.Equal("Interstellar", e.GetProperty("pelicula").GetString());
            Assert.False(string.IsNullOrEmpty(e.GetProperty("sucursal").GetString()));
            Assert.False(string.IsNullOrEmpty(e.GetProperty("sala").GetString()));
            Assert.Equal("4500.00", e.GetProperty("precioUnitario").GetProperty("monto").GetString());
        });
        Assert.NotEqual(entradas[0].GetProperty("codigo").GetString(), entradas[1].GetProperty("codigo").GetString());
        Assert.Contains("CarritoPagado", await _factory.TiposDeOutbox());

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<CinemaDbContext>().PagosProcesados.CountAsync());
    }

    [Fact]
    public async Task CarritoPagado_RetieneSusAsientosParaSiempre()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var (carrito, _) = await _factory.CrearCarritoEnPago(funcionId, "C5", "C6");
        await _factory.CrearCliente().SimularPago(carrito.Id);

        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(30));

        var disponibles = await _factory.Disponibles(funcionId);
        Assert.DoesNotContain("C5", disponibles);
        Assert.DoesNotContain("C6", disponibles);
        Assert.Equal("Pagado", (await _factory.CarritoSegunPortador(carrito)).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await _factory.CrearCliente().CrearCarrito(funcionId, "C5")).StatusCode);
    }

    [Fact]
    public async Task PagoRechazado_DejaElCarritoEnPago()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var (carrito, _) = await _factory.CrearCarritoEnPago(funcionId, "C5");

        var simulado = await (await _factory.CrearCliente().SimularPago(carrito.Id, "Rechazado")).LeerJson();

        Assert.Equal("Pagando", simulado.GetProperty("estadoCarrito").GetString());
        Assert.Empty((await _factory.CarritoSegunPortador(carrito)).GetProperty("entradas").EnumerateArray());
    }

    [Fact]
    public async Task CarritoEnPagoVigente_RetieneYAlVencerSinPagoLibera()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var (carrito, _) = await _factory.CrearCarritoEnPago(funcionId, "C5");

        Assert.DoesNotContain("C5", await _factory.Disponibles(funcionId));
        Assert.Equal(HttpStatusCode.Conflict, (await _factory.CrearCliente().CrearCarrito(funcionId, "C5")).StatusCode);

        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(1)));

        Assert.Contains("C5", await _factory.Disponibles(funcionId));
        Assert.Equal("Expirado", (await _factory.CarritoSegunPortador(carrito)).GetProperty("estado").GetString());
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;
}
