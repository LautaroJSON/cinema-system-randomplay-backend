namespace CinemaSystemRandomPlay.Domain.Catalogo;

public interface IPeliculaRepository
{
    Task<IReadOnlyList<Pelicula>> ListarActivas(string? titulo = null, CancellationToken cancellationToken = default);

    Task<Pelicula?> ObtenerPorId(Guid id, CancellationToken cancellationToken = default);
}
