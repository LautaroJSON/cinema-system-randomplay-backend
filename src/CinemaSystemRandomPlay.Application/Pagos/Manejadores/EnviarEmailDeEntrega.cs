using System.Text.Json;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Application.Pagos.Manejadores;

/// <summary>
/// Envía el email con las entradas cuando un carrito queda pagado (spec 008, FR-025 y FR-026). Si el envío falla, lanza y el Outbox
/// lo reintenta; la compra no se deshace. Un carrito con la entrega ya enviada no se reenvía.
/// </summary>
public class EnviarEmailDeEntrega : IManejadorDeMensaje
{
    private readonly IConsultaCarritos _consultaCarritos;
    private readonly ICarritoRepository _carritoRepository;
    private readonly IEnviadorDeEmail _enviadorDeEmail;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public EnviarEmailDeEntrega(
        IConsultaCarritos consultaCarritos,
        ICarritoRepository carritoRepository,
        IEnviadorDeEmail enviadorDeEmail,
        IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj)
    {
        _consultaCarritos = consultaCarritos;
        _carritoRepository = carritoRepository;
        _enviadorDeEmail = enviadorDeEmail;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public string Tipo => nameof(CarritoPagado);

    public async Task Manejar(string payload, CancellationToken cancellationToken)
    {
        var evento = JsonSerializer.Deserialize<ReferenciaACarrito>(payload)
                     ?? throw new InvalidOperationException("El mensaje CarritoPagado no tiene carrito.");

        var lectura = await _consultaCarritos.ObtenerPorId(evento.CarritoId, cancellationToken);
        if (lectura is null || lectura.EntregaEnviadaEn is not null || lectura.EmailEntrega is null)
            return;

        var carrito = CarritoDto.DesdeLectura(lectura, _reloj.Ahora);
        if (carrito.Estado != EstadoCarrito.Pagado)
            return;

        await _enviadorDeEmail.Enviar(MensajeDeEntrega.Crear(carrito), cancellationToken);

        await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            var entidad = await _carritoRepository.ObtenerPorId(evento.CarritoId, ct);
            entidad?.MarcarEntregaEnviada(_reloj.Ahora);
            return true;
        }, cancellationToken);
    }

    private sealed record ReferenciaACarrito(Guid CarritoId);
}
