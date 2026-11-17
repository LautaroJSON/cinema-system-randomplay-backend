using CinemaSystemRandomPlay.Application.Carritos;
using CinemaSystemRandomPlay.Application.Carritos.Commands;
using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

public class IniciarPagoCommandHandlerTests : OperacionesSobreCarritoTestBase
{
    private readonly IProcesadorDePagos _procesador = Substitute.For<IProcesadorDePagos>();

    public IniciarPagoCommandHandlerTests()
    {
        _procesador.CrearPago(Arg.Any<SolicitudDePago>(), Arg.Any<CancellationToken>())
            .Returns(new PagoCreado("pref-nueva", "https://pago/pref-nueva"));
    }

    private IniciarPagoCommandHandler CrearHandler() =>
        new(UnidadDeTrabajo, FuncionRepository, CarritoRepository, ConsultaCarritos, _procesador, Outbox, Reloj);

    private Carrito ConEmail(params Asiento[] asientos)
    {
        var carrito = Existente(Ahora, asientos);
        carrito.IndicarEmailEntrega(EmailComprador.Crear("ana@example.com"), Ahora);
        return carrito;
    }

    [Fact]
    public async Task Handle_TokenDeOtroCarrito_DevuelveNoEncontradoSinLlamarALaPasarela()
    {
        var carrito = ConEmail(new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, Guid.NewGuid()));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoEncontrado, resultado.Estado);
        await _procesador.DidNotReceive().CrearPago(Arg.Any<SolicitudDePago>(), Arg.Any<CancellationToken>());
        await AssertNoTocaLaBase();
    }

    [Fact]
    public async Task Handle_SinEmail_DevuelveEmailEntregaFaltanteSinLlamarALaPasarela()
    {
        var carrito = Existente(Ahora, new Asiento('A', 1));

        var resultado = await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.EmailEntregaFaltante, resultado.Estado);
        await _procesador.DidNotReceive().CrearPago(Arg.Any<SolicitudDePago>(), Arg.Any<CancellationToken>());
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
    }

    [Fact]
    public async Task Handle_CarritoExpirado_DevuelveCarritoNoActivo()
    {
        var carrito = ConEmail(new Asiento('A', 1));
        Reloj.Ahora.Returns(carrito.VenceEn);

        var resultado = await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.CarritoNoActivo, resultado.Estado);
        Assert.Equal(EstadoCarrito.Expirado, resultado.EstadoActual);
        await _procesador.DidNotReceive().CrearPago(Arg.Any<SolicitudDePago>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_YaEnPago_DevuelveLaMismaUrlSinLlamarALaPasarelaNiExtender()
    {
        var carrito = EnPago(Existente(Ahora, new Asiento('A', 1)), Funcion, Ahora);
        var venceEn = carrito.VenceEn;

        var resultado = await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.Ok, resultado.Estado);
        Assert.Equal("https://pago/pref-1", resultado.UrlPago);
        Assert.Equal(venceEn, carrito.VenceEn);
        await _procesador.DidNotReceive().CrearPago(Arg.Any<SolicitudDePago>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
    }

    [Fact]
    public async Task Handle_PasarelaNoDisponible_DevuelvePagoNoDisponibleSinAbrirLaTransaccion()
    {
        var carrito = ConEmail(new Asiento('A', 1));
        var venceEn = carrito.VenceEn;
        _procesador.CrearPago(Arg.Any<SolicitudDePago>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProcesadorDePagosNoDisponibleException("caída"));

        var resultado = await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.PagoNoDisponible, resultado.Estado);
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
        Assert.Equal(venceEn, carrito.VenceEn);
    }

    [Fact]
    public async Task Handle_CarritoActivo_EnviaALaPasarelaUnItemPorAsientoConElTotalYElVencimientoExtendido()
    {
        var carrito = ConEmail(new Asiento('B', 2), new Asiento('A', 1));
        var venceExtendido = carrito.VenceEn.AddMinutes(1);

        await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, carrito.Id));

        await _procesador.Received(1).CrearPago(Arg.Is<SolicitudDePago>(s =>
            s.CarritoId == carrito.Id &&
            s.Items.Count == 2 &&
            s.Items[0].Titulo == "Interstellar — Sala 1 — Fila A Asiento 1" &&
            s.Items.All(i => i.PrecioUnitario == 5500m) &&
            s.Total == Dinero.Ars(11000m) &&
            s.VenceEn == venceExtendido &&
            s.Desde == Ahora &&
            s.EmailPagador == "ana@example.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CarritoActivo_PasaAPagandoYRegistraPagoIniciado()
    {
        var carrito = ConEmail(new Asiento('A', 1));
        var venceExtendido = carrito.VenceEn.AddMinutes(1);

        var resultado = await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.Ok, resultado.Estado);
        Assert.Equal("https://pago/pref-nueva", resultado.UrlPago);
        Assert.Equal(EstadoCarrito.Pagando, resultado.Carrito!.Estado);
        Assert.Equal(venceExtendido, resultado.Carrito.VenceEn);
        Assert.Equal("https://pago/pref-nueva", resultado.Carrito.Pago!.UrlPago);
        Assert.Equal("pref-nueva", carrito.PreferenciaPagoId);
        Outbox.Received(1).Registrar(Arg.Is<object>(e => e is PagoIniciado && ((PagoIniciado)e).VenceEn == venceExtendido));
    }

    [Fact]
    public async Task Handle_OtroInicioGanoLaCarrera_DevuelveLaUrlGuardadaSinEvento()
    {
        var carrito = ConEmail(new Asiento('A', 1));
        // La lectura previa lo ve activo; cuando llega la transacción, otro pedido ya lo pasó a Pagando.
        ConsultaCarritos.ObtenerPorId(carrito.Id, Arg.Any<CancellationToken>()).Returns(LecturaDe(carrito, Funcion), LecturaDe(carrito, Funcion));
        _procesador.CrearPago(Arg.Any<SolicitudDePago>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            carrito.IniciarPago("pref-ganadora", "https://pago/pref-ganadora", carrito.Asientos, Ahora, Funcion.FechaHoraInicio);
            return new PagoCreado("pref-perdedora", "https://pago/pref-perdedora");
        });

        var resultado = await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.Ok, resultado.Estado);
        Assert.Equal("https://pago/pref-ganadora", resultado.UrlPago);
        Assert.Equal("pref-ganadora", carrito.PreferenciaPagoId);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_SeleccionCambiadaMientrasSeCreabaElPago_DevuelveCarritoModificado()
    {
        var carrito = ConEmail(new Asiento('A', 1));
        _procesador.CrearPago(Arg.Any<SolicitudDePago>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            new AsignadorDeAsientos().Reemplazar(Funcion, carrito, [carrito], [new Asiento('A', 2)], Ahora, new ReglasCarrito());
            return new PagoCreado("pref-nueva", "https://pago/pref-nueva");
        });

        var resultado = await CrearHandler().Handle(new IniciarPagoCommand(carrito.Id, carrito.Id));

        Assert.Equal(EstadoOperacionCarrito.CarritoModificado, resultado.Estado);
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }
}
