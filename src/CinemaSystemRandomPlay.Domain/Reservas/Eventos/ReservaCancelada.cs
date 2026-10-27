using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Reservas.Eventos;

public sealed record ReservaCancelada(Guid ReservaId, Guid FuncionId, IReadOnlyList<Asiento> AsientosLiberados, DateTimeOffset OcurridoEn);
