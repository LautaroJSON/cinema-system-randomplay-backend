using CinemaSystemRandomPlay.Application.Carritos.Ports;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Carritos;

public class ConsultaCarritos : IConsultaCarritos
{
    private readonly CinemaDbContext _dbContext;

    public ConsultaCarritos(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CarritoLecturaDto?> ObtenerPorId(Guid carritoId, CancellationToken cancellationToken = default)
    {
        var fila = await (
            from c in _dbContext.Carritos.AsNoTracking()
            where c.Id == carritoId
            join f in _dbContext.Funciones on c.FuncionId equals f.Id
            join p in _dbContext.Peliculas on f.PeliculaId equals p.Id
            join s in _dbContext.Salas on f.SalaId equals s.Id
            join su in _dbContext.Sucursales on s.SucursalId equals su.Id
            select new
            {
                c.Id, c.Estado, c.CreadoEn, c.VenceEn, c.AbandonadoEn, c.EmailEntrega, c.FuncionId, f.FechaHoraInicio,
                PeliculaId = p.Id, p.Titulo, SucursalId = su.Id, SucursalNombre = su.Nombre, SalaId = s.Id, SalaNombre = s.Nombre,
                c.PrecioUnitario
            }).FirstOrDefaultAsync(cancellationToken);

        if (fila is null)
            return null;

        var asientos = await _dbContext.Carritos
            .AsNoTracking()
            .Where(c => c.Id == carritoId)
            .SelectMany(c => c.Asientos)
            .ToListAsync(cancellationToken);

        return new CarritoLecturaDto(fila.Id, fila.Estado, fila.CreadoEn, fila.VenceEn, fila.AbandonadoEn, fila.EmailEntrega?.Valor,
            asientos, fila.FuncionId, fila.FechaHoraInicio, fila.PeliculaId, fila.Titulo, fila.SucursalId, fila.SucursalNombre,
            fila.SalaId, fila.SalaNombre, fila.PrecioUnitario);
    }
}
