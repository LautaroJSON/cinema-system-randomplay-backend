namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Outbox;

/// <summary>
/// Evento de dominio pendiente de publicar. Se escribe en la misma transacción que el cambio de negocio.
/// Lo consume <c>ProcesadorDeOutbox</c> fuera de la transacción, con reintentos espaciados (<see cref="ProximoIntentoEn"/>).
/// </summary>
public class OutboxMensaje
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OcurridoEn { get; set; }
    public DateTimeOffset? ProcesadoEn { get; set; }

    public int Intentos { get; set; }

    /// <summary>No se toma antes de este momento: sirve de espera entre reintentos y de lease mientras se procesa.</summary>
    public DateTimeOffset? ProximoIntentoEn { get; set; }

    public string? UltimoError { get; set; }

    /// <summary>Se agotaron los reintentos; queda registrado y no se vuelve a tomar.</summary>
    public DateTimeOffset? FallidoEn { get; set; }
}
