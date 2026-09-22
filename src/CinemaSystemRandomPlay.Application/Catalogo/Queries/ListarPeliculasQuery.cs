using CinemaSystemRandomPlay.Application.Catalogo.Dtos;
using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Domain.Catalogo;

namespace CinemaSystemRandomPlay.Application.Catalogo.Queries;

public record ListarPeliculasQuery(string? Titulo = null);

public class ListarPeliculasQueryHandler
{
    private readonly IPeliculaRepository _peliculaRepository;
    private readonly IFuncionAvailabilityChecker _funcionAvailabilityChecker;

    public ListarPeliculasQueryHandler(IPeliculaRepository peliculaRepository, IFuncionAvailabilityChecker funcionAvailabilityChecker)
    {
        _peliculaRepository = peliculaRepository;
        _funcionAvailabilityChecker = funcionAvailabilityChecker;
    }

    public async Task<IReadOnlyList<PeliculaListItemDto>> Handle(ListarPeliculasQuery query, CancellationToken cancellationToken = default)
    {
        var peliculas = await _peliculaRepository.ListarActivas(query.Titulo, cancellationToken);

        var resultado = new List<PeliculaListItemDto>(peliculas.Count);
        foreach (var pelicula in peliculas)
        {
            var tieneFunciones = await _funcionAvailabilityChecker.TieneFuncionesProgramadas(pelicula.Id, cancellationToken);
            resultado.Add(new PeliculaListItemDto(
                pelicula.Id,
                pelicula.Titulo,
                pelicula.DuracionMinutos,
                pelicula.Clasificacion.ToString(),
                SinFuncionesDisponibles: !tieneFunciones));
        }

        return resultado;
    }
}
