using CinemaSystemRandomPlay.Domain.Funciones;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

public class FuncionRepository : IFuncionRepository
{
    private readonly CinemaDbContext _dbContext;

    public FuncionRepository(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Sucursal>> ListarSucursalesConFuncionesDisponibles(Guid peliculaId, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var hoyFin = FinDelDia(hoy);

        var sucursalIds = await _dbContext.Funciones
            .Where(f => f.PeliculaId == peliculaId && f.FechaHoraInicio >= ahora && f.FechaHoraInicio <= hoyFin)
            .Join(_dbContext.Salas, f => f.SalaId, s => s.Id, (f, s) => s.SucursalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (sucursalIds.Count == 0)
            return Array.Empty<Sucursal>();

        return await _dbContext.Sucursales
            .Where(s => sucursalIds.Contains(s.Id))
            .OrderBy(s => s.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExisteFuncionHoy(Guid peliculaId, Guid sucursalId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var hoyInicio = InicioDelDia(hoy);
        var hoyFin = FinDelDia(hoy);

        return await _dbContext.Funciones
            .Where(f => f.PeliculaId == peliculaId && f.FechaHoraInicio >= hoyInicio && f.FechaHoraInicio <= hoyFin)
            .Join(_dbContext.Salas.Where(s => s.SucursalId == sucursalId), f => f.SalaId, s => s.Id, (f, _) => f.Id)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Funcion>> ListarHorariosDisponibles(Guid peliculaId, Guid sucursalId, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var hoyFin = FinDelDia(hoy);

        return await _dbContext.Funciones
            .Include(f => f.Sala)
            .Where(f => f.PeliculaId == peliculaId
                && f.Sala!.SucursalId == sucursalId
                && f.FechaHoraInicio >= ahora
                && f.FechaHoraInicio <= hoyFin)
            .OrderBy(f => f.FechaHoraInicio)
            .ToListAsync(cancellationToken);
    }

    private static DateTimeOffset InicioDelDia(DateOnly dia)
    {
        var fecha = dia.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(fecha, TimeZoneInfo.Local.GetUtcOffset(fecha));
    }

    private static DateTimeOffset FinDelDia(DateOnly dia)
    {
        var fecha = dia.ToDateTime(TimeOnly.MaxValue);
        return new DateTimeOffset(fecha, TimeZoneInfo.Local.GetUtcOffset(fecha));
    }
}
