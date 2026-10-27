using CinemaSystemRandomPlay.Domain.Reservas;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Reservas;

public class ReservaRepository : IReservaRepository
{
    private readonly CinemaDbContext _dbContext;

    public ReservaRepository(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Reserva>> ListarVigentesDeFuncion(Guid funcionId, DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservas
            .Where(r => r.FuncionId == funcionId && r.Estado == EstadoReserva.PendienteDePago && r.VenceEn > ahora)
            .ToListAsync(cancellationToken);
    }

    public async Task<Reserva?> ObtenerPorId(Guid reservaId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservas.FirstOrDefaultAsync(r => r.Id == reservaId, cancellationToken);
    }

    public async Task Agregar(Reserva reserva, CancellationToken cancellationToken = default)
    {
        await _dbContext.Reservas.AddAsync(reserva, cancellationToken);
    }
}
