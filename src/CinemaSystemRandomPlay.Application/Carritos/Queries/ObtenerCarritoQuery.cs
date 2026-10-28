using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Application.Carritos.Queries;

/// <param name="CarritoDelToken">Id del carrito al que da acceso el token del pedido.</param>
public record ObtenerCarritoQuery(Guid CarritoId, Guid CarritoDelToken);

/// <summary>Devuelve <c>null</c> si el carrito no existe o el token es de otro carrito, sin distinguir los dos casos.</summary>
public class ObtenerCarritoQueryHandler
{
    private readonly IConsultaCarritos _consultaCarritos;
    private readonly IReloj _reloj;

    public ObtenerCarritoQueryHandler(IConsultaCarritos consultaCarritos, IReloj reloj)
    {
        _consultaCarritos = consultaCarritos;
        _reloj = reloj;
    }

    public async Task<CarritoDto?> Handle(ObtenerCarritoQuery query, CancellationToken cancellationToken = default)
    {
        if (query.CarritoId != query.CarritoDelToken)
            return null;

        var lectura = await _consultaCarritos.ObtenerPorId(query.CarritoId, cancellationToken);

        return lectura is null ? null : CarritoDto.DesdeLectura(lectura, _reloj.Ahora);
    }
}
