using CinemaSystemRandomPlay.Application.Verificaciones.Ports;

namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>
/// Se registra fuera de Development y Testing: el envío real de emails está fuera de alcance de esta feature, y registrar el
/// código en los logs de un entorno real lo filtraría. Falla de forma explícita en lugar de enviar a ningún lado.
/// </summary>
public class EnviadorDeEmailNoConfigurado : IEnviadorDeEmail
{
    public Task Enviar(MensajeDeVerificacion mensaje, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "No hay un servicio de envío de email configurado para este entorno. El envío real de emails está fuera de alcance " +
            "de la feature 006; en Development y Testing se usa un registrador que escribe el mensaje en el log.");
}
