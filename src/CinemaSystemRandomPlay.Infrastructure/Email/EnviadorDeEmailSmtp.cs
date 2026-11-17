using CinemaSystemRandomPlay.Application.Compartido.Ports;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CinemaSystemRandomPlay.Infrastructure.Email;

/// <summary>
/// Envío por SMTP con MailKit (specs/008-pago-mercado-pago/research.md, Decisión 11). Sirve para Mailpit en desarrollo y para
/// cualquier proveedor que exponga SMTP (por ejemplo, Resend). Se invoca solo desde el Outbox, fuera de toda transacción.
/// </summary>
public class EnviadorDeEmailSmtp : IEnviadorDeEmail
{
    private readonly OpcionesEmail _opciones;

    public EnviadorDeEmailSmtp(OpcionesEmail opciones)
    {
        _opciones = opciones;
    }

    public async Task Enviar(MensajeDeEmail mensaje, CancellationToken cancellationToken = default)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_opciones.NombreRemitente, _opciones.Remitente));
        email.To.Add(MailboxAddress.Parse(mensaje.Destinatario.Valor));
        email.Subject = mensaje.Asunto;
        email.Body = new BodyBuilder { TextBody = mensaje.Cuerpo, HtmlBody = mensaje.CuerpoHtml }.ToMessageBody();

        var smtp = _opciones.Smtp;
        var seguridad = !smtp.UsarTls
            ? SecureSocketOptions.None
            : smtp.Puerto == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        using var cliente = new SmtpClient();
        await cliente.ConnectAsync(smtp.Host, smtp.Puerto, seguridad, cancellationToken);

        if (!string.IsNullOrEmpty(smtp.Usuario))
            await cliente.AuthenticateAsync(smtp.Usuario, smtp.Clave ?? string.Empty, cancellationToken);

        await cliente.SendAsync(new SmtpSendRequest(email), cancellationToken);
        await cliente.DisconnectAsync(true, cancellationToken);
    }
}
