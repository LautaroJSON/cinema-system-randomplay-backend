using CinemaSystemRandomPlay.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Catalogo;

public class PeliculaRepository : IPeliculaRepository
{
    private readonly CinemaDbContext _dbContext;

    public PeliculaRepository(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Pelicula>> ListarActivas(string? titulo = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Peliculas.Where(p => p.Activa);

        if (!string.IsNullOrWhiteSpace(titulo))
        {
            query = query.Where(p => EF.Functions.ILike(p.Titulo, $"%{titulo}%"));
        }

        return await query
            .OrderBy(p => p.Titulo)
            .ToListAsync(cancellationToken);
    }

    public async Task<Pelicula?> ObtenerPorId(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Peliculas
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}
