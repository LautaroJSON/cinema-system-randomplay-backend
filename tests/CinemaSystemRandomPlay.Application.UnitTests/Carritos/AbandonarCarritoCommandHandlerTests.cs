using CinemaSystemRandomPlay.Application.Carritos;
using CinemaSystemRandomPlay.Application.Carritos.Commands;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

public class AbandonarCarritoCommandHandlerTests : OperacionesSobreCarritoTestBase
{
    private AbandonarCarritoCommandHandler CrearHandler() =>
        new(UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, Outbox, Reloj);

    [Fact]
    public async Task Handle_TokenDeOtroCarrito_DevuelveNoEncontradoSinTocarLaBase()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new AbandonarCarritoCommand(carrito.Id, Guid.NewGuid()));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoEncontrado, resultado.Estado);
        await AssertNoTocaLaBase();
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
    }

    [Fact]
    public async Task Handle_CarritoActivo_LoAbandonaYRegistraLosAsientosLiberados()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1), new Asiento('A', 2));

        var resultado = await CrearHandler().Handle(new AbandonarCarritoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.Ok, resultado.Estado);
        Assert.Equal(EstadoCarrito.Abandonado, resultado.Carrito!.Estado);
        Assert.Equal(Ahora, resultado.Carrito.AbandonadoEn);
        Outbox.Received(1).Registrar(Arg.Is<object>(e => e is CarritoAbandonado
            && ((CarritoAbandonado)e).AsientosLiberados.SequenceEqual(new[] { new Asiento('A', 1), new Asiento('A', 2) })));
    }

    [Fact]
    public async Task Handle_CarritoYaAbandonado_DevuelveCarritoNoActivoSinEvento()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));
        carrito.Abandonar(Ahora);

        var resultado = await CrearHandler().Handle(new AbandonarCarritoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoActivo, resultado.Estado);
        Assert.Equal(EstadoCarrito.Abandonado, resultado.EstadoActual);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }
}
