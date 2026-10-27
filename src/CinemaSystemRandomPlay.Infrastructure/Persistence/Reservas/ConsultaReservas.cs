using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Reservas;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Reservas;

public class ConsultaReservas : IConsultaReservas
{
    private readonly CinemaDbContext _dbContext;

    public ConsultaReservas(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ReservaLecturaDto>> ListarDeCliente(string clienteId, CancellationToken cancellationToken = default)
    {
        var filas = await ConContexto(clienteId).ToListAsync(cancellationToken);

        return (await ConAsientos(filas, cancellationToken))
            .OrderByDescending(r => r.CreadaEn)
            .ToList();
    }

    public async Task<ReservaLecturaDto?> ObtenerDeCliente(Guid reservaId, string clienteId, CancellationToken cancellationToken = default)
    {
        var filas = await ConContexto(clienteId, reservaId).ToListAsync(cancellationToken);

        return (await ConAsientos(filas, cancellationToken)).SingleOrDefault();
    }

    private IQueryable<FilaReserva> ConContexto(string clienteId, Guid? reservaId = null)
    {
        return from r in _dbContext.Reservas.AsNoTracking()
               where r.ClienteId == clienteId && (reservaId == null || r.Id == reservaId)
               join f in _dbContext.Funciones on r.FuncionId equals f.Id
               join p in _dbContext.Peliculas on f.PeliculaId equals p.Id
               join s in _dbContext.Salas on f.SalaId equals s.Id
               join su in _dbContext.Sucursales on s.SucursalId equals su.Id
               select new FilaReserva(r.Id, r.Estado, r.CreadaEn, r.VenceEn, r.CanceladaEn, r.FuncionId, f.FechaHoraInicio,
                   p.Id, p.Titulo, su.Id, su.Nombre, s.Id, s.Nombre, r.PrecioUnitario);
    }

    private async Task<IReadOnlyList<ReservaLecturaDto>> ConAsientos(List<FilaReserva> filas, CancellationToken cancellationToken)
    {
        if (filas.Count == 0)
            return Array.Empty<ReservaLecturaDto>();

        var ids = filas.Select(x => x.Id).ToList();

        var asientos = await _dbContext.Reservas
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .SelectMany(r => r.Asientos, (r, a) => new { ReservaId = r.Id, Asiento = a })
            .ToListAsync(cancellationToken);

        var porReserva = asientos.ToLookup(x => x.ReservaId, x => x.Asiento);

        return filas
            .Select(x => new ReservaLecturaDto(x.Id, x.Estado, x.CreadaEn, x.VenceEn, x.CanceladaEn, porReserva[x.Id].ToList(),
                x.FuncionId, x.HoraInicio, x.PeliculaId, x.PeliculaTitulo, x.SucursalId, x.SucursalNombre, x.SalaId, x.SalaNombre, x.PrecioUnitario))
            .ToList();
    }

    private sealed record FilaReserva(
        Guid Id,
        EstadoReserva Estado,
        DateTimeOffset CreadaEn,
        DateTimeOffset VenceEn,
        DateTimeOffset? CanceladaEn,
        Guid FuncionId,
        DateTimeOffset HoraInicio,
        Guid PeliculaId,
        string PeliculaTitulo,
        Guid SucursalId,
        string SucursalNombre,
        Guid SalaId,
        string SalaNombre,
        Dinero PrecioUnitario);
}
