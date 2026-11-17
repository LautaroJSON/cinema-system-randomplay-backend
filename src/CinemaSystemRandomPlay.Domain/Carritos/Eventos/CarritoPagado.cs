using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Carritos.Eventos;

/// <summary>Dispara el envío del email de entrega (Outbox).</summary>
public sealed record CarritoPagado(Guid CarritoId, Guid FuncionId, string PagoId, IReadOnlyList<Asiento> Asientos, DateTimeOffset OcurridoEn);
