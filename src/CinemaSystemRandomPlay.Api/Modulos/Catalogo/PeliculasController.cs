using CinemaSystemRandomPlay.Api.Modulos.Catalogo.Contracts;
using CinemaSystemRandomPlay.Application.Catalogo.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Catalogo;

[ApiController]
[Route("api/catalogo/peliculas")]
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

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PeliculaListItemResponse>>> Listar([FromQuery] string? titulo, CancellationToken cancellationToken)
    {
        var peliculas = await _listarPeliculasQueryHandler.Handle(new ListarPeliculasQuery(titulo), cancellationToken);
        return Ok(peliculas.Select(PeliculaListItemResponse.DesdeDto).ToList());
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PeliculaDetalleResponse>> ObtenerDetalle(Guid id, CancellationToken cancellationToken)
    {
        var pelicula = await _obtenerDetallePeliculaQueryHandler.Handle(new ObtenerDetallePeliculaQuery(id), cancellationToken);

        if (pelicula is null)
            return NotFound();

        return Ok(PeliculaDetalleResponse.DesdeDto(pelicula));
    }
}
