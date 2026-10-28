using CinemaSystemRandomPlay.Api.Modulos.Catalogo.Contracts;
using CinemaSystemRandomPlay.Application.Catalogo.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Catalogo;

[ApiController]
[Route("api/catalogo/peliculas")]
[Tags("Catálogo")]
public class PeliculasController : ControllerBase
{
    private readonly ListarPeliculasQueryHandler _listarPeliculasQueryHandler;
    private readonly ObtenerDetallePeliculaQueryHandler _obtenerDetallePeliculaQueryHandler;

    public PeliculasController(
        ListarPeliculasQueryHandler listarPeliculasQueryHandler,
        ObtenerDetallePeliculaQueryHandler obtenerDetallePeliculaQueryHandler)
    {
        _listarPeliculasQueryHandler = listarPeliculasQueryHandler;
        _obtenerDetallePeliculaQueryHandler = obtenerDetallePeliculaQueryHandler;
    }

    /// <summary>Lista las películas activas del catálogo.</summary>
    /// <param name="titulo">Filtra por título (coincidencia parcial, sin distinguir mayúsculas). Opcional.</param>
    /// <response code="200">Las películas; lista vacía si ninguna coincide.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PeliculaListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PeliculaListItemResponse>>> Listar([FromQuery] string? titulo, CancellationToken cancellationToken)
    {
        var peliculas = await _listarPeliculasQueryHandler.Handle(new ListarPeliculasQuery(titulo), cancellationToken);
        return Ok(peliculas.Select(PeliculaListItemResponse.DesdeDto).ToList());
    }

    /// <summary>Obtiene el detalle de una película activa.</summary>
    /// <param name="id">Película.</param>
    /// <response code="200">El detalle de la película.</response>
    /// <response code="404">La película no existe o no está activa.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<PeliculaDetalleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PeliculaDetalleResponse>> ObtenerDetalle(Guid id, CancellationToken cancellationToken)
    {
        var pelicula = await _obtenerDetallePeliculaQueryHandler.Handle(new ObtenerDetallePeliculaQuery(id), cancellationToken);

        if (pelicula is null)
            return NotFound();

        return Ok(PeliculaDetalleResponse.DesdeDto(pelicula));
    }
}
