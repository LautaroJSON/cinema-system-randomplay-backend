using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Reservas.Commands;
using CinemaSystemRandomPlay.Application.Reservas.Dtos;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Domain.Reservas.Eventos;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Reservas.ReservasTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Reservas;

public class ReservarAsientosCommandHandlerTests
{
    private readonly ReservasTestSupport.UnidadDeTrabajoFake _unidadDeTrabajo = new();
    private readonly IFuncionRepository _funcionRepository = Substitute.For<IFuncionRepository>();
    private readonly IPeliculaRepository _peliculaRepository = Substitute.For<IPeliculaRepository>();
    private readonly IReservaRepository _reservaRepository = Substitute.For<IReservaRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();

    public ReservarAsientosCommandHandlerTests()
    {
        _reloj.Ahora.Returns(Ahora);
        _reservaRepository.ListarVigentesDeFuncion(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Reserva>());
    }

    private ReservarAsientosCommandHandler CrearHandler() =>
        new(_unidadDeTrabajo, _funcionRepository, _peliculaRepository, _reservaRepository, _outbox, _reloj, new ReservadorDeAsientos(), new ReglasReserva());

    private (Funcion Funcion, Pelicula Pelicula) ConfigurarFuncion(bool peliculaActiva = true, DateTimeOffset? inicio = null)
    {
        var (funcion, pelicula) = CrearFuncion(peliculaActiva, inicio);
        _funcionRepository.ObtenerConSalaBloqueando(funcion.Id, Arg.Any<CancellationToken>()).Returns(funcion);
        _peliculaRepository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);
        return (funcion, pelicula);
    }

    private static AsientoPedidoDto[] Pedido(params (string Fila, int Numero)[] asientos) =>
        asientos.Select(a => new AsientoPedidoDto(a.Fila, a.Numero)).ToArray();

    private async Task AssertNadaEscrito()
    {
        await _reservaRepository.DidNotReceive().Agregar(Arg.Any<Reserva>(), Arg.Any<CancellationToken>());
        _outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_PedidoValido_CreaLaReservaYRegistraElEvento()
    {
        var (funcion, pelicula) = ConfigurarFuncion();

        var resultado = await CrearHandler().Handle(new ReservarAsientosCommand(funcion.Id, "ana", Pedido(("A", 2), ("A", 1))));

        Assert.Equal(EstadoReservarAsientos.Ok, resultado.Estado);
        var reserva = resultado.Reserva!;
        Assert.Equal(EstadoReserva.PendienteDePago, reserva.Estado);
        Assert.Equal(Ahora.AddMinutes(10), reserva.VenceEn);
        Assert.Equal([new AsientoPedidoDto("A", 1), new AsientoPedidoDto("A", 2)], reserva.Asientos);
        Assert.Equal(pelicula.Titulo, reserva.Funcion.PeliculaTitulo);
        Assert.Equal("Sucursal Centro", reserva.Funcion.SucursalNombre);
        await _reservaRepository.Received(1).Agregar(Arg.Is<Reserva>(r => r.ClienteId == "ana" && r.Id == reserva.Id), Arg.Any<CancellationToken>());
        _outbox.Received(1).Registrar(Arg.Is<object>(e => e is ReservaCreada && ((ReservaCreada)e).ReservaId == reserva.Id));
    }

    [Fact]
    public async Task Handle_BloqueaLaFuncionDentroDeLaTransaccion()
    {
        var (funcion, _) = ConfigurarFuncion();

        await CrearHandler().Handle(new ReservarAsientosCommand(funcion.Id, "ana", Pedido(("A", 1))));

        Assert.Equal(1, _unidadDeTrabajo.Ejecuciones);
        await _funcionRepository.Received(1).ObtenerConSalaBloqueando(funcion.Id, Arg.Any<CancellationToken>());
    }

    public static TheoryData<AsientoPedidoDto[]?> PedidosInvalidos => new()
    {
        { null },
        { Array.Empty<AsientoPedidoDto>() },
        { Enumerable.Range(1, 11).Select(n => new AsientoPedidoDto("A", n)).ToArray() },
        { new[] { new AsientoPedidoDto("A", 1), new AsientoPedidoDto("A", 1) } },
        { new[] { new AsientoPedidoDto("a", 1) } },
        { new[] { new AsientoPedidoDto("AB", 1) } },
        { new[] { new AsientoPedidoDto("", 1) } },
        { new[] { new AsientoPedidoDto("A", 0) } }
    };

    [Theory]
    [MemberData(nameof(PedidosInvalidos))]
    public async Task Handle_PedidoMalFormado_DevuelvePedidoInvalidoSinTocarNada(AsientoPedidoDto[]? asientos)
    {
        var (funcion, _) = ConfigurarFuncion();

        var resultado = await CrearHandler().Handle(new ReservarAsientosCommand(funcion.Id, "ana", asientos));

        Assert.Equal(EstadoReservarAsientos.PedidoInvalido, resultado.Estado);
        Assert.NotEmpty(resultado.Errores!);
        Assert.Equal(0, _unidadDeTrabajo.Ejecuciones);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_FuncionInexistente_DevuelveFuncionNoEncontrada()
    {
        var resultado = await CrearHandler().Handle(new ReservarAsientosCommand(Guid.NewGuid(), "ana", Pedido(("A", 1))));

        Assert.Equal(EstadoReservarAsientos.FuncionNoEncontrada, resultado.Estado);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_PeliculaInactiva_DevuelveFuncionNoEncontrada()
    {
        var (funcion, _) = ConfigurarFuncion(peliculaActiva: false);

        var resultado = await CrearHandler().Handle(new ReservarAsientosCommand(funcion.Id, "ana", Pedido(("A", 1))));

        Assert.Equal(EstadoReservarAsientos.FuncionNoEncontrada, resultado.Estado);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_FuncionYaComenzada_DevuelveFuncionNoDisponible()
    {
        var (funcion, _) = ConfigurarFuncion(inicio: Ahora.AddMinutes(-1));

        var resultado = await CrearHandler().Handle(new ReservarAsientosCommand(funcion.Id, "ana", Pedido(("A", 1))));

        Assert.Equal(EstadoReservarAsientos.FuncionNoDisponible, resultado.Estado);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_AsientoInexistenteEnLaSala_DevuelveLosInexistentesSinEscribir()
    {
        var (funcion, _) = ConfigurarFuncion();

        var resultado = await CrearHandler().Handle(new ReservarAsientosCommand(funcion.Id, "ana", Pedido(("A", 1), ("Z", 1))));

        Assert.Equal(EstadoReservarAsientos.AsientosInexistentes, resultado.Estado);
        Assert.Equal([new AsientoPedidoDto("Z", 1)], resultado.Asientos);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_AsientoYaRetenido_DevuelveNoDisponiblesSinEscribir()
    {
        var (funcion, _) = ConfigurarFuncion();
        var previa = new ReservadorDeAsientos().Reservar(funcion, [], "beto", [new Asiento('A', 1)], Ahora, new ReglasReserva()).Reserva!;
        _reservaRepository.ListarVigentesDeFuncion(funcion.Id, Ahora, Arg.Any<CancellationToken>()).Returns(new[] { previa });

        var resultado = await CrearHandler().Handle(new ReservarAsientosCommand(funcion.Id, "ana", Pedido(("A", 1), ("A", 2))));

        Assert.Equal(EstadoReservarAsientos.AsientosNoDisponibles, resultado.Estado);
        Assert.Equal([new AsientoPedidoDto("A", 1)], resultado.Asientos);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_SuperaElTopeDelCliente_DevuelveLimiteSuperadoSinEscribir()
    {
        var (funcion, _) = ConfigurarFuncion();
        var siete = Enumerable.Range(1, 7).Select(n => new Asiento('A', n)).ToArray();
        var previa = new ReservadorDeAsientos().Reservar(funcion, [], "ana", siete, Ahora, new ReglasReserva()).Reserva!;
        _reservaRepository.ListarVigentesDeFuncion(funcion.Id, Ahora, Arg.Any<CancellationToken>()).Returns(new[] { previa });

        var resultado = await CrearHandler().Handle(new ReservarAsientosCommand(funcion.Id, "ana",
            Pedido(("B", 1), ("B", 2), ("B", 3), ("B", 4))));

        Assert.Equal(EstadoReservarAsientos.LimiteSuperado, resultado.Estado);
        Assert.Equal(3, resultado.AsientosRestantes);
        await AssertNadaEscrito();
    }
}
