using CinemaSystemRandomPlay.Application.Carritos;
using CinemaSystemRandomPlay.Application.Carritos.Commands;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

public class ReemplazarSeleccionCommandHandlerTests : OperacionesSobreCarritoTestBase
{
    private ReemplazarSeleccionCommandHandler CrearHandler() =>
        new(UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, Outbox, Reloj, new AsignadorDeAsientos(), new ReglasCarrito());

    private static AsientoPedidoDto[] Pedido(params (string Fila, int Numero)[] asientos) =>
        asientos.Select(a => new AsientoPedidoDto(a.Fila, a.Numero)).ToArray();

    [Fact]
    public async Task Handle_TokenDeOtroCarrito_DevuelveNoEncontradoSinTocarLaBase()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new ReemplazarSeleccionCommand(carrito.Id, Guid.NewGuid(), Pedido(("A", 2))));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoEncontrado, resultado.Estado);
        await AssertNoTocaLaBase();
        Assert.Equal([new Asiento('A', 1)], carrito.Asientos);
    }

    [Fact]
    public async Task Handle_CarritoInexistente_DevuelveNoEncontrado()
    {
        var id = Guid.NewGuid();

        var resultado = await CrearHandler().Handle(new ReemplazarSeleccionCommand(id, id, Pedido(("A", 2))));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoEncontrado, resultado.Estado);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_SeleccionVacia_DevuelvePedidoInvalidoSinAbrirTransaccion()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new ReemplazarSeleccionCommand(carrito.Id, carrito.Id, []));

        Assert.Equal(EstadoOperacionCarrito.PedidoInvalido, resultado.Estado);
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
    }

    [Fact]
    public async Task Handle_PedidoValido_ReemplazaRegistraElEventoYNoCambiaElVencimiento()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1), new Asiento('A', 2));
        var venceEn = carrito.VenceEn;
        Reloj.Ahora.Returns(Ahora.AddMinutes(3));

        var resultado = await CrearHandler().Handle(new ReemplazarSeleccionCommand(carrito.Id, carrito.Id, Pedido(("A", 2), ("A", 3))));

        Assert.Equal(EstadoOperacionCarrito.Ok, resultado.Estado);
        Assert.Equal([new AsientoPedidoDto("A", 2), new AsientoPedidoDto("A", 3)], resultado.Carrito!.Asientos);
        Assert.Equal(venceEn, resultado.Carrito.VenceEn);
        Outbox.Received(1).Registrar(Arg.Is<object>(e => e is SeleccionReemplazada
            && ((SeleccionReemplazada)e).AsientosLiberados.SequenceEqual(new[] { new Asiento('A', 1) })
            && ((SeleccionReemplazada)e).AsientosRetenidos.SequenceEqual(new[] { new Asiento('A', 3) })
            && ((SeleccionReemplazada)e).VenceEn == venceEn));
    }

    [Fact]
    public async Task Handle_TomaElBloqueoDeLaFuncionAntesDeCargarElCarrito()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));

        await CrearHandler().Handle(new ReemplazarSeleccionCommand(carrito.Id, carrito.Id, Pedido(("A", 2))));

        Received.InOrder(() =>
        {
            CarritoRepository.ObtenerFuncionId(carrito.Id, Arg.Any<CancellationToken>());
            FuncionRepository.ObtenerConSalaBloqueando(Funcion.Id, Arg.Any<CancellationToken>());
            CarritoRepository.ObtenerPorId(carrito.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_AsientoDeOtroCarrito_RechazaYConservaLaSeleccion()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));
        Existente(Ahora, new Asiento('B', 1));

        var resultado = await CrearHandler().Handle(new ReemplazarSeleccionCommand(carrito.Id, carrito.Id, Pedido(("B", 1))));

        Assert.Equal(EstadoOperacionCarrito.AsientosNoDisponibles, resultado.Estado);
        Assert.Equal([new AsientoPedidoDto("B", 1)], resultado.Asientos);
        Assert.Equal([new Asiento('A', 1)], carrito.Asientos);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_CarritoVencido_DevuelveCarritoNoActivo()
    {
        var carrito = Existente(Ahora.AddMinutes(-5), new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new ReemplazarSeleccionCommand(carrito.Id, carrito.Id, Pedido(("A", 2))));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoActivo, resultado.Estado);
        Assert.Equal(EstadoCarrito.Expirado, resultado.EstadoActual);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }
}
