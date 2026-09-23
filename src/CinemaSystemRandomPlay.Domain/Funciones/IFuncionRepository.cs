namespace CinemaSystemRandomPlay.Domain.Funciones;

public interface IFuncionRepository
{
    Task<IReadOnlyList<Sucursal>> ListarSucursalesConFuncionesDisponibles(Guid peliculaId, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancellationToken = default);

    Task<bool> ExisteFuncionHoy(Guid peliculaId, Guid sucursalId, DateOnly hoy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Funcion>> ListarHorariosDisponibles(Guid peliculaId, Guid sucursalId, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancellationToken = default);

    Task<Funcion?> ObtenerConSala(Guid funcionId, CancellationToken cancellationToken = default);
}
