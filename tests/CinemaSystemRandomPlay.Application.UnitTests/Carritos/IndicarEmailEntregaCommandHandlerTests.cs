using CinemaSystemRandomPlay.Application.Carritos;
using CinemaSystemRandomPlay.Application.Carritos.Commands;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

public class IndicarEmailEntregaCommandHandlerTests : OperacionesSobreCarritoTestBase
{
    private IndicarEmailEntregaCommandHandler CrearHandler() =>
        new(UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, Reloj);

    [Fact]
    public async Task Handle_TokenDeOtroCarrito_DevuelveNoEncontradoSinTocarLaBase()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new IndicarEmailEntregaCommand(carrito.Id, Guid.NewGuid(), "ana@example.com"));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoEncontrado, resultado.Estado);
        await AssertNoTocaLaBase();
        Assert.Null(carrito.EmailEntrega);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-email")]
    public async Task Handle_EmailInvalido_DevuelvePedidoInvalidoSinAbrirTransaccion(string? email)
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new IndicarEmailEntregaCommand(carrito.Id, carrito.Id, email));

        Assert.Equal(EstadoOperacionCarrito.PedidoInvalido, resultado.Estado);
        Assert.True(resultado.Errores!.ContainsKey("email"));
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
    }

    [Fact]
    public async Task Handle_CarritoActivo_GuardaElEmailNormalizadoSinEventoNiCambioDeVencimiento()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));
        var venceEn = carrito.VenceEn;

        var resultado = await CrearHandler().Handle(new IndicarEmailEntregaCommand(carrito.Id, carrito.Id, " Ana@Example.com "));

        Assert.Equal(EstadoOperacionCarrito.Ok, resultado.Estado);
        Assert.Equal("ana@example.com", resultado.Carrito!.EmailEntrega);
        Assert.Equal(venceEn, resultado.Carrito.VenceEn);
        Assert.Equal(EstadoCarrito.Activo, resultado.Carrito.Estado);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_CarritoVencido_DevuelveCarritoNoActivo()
    {
        var carrito = Existente(Ahora.AddMinutes(-5), new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new IndicarEmailEntregaCommand(carrito.Id, carrito.Id, "ana@example.com"));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoActivo, resultado.Estado);
        Assert.Equal(EstadoCarrito.Expirado, resultado.EstadoActual);
        Assert.Null(carrito.EmailEntrega);
    }
}
