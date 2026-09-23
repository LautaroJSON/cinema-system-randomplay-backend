using CinemaSystemRandomPlay.Application.Funciones.Dtos;
using CinemaSystemRandomPlay.Application.Funciones.Ports;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Funciones.Queries;

public record ObtenerMapaAsientosQuery(Guid FuncionId);

public enum EstadoMapaAsientos
{
    Ok,
    FuncionNoEncontrada,
    FuncionNoDisponible
}

public record ObtenerMapaAsientosResultado(EstadoMapaAsientos Estado, MapaAsientosDto? Mapa)
{
    public static ObtenerMapaAsientosResultado Ok(MapaAsientosDto mapa) => new(EstadoMapaAsientos.Ok, mapa);

    public static ObtenerMapaAsientosResultado FuncionNoEncontrada() => new(EstadoMapaAsientos.FuncionNoEncontrada, null);

    public static ObtenerMapaAsientosResultado FuncionNoDisponible() => new(EstadoMapaAsientos.FuncionNoDisponible, null);
}

public class ObtenerMapaAsientosQueryHandler
{
    private readonly IPeliculaRepository _peliculaRepository;
    private readonly IFuncionRepository _funcionRepository;
    private readonly IReloj _reloj;
    private readonly IOcupacionAsientos _ocupacionAsientos;

    public ObtenerMapaAsientosQueryHandler(IPeliculaRepository peliculaRepository, IFuncionRepository funcionRepository, IReloj reloj, IOcupacionAsientos ocupacionAsientos)
    {
        _peliculaRepository = peliculaRepository;
        _funcionRepository = funcionRepository;
        _reloj = reloj;
        _ocupacionAsientos = ocupacionAsientos;
    }

    public async Task<ObtenerMapaAsientosResultado> Handle(ObtenerMapaAsientosQuery query, CancellationToken cancellationToken = default)
    {
        var funcion = await _funcionRepository.ObtenerConSala(query.FuncionId, cancellationToken);

        if (funcion?.Sala is null)
            return ObtenerMapaAsientosResultado.FuncionNoEncontrada();

        var pelicula = await _peliculaRepository.ObtenerPorId(funcion.PeliculaId, cancellationToken);

        if (pelicula is null || !pelicula.Activa)
            return ObtenerMapaAsientosResultado.FuncionNoEncontrada();

        if (funcion.YaComenzo(_reloj.Ahora))
            return ObtenerMapaAsientosResultado.FuncionNoDisponible();

        var sala = funcion.Sala;

        var ocupados = await _ocupacionAsientos.ObtenerOcupados(funcion.Id, cancellationToken);

        var filas = sala.AsientosDisponibles(ocupados)
            .Select(f => new FilaMapaDto(f.Letra.ToString(), f.CantidadAsientos, f.NumerosDisponibles))
            .ToList();

        var mapa = new MapaAsientosDto(
            funcion.Id,
            funcion.FechaHoraInicio,
            pelicula.Id,
            pelicula.Titulo,
            sala.SucursalId,
            sala.Sucursal?.Nombre ?? string.Empty,
            sala.Id,
            sala.Nombre,
            sala.TotalAsientos,
            filas.Sum(f => f.AsientosDisponibles.Count),
            filas);

        return ObtenerMapaAsientosResultado.Ok(mapa);
    }
}
