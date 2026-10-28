using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Carritos.Queries;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

public class ObtenerCarritoQueryHandlerTests
{
    private readonly IConsultaCarritos _consultaCarritos = Substitute.For<IConsultaCarritos>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();

    public ObtenerCarritoQueryHandlerTests()
    {
        _reloj.Ahora.Returns(Ahora);
    }

    private ObtenerCarritoQueryHandler CrearHandler() => new(_consultaCarritos, _reloj);

    [Fact]
    public async Task Handle_TokenDeOtroCarrito_DevuelveNullSinConsultar()
    {
        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Null(resultado);
        await _consultaCarritos.DidNotReceive().ObtenerPorId(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CarritoInexistente_DevuelveNull()
    {
        var id = Guid.NewGuid();

        Assert.Null(await CrearHandler().Handle(new ObtenerCarritoQuery(id, id)));
    }

    [Fact]
    public async Task Handle_CarritoVencido_DevuelveElEstadoEfectivoExpirado()
    {
        var (funcion, _) = CrearFuncion();
        var carrito = CarritoDe(funcion, Ahora.AddMinutes(-6), new Asiento('A', 1));
        _consultaCarritos.ObtenerPorId(carrito.Id, Arg.Any<CancellationToken>()).Returns(LecturaDe(carrito, funcion));

        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        Assert.Equal(EstadoCarrito.Expirado, resultado!.Estado);
        Assert.Equal(carrito.VenceEn, resultado.VenceEn);
    }
}
