using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Reservas.Commands;
using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Domain.Reservas.Eventos;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Reservas.ReservasTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Reservas;

public class CancelarReservaCommandHandlerTests
{
    private readonly ReservasTestSupport.UnidadDeTrabajoFake _unidadDeTrabajo = new();
    private readonly IReservaRepository _reservaRepository = Substitute.For<IReservaRepository>();
    private readonly IConsultaReservas _consulta = Substitute.For<IConsultaReservas>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();

    public CancelarReservaCommandHandlerTests()
    {
        _reloj.Ahora.Returns(Ahora);
    }

    private CancelarReservaCommandHandler CrearHandler() => new(_unidadDeTrabajo, _reservaRepository, _consulta, _outbox, _reloj);

    private Reserva CrearReserva(string cliente, DateTimeOffset creadaEn)
    {
        var (funcion, _) = CrearFuncion();
        var reserva = new ReservadorDeAsientos().Reservar(funcion, [], cliente, [new Asiento('A', 1)], creadaEn, new ReglasReserva()).Reserva!;
        _reservaRepository.ObtenerPorId(reserva.Id, Arg.Any<CancellationToken>()).Returns(reserva);
        return reserva;
    }

    private static ReservaLecturaDto LecturaCancelada(Reserva r) =>
        new(r.Id, EstadoReserva.Cancelada, r.CreadaEn, r.VenceEn, Ahora, r.Asientos, r.FuncionId, Ahora.AddHours(3),
            Guid.NewGuid(), "Interstellar", Guid.NewGuid(), "Sucursal Centro", Guid.NewGuid(), "Sala 1", r.PrecioUnitario);

    [Fact]
    public async Task Handle_ReservaPropiaVigente_LaCancelaYRegistraElEvento()
    {
        var reserva = CrearReserva("ana", Ahora);
        _consulta.ObtenerDeCliente(reserva.Id, "ana", Arg.Any<CancellationToken>()).Returns(LecturaCancelada(reserva));

        var resultado = await CrearHandler().Handle(new CancelarReservaCommand(reserva.Id, "ana"));

        Assert.Equal(EstadoCancelarReserva.Ok, resultado.Estado);
        Assert.Equal(EstadoReserva.Cancelada, resultado.Reserva!.Estado);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
        _outbox.Received(1).Registrar(Arg.Is<object>(e => e is ReservaCancelada && ((ReservaCancelada)e).ReservaId == reserva.Id));
    }

    [Fact]
    public async Task Handle_ReservaInexistente_DevuelveNoEncontrada()
    {
        var resultado = await CrearHandler().Handle(new CancelarReservaCommand(Guid.NewGuid(), "ana"));

        Assert.Equal(EstadoCancelarReserva.ReservaNoEncontrada, resultado.Estado);
        _outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ReservaAjena_DevuelveNoEncontradaYNoLaToca()
    {
        var reserva = CrearReserva("ana", Ahora);

        var resultado = await CrearHandler().Handle(new CancelarReservaCommand(reserva.Id, "beto"));

        Assert.Equal(EstadoCancelarReserva.ReservaNoEncontrada, resultado.Estado);
        Assert.Equal(EstadoReserva.PendienteDePago, reserva.Estado);
        _outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ReservaVencida_DevuelveNoCancelableConElEstadoActual()
    {
        var reserva = CrearReserva("ana", Ahora.AddMinutes(-20));

        var resultado = await CrearHandler().Handle(new CancelarReservaCommand(reserva.Id, "ana"));

        Assert.Equal(EstadoCancelarReserva.NoCancelable, resultado.Estado);
        Assert.Equal(EstadoReserva.Expirada, resultado.EstadoActual);
        _outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ReservaYaCancelada_DevuelveNoCancelableCancelada()
    {
        var reserva = CrearReserva("ana", Ahora);
        reserva.Cancelar(Ahora);

        var resultado = await CrearHandler().Handle(new CancelarReservaCommand(reserva.Id, "ana"));

        Assert.Equal(EstadoCancelarReserva.NoCancelable, resultado.Estado);
        Assert.Equal(EstadoReserva.Cancelada, resultado.EstadoActual);
        _outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }
}
