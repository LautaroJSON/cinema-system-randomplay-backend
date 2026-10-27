using CinemaSystemRandomPlay.Application.Reservas.Dtos;
using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Application.Reservas.Queries;

public record ListarMisReservasQuery(string ClienteId);

public class ListarMisReservasQueryHandler
{
    private readonly IConsultaReservas _consultaReservas;
    private readonly IReloj _reloj;

    public ListarMisReservasQueryHandler(IConsultaReservas consultaReservas, IReloj reloj)
    {
        _consultaReservas = consultaReservas;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<ReservaDto>> Handle(ListarMisReservasQuery query, CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.Ahora;
        var reservas = await _consultaReservas.ListarDeCliente(query.ClienteId, cancellationToken);

        return reservas
            .OrderByDescending(r => r.CreadaEn)
            .Select(r => ReservaDto.DesdeLectura(r, ahora))
            .ToList();
    }
}
