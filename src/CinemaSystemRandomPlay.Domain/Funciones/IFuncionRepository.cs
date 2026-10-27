namespace CinemaSystemRandomPlay.Domain.Funciones;

public interface IFuncionRepository
{
    Task<IReadOnlyList<Sucursal>> ListarSucursalesConFuncionesDisponibles(Guid peliculaId, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancellationToken = default);

    Task<bool> ExisteFuncionHoy(Guid peliculaId, Guid sucursalId, DateOnly hoy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Funcion>> ListarHorariosDisponibles(Guid peliculaId, Guid sucursalId, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancellationToken = default);

    Task<Funcion?> ObtenerConSala(Guid funcionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual que <see cref="ObtenerConSala"/>, pero toma un bloqueo exclusivo sobre la fila de la Funcion
    /// hasta el fin de la transacción en curso. Requiere una transacción activa.
    /// </summary>
    Task<Funcion?> ObtenerConSalaBloqueando(Guid funcionId, CancellationToken cancellationToken = default);
}
