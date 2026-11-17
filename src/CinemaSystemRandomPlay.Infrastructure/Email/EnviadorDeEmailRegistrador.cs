using CinemaSystemRandomPlay.Application.Compartido.Ports;
using Microsoft.Extensions.Logging;

namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>
/// Implementación de desarrollo y pruebas: registra el destinatario y el asunto en lugar de enviar el mensaje. El cuerpo no
/// se registra, porque puede llevar datos de la entrada. Solo se registra en Development y Testing (ver Program.cs).
/// </summary>
public class EnviadorDeEmailRegistrador : IEnviadorDeEmail
{
    private readonly ILogger<EnviadorDeEmailRegistrador> _logger;

    public EnviadorDeEmailRegistrador(ILogger<EnviadorDeEmailRegistrador> logger)
    {
        _logger = logger;
    }

    public Task Enviar(MensajeDeEmail mensaje, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Email (no enviado, solo registrado). Destinatario: {Destinatario}. Asunto: {Asunto}",
            mensaje.Destinatario, mensaje.Asunto);

        return Task.CompletedTask;
    }
}
