namespace CinemaSystemRandomPlay.Api.Modulos.Reservas.Contracts;

public record CrearReservaRequest(IReadOnlyList<AsientoRequest>? Asientos);

public record AsientoRequest(string? Fila, int Numero);
