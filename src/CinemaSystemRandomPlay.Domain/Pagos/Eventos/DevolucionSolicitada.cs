namespace CinemaSystemRandomPlay.Domain.Pagos.Eventos;

/// <summary>Dispara la devolución en la pasarela (Outbox).</summary>
public sealed record DevolucionSolicitada(Guid DevolucionId, string PagoId, Guid CarritoId, MotivoDevolucion Motivo, DateTimeOffset OcurridoEn);
