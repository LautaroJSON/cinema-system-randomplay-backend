using CinemaSystemRandomPlay.Application.Compartido.Ports;

namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>
/// Se registra fuera de Development y Testing: todavía no hay un servicio de email real (llegará con la feature de pago,
/// que envía la entrada). Falla de forma explícita en lugar de enviar a ningún lado.
/// </summary>
public class EnviadorDeEmailNoConfigurado : IEnviadorDeEmail
{
    public Task Enviar(MensajeDeEmail mensaje, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "No hay un servicio de envío de email configurado para este entorno. El envío real llegará con la feature de pago; " +
            "en Development y Testing se usa un registrador que escribe el mensaje en el log.");
}
