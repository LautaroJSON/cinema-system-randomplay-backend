using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Reservas.Dtos;
using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Domain.Reservas.Eventos;

namespace CinemaSystemRandomPlay.Application.Reservas.Commands;

public record CancelarReservaCommand(Guid ReservaId, string ClienteId);

public enum EstadoCancelarReserva
{
    Ok,
    ReservaNoEncontrada,
    NoCancelable
}

public record CancelarReservaResultado(EstadoCancelarReserva Estado, ReservaDto? Reserva = null, EstadoReserva? EstadoActual = null)
{
    public static CancelarReservaResultado Ok(ReservaDto reserva) => new(EstadoCancelarReserva.Ok, Reserva: reserva);

    public static CancelarReservaResultado ReservaNoEncontrada() => new(EstadoCancelarReserva.ReservaNoEncontrada);

    public static CancelarReservaResultado NoCancelable(EstadoReserva estadoActual) => new(EstadoCancelarReserva.NoCancelable, EstadoActual: estadoActual);
}

public class CancelarReservaCommandHandler
{
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReservaRepository _reservaRepository;
    private readonly IConsultaReservas _consultaReservas;
    private readonly IOutbox _outbox;
    private readonly IReloj _reloj;

    public CancelarReservaCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IReservaRepository reservaRepository,
        IConsultaReservas consultaReservas,
        IOutbox outbox,
        IReloj reloj)
    {
        _unidadDeTrabajo = unidadDeTrabajo;
        _reservaRepository = reservaRepository;
        _consultaReservas = consultaReservas;
        _outbox = outbox;
        _reloj = reloj;
    }

    public async Task<CancelarReservaResultado> Handle(CancelarReservaCommand command, CancellationToken cancellationToken = default)
    {
        var resultado = await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            var reserva = await _reservaRepository.ObtenerPorId(command.ReservaId, ct);

            if (reserva is null || reserva.ClienteId != command.ClienteId)
                return CancelarReservaResultado.ReservaNoEncontrada();

            var ahora = _reloj.Ahora;
            var cancelacion = reserva.Cancelar(ahora);

            if (!cancelacion.Cancelada)
                return CancelarReservaResultado.NoCancelable(cancelacion.EstadoActual);

            _outbox.Registrar(new ReservaCancelada(reserva.Id, reserva.FuncionId, reserva.Asientos, ahora));

            return CancelarReservaResultado.Ok(null!);
        }, cancellationToken);

        if (resultado.Estado != EstadoCancelarReserva.Ok)
            return resultado;

        // La lectura va después de confirmar la transacción para ver la reserva ya cancelada.
        var lectura = await _consultaReservas.ObtenerDeCliente(command.ReservaId, command.ClienteId, cancellationToken);

        return CancelarReservaResultado.Ok(ReservaDto.DesdeLectura(lectura!, _reloj.Ahora));
    }
}
