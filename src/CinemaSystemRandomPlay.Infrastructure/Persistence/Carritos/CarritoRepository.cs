using CinemaSystemRandomPlay.Domain.Carritos;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Carritos;

public class CarritoRepository : ICarritoRepository
{
    private readonly CinemaDbContext _dbContext;

    public CarritoRepository(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Carrito>> ListarActivosDeFuncion(Guid funcionId, DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Carritos
            .Where(c => c.FuncionId == funcionId && c.Estado == EstadoCarrito.Activo && c.VenceEn > ahora)
            .ToListAsync(cancellationToken);
    }

    public async Task<Carrito?> ObtenerPorId(Guid carritoId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Carritos.FirstOrDefaultAsync(c => c.Id == carritoId, cancellationToken);
    }

    public async Task<Guid?> ObtenerFuncionId(Guid carritoId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Carritos
            .AsNoTracking()
            .Where(c => c.Id == carritoId)
            .Select(c => (Guid?)c.FuncionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task Agregar(Carrito carrito, CancellationToken cancellationToken = default)
    {
        await _dbContext.Carritos.AddAsync(carrito, cancellationToken);
    }
}
