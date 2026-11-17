namespace CinemaSystemRandomPlay.Domain.Carritos.Eventos;

public sealed record PagoIniciado(Guid CarritoId, Guid FuncionId, DateTimeOffset VenceEn, DateTimeOffset OcurridoEn);
