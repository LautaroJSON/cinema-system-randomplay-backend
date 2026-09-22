using CinemaSystemRandomPlay.Application.Catalogo.Ports;

namespace CinemaSystemRandomPlay.Infrastructure.Catalogo;

/// <summary>
/// Stub temporal: hasta que exista la feature "Funciones", ninguna película tiene funciones
/// programadas (ver specs/001-catalogo-peliculas/research.md, Decisión 1).
/// </summary>
public class NingunaFuncionDisponibleChecker : IFuncionAvailabilityChecker
{
    public Task<bool> TieneFuncionesProgramadas(Guid peliculaId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
