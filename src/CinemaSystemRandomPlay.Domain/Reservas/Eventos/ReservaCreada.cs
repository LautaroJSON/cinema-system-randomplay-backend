using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Reservas.Eventos;

public sealed record ReservaCreada(Guid ReservaId, Guid FuncionId, IReadOnlyList<Asiento> Asientos, DateTimeOffset VenceEn, DateTimeOffset OcurridoEn, Dinero PrecioUnitario, Dinero Total);
