using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Carritos.Eventos;

public sealed record CarritoCreado(Guid CarritoId, Guid FuncionId, IReadOnlyList<Asiento> Asientos, DateTimeOffset VenceEn, DateTimeOffset OcurridoEn, Dinero PrecioUnitario, Dinero Total);
