using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Commands;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Application.UnitTests.Carritos;
using CinemaSystemRandomPlay.Domain.Pagos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CinemaSystemRandomPlay.Application.UnitTests.Pagos;

public class RecibirAvisoDePagoCommandHandlerTests : OperacionesSobreCarritoTestBase
{
    private readonly IVerificadorDeAvisos _verificador = Substitute.For<IVerificadorDeAvisos>();
    private readonly IProcesadorDePagos _procesador = Substitute.For<IProcesadorDePagos>();

    public RecibirAvisoDePagoCommandHandlerTests()
    {
        _verificador.EsValido(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>()).Returns(true);
    }

    private RecibirAvisoDePagoCommandHandler CrearHandler() =>
        new(_verificador, new ProcesarPagoCommandHandler(_procesador, UnidadDeTrabajo, FuncionRepository, CarritoRepository,
            ConsultaCarritos, Substitute.For<IRegistroDePagos>(), new ConfirmadorDePagos(), Substitute.For<IGeneradorDeCodigos>(), Outbox,
            Reloj));

    [Fact]
    public async Task Handle_FirmaInvalida_NoConsultaNada()
    {
        _verificador.EsValido("ts=1,v1=00", "r", "123").Returns(false);

        var (resultado, _) = await CrearHandler().Handle(new RecibirAvisoDePagoCommand("ts=1,v1=00", "r", "123", "payment"));

        Assert.Equal(ResultadoAviso.FirmaInvalida, resultado);
        await _procesador.DidNotReceive().ObtenerPago(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("merchant_order", "123")]
    [InlineData(null, "123")]
    [InlineData("payment", null)]
    [InlineData("payment", " ")]
    public async Task Handle_TipoDistintoDePagoOSinId_Ignora(string? tipo, string? dataId)
    {
        var (resultado, _) = await CrearHandler().Handle(new RecibirAvisoDePagoCommand("firma", "r", dataId, tipo));

        Assert.Equal(ResultadoAviso.Ignorado, resultado);
        await _procesador.DidNotReceive().ObtenerPago(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AvisoDePagoValido_ConsultaElPagoPorSuId()
    {
        _procesador.ObtenerPago("123", Arg.Any<CancellationToken>()).Returns((PagoConsultado?)null);

        var (resultado, procesamiento) = await CrearHandler().Handle(new RecibirAvisoDePagoCommand("firma", "r", "123", "payment"));

        Assert.Equal(ResultadoAviso.Procesado, resultado);
        Assert.Equal(ResultadoProcesamientoPago.Ignorado, procesamiento);
        await _procesador.Received(1).ObtenerPago("123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PasarelaNoDisponible_DevuelveProcesadorNoDisponible()
    {
        _procesador.ObtenerPago("123", Arg.Any<CancellationToken>()).ThrowsAsync(new ProcesadorDePagosNoDisponibleException("caída"));

        var (resultado, _) = await CrearHandler().Handle(new RecibirAvisoDePagoCommand("firma", "r", "123", "payment"));

        Assert.Equal(ResultadoAviso.ProcesadorNoDisponible, resultado);
    }
}
