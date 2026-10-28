using CinemaSystemRandomPlay.Application.Carritos;
using CinemaSystemRandomPlay.Application.Carritos.Commands;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

public class CrearCarritoCommandHandlerTests
{
    private readonly UnidadDeTrabajoFake _unidadDeTrabajo = new();
    private readonly IFuncionRepository _funcionRepository = Substitute.For<IFuncionRepository>();
    private readonly IPeliculaRepository _peliculaRepository = Substitute.For<IPeliculaRepository>();
    private readonly ICarritoRepository _carritoRepository = Substitute.For<ICarritoRepository>();
    private readonly IConsultaCarritos _consultaCarritos = Substitute.For<IConsultaCarritos>();
    private readonly IEmisorDeTokenDeCarrito _emisorDeToken = Substitute.For<IEmisorDeTokenDeCarrito>();
    private readonly IDesafioAntiBots _desafio = Substitute.For<IDesafioAntiBots>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();
    private readonly List<Carrito> _activos = new();
    private Carrito? _agregado;

    public CrearCarritoCommandHandlerTests()
    {
        _reloj.Ahora.Returns(Ahora);
        _desafio.Verificar(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(true);
        _carritoRepository.ListarActivosDeFuncion(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_activos);
        _carritoRepository.Agregar(Arg.Do<Carrito>(c => _agregado = c), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _emisorDeToken.Emitir(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>())
            .Returns(llamada => new TokenDeCarrito($"token-{llamada.Arg<Guid>()}", Ahora.AddHours(1)));
    }

    private CrearCarritoCommandHandler CrearHandler() =>
        new(_unidadDeTrabajo, _funcionRepository, _peliculaRepository, _carritoRepository, _consultaCarritos, _emisorDeToken, _desafio,
            _outbox, _reloj, new AsignadorDeAsientos(), new ReglasCarrito());

    private Funcion ConfigurarFuncion(bool peliculaActiva = true, DateTimeOffset? inicio = null)
    {
        var (funcion, pelicula) = CrearFuncion(peliculaActiva, inicio);
        _funcionRepository.ObtenerConSalaBloqueando(funcion.Id, Arg.Any<CancellationToken>()).Returns(funcion);
        _peliculaRepository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);
        _consultaCarritos.ObtenerPorId(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => _agregado is null ? null : LecturaDe(_agregado, funcion));
        return funcion;
    }

    private static AsientoPedidoDto[] Pedido(params (string Fila, int Numero)[] asientos) =>
        asientos.Select(a => new AsientoPedidoDto(a.Fila, a.Numero)).ToArray();

    private async Task AssertNadaEscrito()
    {
        await _carritoRepository.DidNotReceive().Agregar(Arg.Any<Carrito>(), Arg.Any<CancellationToken>());
        _outbox.DidNotReceive().Registrar(Arg.Any<object>());
        _emisorDeToken.DidNotReceive().Emitir(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task Handle_PedidoValido_CreaElCarritoRegistraElEventoYEmiteElToken()
    {
        var funcion = ConfigurarFuncion();

        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, Pedido(("A", 2), ("A", 1))));

        Assert.Equal(EstadoOperacionCarrito.Ok, resultado.Estado);
        var carrito = resultado.Carrito!;
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
        Assert.Equal(Ahora.AddMinutes(5), carrito.VenceEn);
        Assert.Equal([new AsientoPedidoDto("A", 1), new AsientoPedidoDto("A", 2)], carrito.Asientos);
        Assert.Equal("Sucursal Centro", carrito.Funcion.SucursalNombre);
        Assert.Equal($"token-{carrito.Id}", resultado.Token!.Valor);
        Assert.Equal(_agregado!.Id, carrito.Id);
        _outbox.Received(1).Registrar(Arg.Is<object>(e => e is CarritoCreado && ((CarritoCreado)e).CarritoId == carrito.Id));
    }

    [Fact]
    public async Task Handle_BloqueaLaFuncionDentroDeLaTransaccion()
    {
        var funcion = ConfigurarFuncion();

        await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, Pedido(("A", 1))));

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
        { new[] { new AsientoPedidoDto("A", 0) } }
    };

    [Theory]
    [MemberData(nameof(PedidosInvalidos))]
    public async Task Handle_PedidoInvalido_RechazaSinAbrirTransaccionNiEvaluarElDesafio(AsientoPedidoDto[]? pedido)
    {
        var funcion = ConfigurarFuncion();

        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, pedido));

        Assert.Equal(EstadoOperacionCarrito.PedidoInvalido, resultado.Estado);
        Assert.True(resultado.Errores!.ContainsKey("asientos"));
        Assert.Equal(0, _unidadDeTrabajo.Ejecuciones);
        await _desafio.DidNotReceive().Verificar(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DesafioRechazado_NoAbreTransaccionNiEscribeNada()
    {
        var funcion = ConfigurarFuncion();
        _desafio.Verificar("respuesta", "10.0.0.1", Arg.Any<CancellationToken>()).Returns(false);

        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, Pedido(("A", 1)), "respuesta", "10.0.0.1"));

        Assert.Equal(EstadoOperacionCarrito.DesafioRechazado, resultado.Estado);
        Assert.Equal(0, _unidadDeTrabajo.Ejecuciones);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_FuncionInexistente_DevuelveFuncionNoEncontrada()
    {
        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(Guid.NewGuid(), Pedido(("A", 1))));

        Assert.Equal(EstadoOperacionCarrito.FuncionNoEncontrada, resultado.Estado);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_PeliculaInactiva_DevuelveFuncionNoEncontrada()
    {
        var funcion = ConfigurarFuncion(peliculaActiva: false);

        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, Pedido(("A", 1))));

        Assert.Equal(EstadoOperacionCarrito.FuncionNoEncontrada, resultado.Estado);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_FuncionComenzada_DevuelveFuncionNoDisponible()
    {
        var funcion = ConfigurarFuncion(inicio: Ahora.AddMinutes(-1));

        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, Pedido(("A", 1))));

        Assert.Equal(EstadoOperacionCarrito.FuncionNoDisponible, resultado.Estado);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_AsientosInexistentes_LosInforma()
    {
        var funcion = ConfigurarFuncion();

        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, Pedido(("C", 1), ("A", 1))));

        Assert.Equal(EstadoOperacionCarrito.AsientosInexistentes, resultado.Estado);
        Assert.Equal([new AsientoPedidoDto("C", 1)], resultado.Asientos);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_AsientosRetenidosPorOtroCarrito_LosInforma()
    {
        var funcion = ConfigurarFuncion();
        _activos.Add(CarritoDe(funcion, Ahora, new Asiento('A', 1)));

        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, Pedido(("A", 1), ("A", 2))));

        Assert.Equal(EstadoOperacionCarrito.AsientosNoDisponibles, resultado.Estado);
        Assert.Equal([new AsientoPedidoDto("A", 1)], resultado.Asientos);
        await AssertNadaEscrito();
    }

    [Fact]
    public async Task Handle_FuncionEnElTope_DevuelveFuncionSinCupo()
    {
        // 20 asientos al 50%: tope 10.
        var funcion = ConfigurarFuncion();
        _activos.Add(CarritoDe(funcion, Ahora, Enumerable.Range(1, 10).Select(n => new Asiento('A', n)).ToArray()));

        var resultado = await CrearHandler().Handle(new CrearCarritoCommand(funcion.Id, Pedido(("B", 1))));

        Assert.Equal(EstadoOperacionCarrito.FuncionSinCupo, resultado.Estado);
        await AssertNadaEscrito();
    }
}
