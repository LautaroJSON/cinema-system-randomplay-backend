using CinemaSystemRandomPlay.Application.Funciones.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Reservas;

/// <summary>
/// Los asientos ocupados de una función son los de sus reservas pendientes de pago y no vencidas.
/// Reemplaza al stub de la feature 003.
/// </summary>
public class ReservasRetienenAsientos : IOcupacionAsientos
{
    private readonly CinemaDbContext _dbContext;
    private readonly IReloj _reloj;

    public ReservasRetienenAsientos(CinemaDbContext dbContext, IReloj reloj)
    {
        _dbContext = dbContext;
        _reloj = reloj;
    }

    public async Task<IReadOnlySet<Asiento>> ObtenerOcupados(Guid funcionId, CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.Ahora;

        var asientos = await _dbContext.Reservas
            .AsNoTracking()
            .Where(r => r.FuncionId == funcionId && r.Estado == EstadoReserva.PendienteDePago && r.VenceEn > ahora)
            .SelectMany(r => r.Asientos)
            .ToListAsync(cancellationToken);

        return asientos.ToHashSet();
    }
}
