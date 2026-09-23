using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Funciones.Ports;

public interface IOcupacionAsientos
{
    Task<IReadOnlySet<Asiento>> ObtenerOcupados(Guid funcionId, CancellationToken cancellationToken = default);
}
