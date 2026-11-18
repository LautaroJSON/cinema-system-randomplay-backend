using CinemaSystemRandomPlay.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CinemaSystemRandomPlay.Api.Compartido;

/// <summary>
/// Chequeo de <c>/health</c>: la API está sana si llega a la base. Consultar la base en cada ping mantiene despiertos tanto el
/// servicio del hosting como la base gratuita, que se pausa por inactividad.
/// </summary>
public class BaseDeDatosDisponible : IHealthCheck
{
    private readonly CinemaDbContext _dbContext;

    public BaseDeDatosDisponible(CinemaDbContext dbContext) => _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("No se pudo conectar a la base de datos.");
}
