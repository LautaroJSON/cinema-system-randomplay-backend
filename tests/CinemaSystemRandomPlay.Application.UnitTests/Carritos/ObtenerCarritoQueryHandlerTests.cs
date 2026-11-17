using CinemaSystemRandomPlay.Application.Carritos.Queries;
using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Commands;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Pagos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

public class ObtenerCarritoQueryHandlerTests : OperacionesSobreCarritoTestBase
{
    private readonly IProcesadorDePagos _procesador = Substitute.For<IProcesadorDePagos>();
    private readonly IRegistroDePagos _registro = Substitute.For<IRegistroDePagos>();
    private readonly IGeneradorDeCodigos _generador = Substitute.For<IGeneradorDeCodigos>();

    public ObtenerCarritoQueryHandlerTests()
    {
        _generador.Generar().Returns(_ => Guid.NewGuid().ToString("N")[..10]);
        _procesador.BuscarPagosDelCarrito(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private ObtenerCarritoQueryHandler CrearHandler(TimeSpan? timeout = null) =>
        new(ConsultaCarritos, Reloj, _procesador, _registro,
            new ProcesarPagoCommandHandler(_procesador, UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, _registro,
                new ConfirmadorDePagos(), _generador, Outbox, Reloj),
            new OpcionesVerificacionDePagos(timeout ?? TimeSpan.FromSeconds(3)));

    private static PagoConsultado Aprobado(Carrito carrito, string id = "p1") =>
        new(id, EstadoPagoExterno.Aprobado, carrito.Total, carrito.Id.ToString(), Ahora.AddMinutes(1));

    [Fact]
    public async Task Handle_TokenDeOtroCarrito_DevuelveNullSinConsultar()
    {
        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Null(resultado);
        await ConsultaCarritos.DidNotReceive().ObtenerPorId(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _procesador.DidNotReceive().BuscarPagosDelCarrito(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
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
        var carrito = Existente(Ahora.AddMinutes(-6), new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        Assert.Equal(EstadoCarrito.Expirado, resultado!.Estado);
        Assert.Equal(carrito.VenceEn, resultado.VenceEn);
    }

    [Fact]
    public async Task Handle_CarritoActivo_NoPreguntaALaPasarela()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));

        await CrearHandler().Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        await _procesador.DidNotReceive().BuscarPagosDelCarrito(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnPagoSinPagos_DevuelvePagandoConLaUrl()
    {
        var carrito = EnPago(Existente(Ahora, new Asiento('A', 1)), Funcion, Ahora);

        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        Assert.Equal(EstadoCarrito.Pagando, resultado!.Estado);
        Assert.Equal("https://pago/pref-1", resultado.Pago!.UrlPago);
        Assert.Empty(resultado.Entradas);
        await _procesador.Received(1).BuscarPagosDelCarrito(carrito.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnPagoConUnPagoAprobadoSinAviso_LoProcesaYDevuelvePagado()
    {
        var carrito = EnPago(Existente(Ahora, new Asiento('A', 1)), Funcion, Ahora);
        _procesador.BuscarPagosDelCarrito(carrito.Id, Arg.Any<CancellationToken>()).Returns([Aprobado(carrito)]);
        Reloj.Ahora.Returns(Ahora.AddMinutes(2));

        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        Assert.Equal(EstadoCarrito.Pagado, resultado!.Estado);
        Assert.Single(resultado.Entradas);
        await _procesador.DidNotReceive().ObtenerPago(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnPagoVencidoConPagoAprobadoATiempo_LoAcredita()
    {
        var carrito = EnPago(Existente(Ahora, new Asiento('A', 1)), Funcion, Ahora);
        _procesador.BuscarPagosDelCarrito(carrito.Id, Arg.Any<CancellationToken>()).Returns([Aprobado(carrito)]);
        Reloj.Ahora.Returns(carrito.VenceEn.AddSeconds(30));

        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        Assert.Equal(EstadoCarrito.Pagado, resultado!.Estado);
    }

    [Fact]
    public async Task Handle_PagosYaProcesadosONoAprobados_NoSeProcesan()
    {
        var carrito = EnPago(Existente(Ahora, new Asiento('A', 1)), Funcion, Ahora);
        _procesador.BuscarPagosDelCarrito(carrito.Id, Arg.Any<CancellationToken>()).Returns([
            Aprobado(carrito, "ya-procesado"),
            new PagoConsultado("rechazado", EstadoPagoExterno.Rechazado, carrito.Total, carrito.Id.ToString(), null)
        ]);
        _registro.YaProcesado("ya-procesado", Arg.Any<CancellationToken>()).Returns(true);

        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        Assert.Equal(EstadoCarrito.Pagando, resultado!.Estado);
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
    }

    [Fact]
    public async Task Handle_PasarelaNoDisponible_DevuelveElEstadoConocido()
    {
        var carrito = EnPago(Existente(Ahora, new Asiento('A', 1)), Funcion, Ahora);
        _procesador.BuscarPagosDelCarrito(carrito.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProcesadorDePagosNoDisponibleException("caída"));

        var resultado = await CrearHandler().Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        Assert.Equal(EstadoCarrito.Pagando, resultado!.Estado);
    }

    [Fact]
    public async Task Handle_PasarelaLenta_RespondeElEstadoConocidoAlVencerElTimeout()
    {
        var carrito = EnPago(Existente(Ahora, new Asiento('A', 1)), Funcion, Ahora);
        _procesador.BuscarPagosDelCarrito(carrito.Id, Arg.Any<CancellationToken>()).Returns(async llamada =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), llamada.Arg<CancellationToken>());
            return (IReadOnlyList<PagoConsultado>)[];
        });

        var resultado = await CrearHandler(TimeSpan.FromMilliseconds(50)).Handle(new ObtenerCarritoQuery(carrito.Id, carrito.Id));

        Assert.Equal(EstadoCarrito.Pagando, resultado!.Estado);
    }
}
