using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Application.Compartido.Ports;

/// <param name="Cuerpo">Texto plano.</param>
/// <param name="CuerpoHtml">Versión HTML opcional del mismo contenido.</param>
public record MensajeDeEmail(EmailComprador Destinatario, string Asunto, string Cuerpo, string? CuerpoHtml = null);

/// <summary>
/// Envío de emails al comprador: las entradas de un carrito pagado (feature 008). Se envía siempre desde el Outbox, después de
/// confirmar la transacción, nunca dentro de ella.
/// </summary>
public interface IEnviadorDeEmail
{
    Task Enviar(MensajeDeEmail mensaje, CancellationToken cancellationToken = default);
}
