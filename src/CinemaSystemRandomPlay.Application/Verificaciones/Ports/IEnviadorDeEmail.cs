using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Application.Verificaciones.Ports;

public record MensajeDeVerificacion(EmailComprador Destinatario, string Codigo);

public interface IEnviadorDeEmail
{
    Task Enviar(MensajeDeVerificacion mensaje, CancellationToken cancellationToken = default);
}
