using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using Microsoft.Extensions.Logging;

namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>
/// Implementación de desarrollo y pruebas: registra el mensaje (destinatario y código) en lugar de enviarlo. Escribe el
/// código en claro, así que solo se registra en Development y Testing (ver Program.cs).
/// </summary>
public class EnviadorDeEmailRegistrador : IEnviadorDeEmail
{
    private readonly ILogger<EnviadorDeEmailRegistrador> _logger;

    public EnviadorDeEmailRegistrador(ILogger<EnviadorDeEmailRegistrador> logger)
    {
        _logger = logger;
    }

    public Task Enviar(MensajeDeVerificacion mensaje, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Email de verificación (no enviado, solo registrado). Destinatario: {Destinatario}. Código: {Codigo}",
            mensaje.Destinatario, mensaje.Codigo);

        return Task.CompletedTask;
    }
}
