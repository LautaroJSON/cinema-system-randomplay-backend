using CinemaSystemRandomPlay.Application.Funciones.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Carritos;

/// <summary>Los asientos ocupados de una función son los de sus carritos activos y no vencidos.</summary>
public class CarritosRetienenAsientos : IOcupacionAsientos
{
    private readonly CinemaDbContext _dbContext;
    private readonly IReloj _reloj;

    public CarritosRetienenAsientos(CinemaDbContext dbContext, IReloj reloj)
    {
        _dbContext = dbContext;
        _reloj = reloj;
    }

    public async Task<IReadOnlySet<Asiento>> ObtenerOcupados(Guid funcionId, CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.Ahora;

        var asientos = await _dbContext.Carritos
            .AsNoTracking()
            .Where(c => c.FuncionId == funcionId && c.Estado == EstadoCarrito.Activo && c.VenceEn > ahora)
            .SelectMany(c => c.Asientos)
            .ToListAsync(cancellationToken);

        return asientos.ToHashSet();
    }
}
