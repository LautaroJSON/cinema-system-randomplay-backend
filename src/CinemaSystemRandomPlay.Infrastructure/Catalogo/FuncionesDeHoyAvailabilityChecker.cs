using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Catalogo;

/// <summary>
/// Una película tiene funciones disponibles si le queda alguna hoy que todavía no empezó: el mismo criterio que usan
/// "sucursales hoy" y "horarios hoy", así la marca del catálogo coincide con lo que el comprador puede elegir.
/// </summary>
public class FuncionesDeHoyAvailabilityChecker : IFuncionAvailabilityChecker
{
    private readonly CinemaDbContext _dbContext;
    private readonly IReloj _reloj;

    public FuncionesDeHoyAvailabilityChecker(CinemaDbContext dbContext, IReloj reloj)
    {
        _dbContext = dbContext;
        _reloj = reloj;
    }

    public async Task<bool> TieneFuncionesProgramadas(Guid peliculaId, CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.Ahora;
        var hoyFin = FuncionRepository.FinDelDia(DateOnly.FromDateTime(ahora.Date));

        return await _dbContext.Funciones
            .AnyAsync(f => f.PeliculaId == peliculaId && f.FechaHoraInicio >= ahora && f.FechaHoraInicio <= hoyFin, cancellationToken);
    }
}
