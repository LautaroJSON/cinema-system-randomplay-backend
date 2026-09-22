using CinemaSystemRandomPlay.Application.Catalogo.Dtos;
using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Domain.Catalogo;

namespace CinemaSystemRandomPlay.Application.Catalogo.Queries;

public record ObtenerDetallePeliculaQuery(Guid Id);

public class ObtenerDetallePeliculaQueryHandler
{
    private readonly IPeliculaRepository _peliculaRepository;
    private readonly IFuncionAvailabilityChecker _funcionAvailabilityChecker;

    public ObtenerDetallePeliculaQueryHandler(IPeliculaRepository peliculaRepository, IFuncionAvailabilityChecker funcionAvailabilityChecker)
    {
        _peliculaRepository = peliculaRepository;
        _funcionAvailabilityChecker = funcionAvailabilityChecker;
    }

    public async Task<PeliculaDetalleDto?> Handle(ObtenerDetallePeliculaQuery query, CancellationToken cancellationToken = default)
    {
        var pelicula = await _peliculaRepository.ObtenerPorId(query.Id, cancellationToken);

        if (pelicula is null || !pelicula.Activa)
            return null;

        var tieneFunciones = await _funcionAvailabilityChecker.TieneFuncionesProgramadas(pelicula.Id, cancellationToken);

        return new PeliculaDetalleDto(
            pelicula.Id,
            pelicula.Titulo,
            pelicula.DuracionMinutos,
            pelicula.Clasificacion.ToString(),
            pelicula.Sinopsis,
            SinFuncionesDisponibles: !tieneFunciones);
    }
}
