using CinemaSystemRandomPlay.Application.Carritos;
using CinemaSystemRandomPlay.Application.Carritos.Commands;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

/// <summary>Con el carrito en <c>Pagando</c> no se cambian asientos ni email, ni se abandona (spec 008, FR-009).</summary>
public class CambiosDuranteElPagoTests : OperacionesSobreCarritoTestBase
{
    private Carrito EnPagoExistente() => EnPago(Existente(Ahora, new Asiento('A', 1)), Funcion, Ahora);

    [Fact]
    public async Task Reemplazar_EnPago_DevuelveCarritoEnPagoSinEvento()
    {
        var carrito = EnPagoExistente();
        var handler = new ReemplazarSeleccionCommandHandler(UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, Outbox,
            Reloj, new AsignadorDeAsientos(), new ReglasCarrito());

        var resultado = await handler.Handle(new ReemplazarSeleccionCommand(carrito.Id, carrito.Id, [new AsientoPedidoDto("B", 1)]));

        Assert.Equal(EstadoOperacionCarrito.CarritoEnPago, resultado.Estado);
        Assert.Equal([new Asiento('A', 1)], carrito.Asientos);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Abandonar_EnPago_DevuelveCarritoEnPagoSinEvento()
    {
        var carrito = EnPagoExistente();
        var handler = new AbandonarCarritoCommandHandler(UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, Outbox, Reloj);

        var resultado = await handler.Handle(new AbandonarCarritoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.CarritoEnPago, resultado.Estado);
        Assert.Equal(EstadoCarrito.Pagando, carrito.Estado);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task IndicarEmail_EnPago_DevuelveCarritoEnPagoYConservaElEmail()
    {
        var carrito = EnPagoExistente();
        var handler = new IndicarEmailEntregaCommandHandler(UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, Reloj);

        var resultado = await handler.Handle(new IndicarEmailEntregaCommand(carrito.Id, carrito.Id, "beto@example.com"));

        Assert.Equal(EstadoOperacionCarrito.CarritoEnPago, resultado.Estado);
        Assert.Equal("ana@example.com", carrito.EmailEntrega!.Valor);
    }
}
