using CinemaSystemRandomPlay.Application.Funciones.Dtos;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Funciones.Queries;

public record ListarSucursalesConFuncionesHoyQuery(Guid PeliculaId);

public class ListarSucursalesConFuncionesHoyQueryHandler
{
    private readonly IPeliculaRepository _peliculaRepository;
    private readonly IFuncionRepository _funcionRepository;
    private readonly IReloj _reloj;

    public ListarSucursalesConFuncionesHoyQueryHandler(IPeliculaRepository peliculaRepository, IFuncionRepository funcionRepository, IReloj reloj)
    {
        _peliculaRepository = peliculaRepository;
        _funcionRepository = funcionRepository;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<SucursalConFuncionesHoyDto>?> Handle(ListarSucursalesConFuncionesHoyQuery query, CancellationToken cancellationToken = default)
    {
        var pelicula = await _peliculaRepository.ObtenerPorId(query.PeliculaId, cancellationToken);

        if (pelicula is null || !pelicula.Activa)
            return null;

        var ahora = _reloj.Ahora;
        var hoy = DateOnly.FromDateTime(ahora.Date);

        var sucursales = await _funcionRepository.ListarSucursalesConFuncionesDisponibles(pelicula.Id, hoy, ahora, cancellationToken);

        return sucursales
            .Select(s => new SucursalConFuncionesHoyDto(s.Id, s.Nombre))
            .ToList();
    }
}
