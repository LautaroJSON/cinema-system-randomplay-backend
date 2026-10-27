using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Infrastructure.Persistence;

namespace CinemaSystemRandomPlay.Infrastructure.Compartido;

public class UnidadDeTrabajoEf : IUnidadDeTrabajo
{
    private readonly CinemaDbContext _dbContext;

    public UnidadDeTrabajoEf(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> EjecutarEnTransaccion<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken cancellationToken = default)
    {
        await using var transaccion = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var resultado = await operacion(cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return resultado;
    }
}
