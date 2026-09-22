using CinemaSystemRandomPlay.Application.Funciones.Dtos;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Funciones.Queries;

public record ListarHorariosDisponiblesHoyQuery(Guid PeliculaId, Guid SucursalId);

public class ListarHorariosDisponiblesHoyQueryHandler
{
    private readonly IPeliculaRepository _peliculaRepository;
    private readonly IFuncionRepository _funcionRepository;
    private readonly IReloj _reloj;

    public ListarHorariosDisponiblesHoyQueryHandler(IPeliculaRepository peliculaRepository, IFuncionRepository funcionRepository, IReloj reloj)
    {
        _peliculaRepository = peliculaRepository;
        _funcionRepository = funcionRepository;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<HorarioFuncionDto>?> Handle(ListarHorariosDisponiblesHoyQuery query, CancellationToken cancellationToken = default)
    {
        var pelicula = await _peliculaRepository.ObtenerPorId(query.PeliculaId, cancellationToken);

        if (pelicula is null || !pelicula.Activa)
            return null;

        var ahora = _reloj.Ahora;
        var hoy = DateOnly.FromDateTime(ahora.Date);

        var esSucursalValida = await _funcionRepository.ExisteFuncionHoy(pelicula.Id, query.SucursalId, hoy, cancellationToken);

        if (!esSucursalValida)
            return null;

        var funciones = await _funcionRepository.ListarHorariosDisponibles(pelicula.Id, query.SucursalId, hoy, ahora, cancellationToken);

        return funciones
            .Select(f => new HorarioFuncionDto(f.Id, f.FechaHoraInicio, f.SalaId, f.Sala!.Nombre))
            .ToList();
    }
}
