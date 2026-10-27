using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Verificaciones;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Verificaciones;

public class VerificacionEmailRepository : IVerificacionEmailRepository
{
    private readonly CinemaDbContext _dbContext;

    public VerificacionEmailRepository(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task BloquearPorEmail(EmailComprador email, CancellationToken cancellationToken = default)
    {
        // Bloqueo asesor a nivel de transacción: no exige que exista una fila y se libera solo al confirmar o deshacer
        // (ver research.md, Decisión 5).
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({email.Valor}, 0))", cancellationToken);
    }

    public async Task<VerificacionEmail?> ObtenerVigente(EmailComprador email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.VerificacionesEmail
            .FirstOrDefaultAsync(v => v.Email == email && v.Estado == EstadoVerificacion.Vigente, cancellationToken);
    }

    public async Task<int> ContarDesde(EmailComprador email, DateTimeOffset desde, CancellationToken cancellationToken = default)
    {
        return await _dbContext.VerificacionesEmail
            .CountAsync(v => v.Email == email && v.CreadaEn >= desde, cancellationToken);
    }

    public async Task Agregar(VerificacionEmail verificacion, CancellationToken cancellationToken = default)
    {
        // Primero se persisten los cambios pendientes (la invalidación de la vigente anterior): si EF mandara el INSERT
        // antes del UPDATE, el índice único parcial vería dos vigentes a la vez.
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _dbContext.VerificacionesEmail.AddAsync(verificacion, cancellationToken);
    }
}
