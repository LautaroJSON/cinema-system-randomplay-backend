namespace CinemaSystemRandomPlay.Domain.Reservas;

public interface IReservaRepository
{
    Task<IReadOnlyList<Reserva>> ListarVigentesDeFuncion(Guid funcionId, DateTimeOffset ahora, CancellationToken cancellationToken = default);

    Task<Reserva?> ObtenerPorId(Guid reservaId, CancellationToken cancellationToken = default);

    Task Agregar(Reserva reserva, CancellationToken cancellationToken = default);
}
