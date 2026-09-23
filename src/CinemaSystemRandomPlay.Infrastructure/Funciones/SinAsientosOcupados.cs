using CinemaSystemRandomPlay.Application.Funciones.Ports;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Infrastructure.Funciones;

/// <summary>
/// Stub temporal: hasta que exista la feature de Reservas, ningún asiento está ocupado en ninguna
/// función (ver specs/003-mapa-asientos-funcion/research.md, Decisión 3).
/// </summary>
public class SinAsientosOcupados : IOcupacionAsientos
{
    public Task<IReadOnlySet<Asiento>> ObtenerOcupados(Guid funcionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<Asiento>>(new HashSet<Asiento>());
}
