using CinemaSystemRandomPlay.Application.Reservas.Dtos;
using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Application.Reservas.Queries;

public record ObtenerMiReservaQuery(Guid ReservaId, EmailComprador Comprador);

/// <summary>Devuelve <c>null</c> si la reserva no existe o pertenece a otro comprador.</summary>
public class ObtenerMiReservaQueryHandler
{
    private readonly IConsultaReservas _consultaReservas;
    private readonly IReloj _reloj;

    public ObtenerMiReservaQueryHandler(IConsultaReservas consultaReservas, IReloj reloj)
    {
        _consultaReservas = consultaReservas;
        _reloj = reloj;
    }

    public async Task<ReservaDto?> Handle(ObtenerMiReservaQuery query, CancellationToken cancellationToken = default)
    {
        var lectura = await _consultaReservas.ObtenerDeComprador(query.ReservaId, query.Comprador, cancellationToken);

        return lectura is null ? null : ReservaDto.DesdeLectura(lectura, _reloj.Ahora);
    }
}
