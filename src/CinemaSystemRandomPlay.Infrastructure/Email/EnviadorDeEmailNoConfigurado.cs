using CinemaSystemRandomPlay.Application.Compartido.Ports;

namespace CinemaSystemRandomPlay.Infrastructure.Email;

/// <summary>
/// Se registra fuera de Development y Testing cuando no se configuró un proveedor (<c>Email:Proveedor</c>). Falla de forma
/// explícita en lugar de enviar a ningún lado; el Outbox reintenta y deja el mensaje como fallido.
/// </summary>
public class EnviadorDeEmailNoConfigurado : IEnviadorDeEmail
{
    public Task Enviar(MensajeDeEmail mensaje, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "No hay un servicio de envío de email configurado para este entorno: configurar Email:Proveedor=Smtp y Email:Smtp " +
            "(ver specs/008-pago-mercado-pago/quickstart.md).");
}
