namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Outbox;

/// <summary>
/// Evento de dominio pendiente de publicar. Se escribe en la misma transacción que el cambio de negocio.
/// El publicador que lo consume (<see cref="ProcesadoEn"/>) llega con la feature de notificaciones en tiempo real.
/// </summary>
public class OutboxMensaje
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OcurridoEn { get; set; }
    public DateTimeOffset? ProcesadoEn { get; set; }
}
