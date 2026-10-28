using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Application.Compartido.Ports;

public record MensajeDeEmail(EmailComprador Destinatario, string Asunto, string Cuerpo);

/// <summary>
/// Envío de emails al comprador. En la feature 007 nadie lo invoca: lo usará la feature de pago para enviar la entrada.
/// Se envía siempre después de confirmar la transacción, nunca dentro de ella.
/// </summary>
public interface IEnviadorDeEmail
{
    Task Enviar(MensajeDeEmail mensaje, CancellationToken cancellationToken = default);
}
