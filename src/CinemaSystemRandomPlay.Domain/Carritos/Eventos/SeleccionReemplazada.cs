using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Carritos.Eventos;

public sealed record SeleccionReemplazada(Guid CarritoId, Guid FuncionId, IReadOnlyList<Asiento> AsientosLiberados, IReadOnlyList<Asiento> AsientosRetenidos, DateTimeOffset VenceEn, DateTimeOffset OcurridoEn);
