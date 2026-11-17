using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Commands;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Application.UnitTests.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Pagos;
using CinemaSystemRandomPlay.Domain.Pagos.Eventos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Pagos;

public class ProcesarPagoCommandHandlerTests : OperacionesSobreCarritoTestBase
{
    private readonly IProcesadorDePagos _procesador = Substitute.For<IProcesadorDePagos>();
    private readonly IRegistroDePagos _registro = Substitute.For<IRegistroDePagos>();
    private readonly IGeneradorDeCodigos _generador = Substitute.For<IGeneradorDeCodigos>();

    public ProcesarPagoCommandHandlerTests()
    {
        var siguiente = 0;
        _generador.Generar().Returns(_ => $"COD-{++siguiente}");
    }

    private ProcesarPagoCommandHandler CrearHandler() =>
        new(_procesador, UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, _registro, new ConfirmadorDePagos(),
            _generador, Outbox, Reloj);

    private Carrito CarritoEnPago(params Asiento[] asientos) => EnPago(Existente(Ahora, asientos), Funcion, Ahora);

    private void PagoEnLaPasarela(string pagoId, EstadoPagoExterno estado, Dinero monto, string? referencia, DateTimeOffset? aprobadoEn) =>
        _procesador.ObtenerPago(pagoId, Arg.Any<CancellationToken>())
            .Returns(new PagoConsultado(pagoId, estado, monto, referencia, aprobadoEn));

    [Fact]
    public async Task Handle_PagoInexistente_Ignora()
    {
        _procesador.ObtenerPago("p", Arg.Any<CancellationToken>()).Returns((PagoConsultado?)null);

        var resultado = await CrearHandler().Handle(new ProcesarPagoCommand("p"));

        Assert.Equal(ResultadoProcesamientoPago.Ignorado, resultado);
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
    }

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData(null)]
    public async Task Handle_ReferenciaQueNoEsUnCarrito_Ignora(string? referencia)
    {
        PagoEnLaPasarela("p", EstadoPagoExterno.Aprobado, Dinero.Ars(5500m), referencia, Ahora);

        var resultado = await CrearHandler().Handle(new ProcesarPagoCommand("p"));

        Assert.Equal(ResultadoProcesamientoPago.Ignorado, resultado);
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
    }

    [Fact]
    public async Task Handle_CarritoInexistente_Ignora()
    {
        PagoEnLaPasarela("p", EstadoPagoExterno.Aprobado, Dinero.Ars(5500m), Guid.NewGuid().ToString(), Ahora);

        var resultado = await CrearHandler().Handle(new ProcesarPagoCommand("p"));

        Assert.Equal(ResultadoProcesamientoPago.Ignorado, resultado);
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
    }

    [Theory]
    [InlineData(EstadoPagoExterno.Rechazado)]
    [InlineData(EstadoPagoExterno.Pendiente)]
    [InlineData(EstadoPagoExterno.Devuelto)]
    [InlineData(EstadoPagoExterno.Otro)]
    public async Task Handle_PagoNoAprobado_NoCambiaNada(EstadoPagoExterno estado)
    {
        var carrito = CarritoEnPago(new Asiento('A', 1));
        PagoEnLaPasarela("p", estado, carrito.Total, carrito.Id.ToString(), null);

        var resultado = await CrearHandler().Handle(new ProcesarPagoCommand("p"));

        Assert.Equal(ResultadoProcesamientoPago.NoAprobado, resultado);
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
        Assert.Equal(EstadoCarrito.Pagando, carrito.Estado);
    }

    [Fact]
    public async Task Handle_PagoYaProcesado_NoCambiaNada()
    {
        var carrito = CarritoEnPago(new Asiento('A', 1));
        PagoEnLaPasarela("p", EstadoPagoExterno.Aprobado, carrito.Total, carrito.Id.ToString(), Ahora);
        _registro.YaProcesado("p", Arg.Any<CancellationToken>()).Returns(true);

        var resultado = await CrearHandler().Handle(new ProcesarPagoCommand("p"));

        Assert.Equal(ResultadoProcesamientoPago.YaProcesado, resultado);
        Assert.Equal(EstadoCarrito.Pagando, carrito.Estado);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
        await _registro.DidNotReceive().Registrar(Arg.Any<PagoProcesado>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PagoAprobadoATiempoPorElTotal_AcreditaEmiteEntradasYRegistra()
    {
        var carrito = CarritoEnPago(new Asiento('A', 1), new Asiento('A', 2));
        var aprobadoEn = Ahora.AddMinutes(2);
        PagoEnLaPasarela("p", EstadoPagoExterno.Aprobado, carrito.Total, carrito.Id.ToString(), aprobadoEn);
        Reloj.Ahora.Returns(Ahora.AddMinutes(3));

        var resultado = await CrearHandler().Handle(new ProcesarPagoCommand("p"));

        Assert.Equal(ResultadoProcesamientoPago.Acreditado, resultado);
        Assert.Equal(EstadoCarrito.Pagado, carrito.Estado);
        Assert.Equal(["COD-1", "COD-2"], carrito.Entradas.Select(e => e.Codigo));
        Assert.All(carrito.Entradas, e => Assert.Equal("Interstellar", e.PeliculaTitulo));
        Outbox.Received(1).Registrar(Arg.Is<object>(e => e is CarritoPagado && ((CarritoPagado)e).PagoId == "p"));
        await _registro.Received(1).Registrar(Arg.Is<PagoProcesado>(p =>
            p.PagoId == "p" && p.Resultado == ResultadoPagoProcesado.Acreditado && p.AprobadoEn == aprobadoEn), Arg.Any<CancellationToken>());
        await _registro.DidNotReceive().AgregarDevolucion(Arg.Any<Devolucion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PagoNoAcreditable_RegistraDevolucionPendientePorElMontoCompleto()
    {
        var carrito = CarritoEnPago(new Asiento('A', 1));
        PagoEnLaPasarela("p", EstadoPagoExterno.Aprobado, Dinero.Ars(1m), carrito.Id.ToString(), Ahora.AddMinutes(1));

        var resultado = await CrearHandler().Handle(new ProcesarPagoCommand("p"));

        Assert.Equal(ResultadoProcesamientoPago.Devuelto, resultado);
        Assert.Equal(EstadoCarrito.Pagando, carrito.Estado);
        await _registro.Received(1).AgregarDevolucion(Arg.Is<Devolucion>(d =>
            d.PagoId == "p" && d.Monto == Dinero.Ars(1m) && d.Motivo == MotivoDevolucion.MontoDistinto &&
            d.Estado == EstadoDevolucion.Pendiente), Arg.Any<CancellationToken>());
        Outbox.Received(1).Registrar(Arg.Is<object>(e => e is DevolucionSolicitada && ((DevolucionSolicitada)e).PagoId == "p"));
        await _registro.Received(1).Registrar(Arg.Is<PagoProcesado>(p => p.Resultado == ResultadoPagoProcesado.Devuelto),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PasarelaNoDisponible_PropagaLaExcepcion()
    {
        _procesador.ObtenerPago("p", Arg.Any<CancellationToken>()).ThrowsAsync(new ProcesadorDePagosNoDisponibleException("caída"));

        await Assert.ThrowsAsync<ProcesadorDePagosNoDisponibleException>(() => CrearHandler().Handle(new ProcesarPagoCommand("p")));
    }
}
