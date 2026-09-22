namespace CinemaSystemRandomPlay.Application.Catalogo.Ports;

public interface IFuncionAvailabilityChecker
{
    Task<bool> TieneFuncionesProgramadas(Guid peliculaId, CancellationToken cancellationToken = default);
}
