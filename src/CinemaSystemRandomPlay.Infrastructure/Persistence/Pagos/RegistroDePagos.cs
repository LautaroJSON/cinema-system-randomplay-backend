using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Pagos;

/// <summary>No guarda: los cambios se confirman con la unidad de trabajo en curso.</summary>
public class RegistroDePagos : IRegistroDePagos
{
    private readonly CinemaDbContext _dbContext;

    public RegistroDePagos(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> YaProcesado(string pagoId, CancellationToken cancellationToken = default) =>
        _dbContext.PagosProcesados.AnyAsync(p => p.PagoId == pagoId, cancellationToken);

    public async Task Registrar(PagoProcesado pago, CancellationToken cancellationToken = default) =>
        await _dbContext.PagosProcesados.AddAsync(pago, cancellationToken);

    public async Task AgregarDevolucion(Devolucion devolucion, CancellationToken cancellationToken = default) =>
        await _dbContext.Devoluciones.AddAsync(devolucion, cancellationToken);

    public Task<Devolucion?> ObtenerDevolucion(Guid devolucionId, CancellationToken cancellationToken = default) =>
        _dbContext.Devoluciones.FirstOrDefaultAsync(d => d.Id == devolucionId, cancellationToken);
}
