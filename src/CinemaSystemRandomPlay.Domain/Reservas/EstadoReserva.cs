namespace CinemaSystemRandomPlay.Domain.Reservas;

/// <summary>
/// <see cref="Expirada"/> nunca se guarda: es el estado efectivo de una reserva
/// <see cref="PendienteDePago"/> cuyo vencimiento ya pasó (ver specs/004-reserva-asientos-funcion/research.md, Decisión 3).
/// </summary>
public enum EstadoReserva
{
    PendienteDePago,
    Cancelada,
    Expirada
}
