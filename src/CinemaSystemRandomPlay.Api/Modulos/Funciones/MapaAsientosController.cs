using CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones;

[ApiController]
[Route("api/funciones/{funcionId:guid}/asientos")]
[Tags("Funciones")]
public class MapaAsientosController : ControllerBase
{
    private readonly ObtenerMapaAsientosQueryHandler _obtenerMapaAsientosQueryHandler;

    public MapaAsientosController(ObtenerMapaAsientosQueryHandler obtenerMapaAsientosQueryHandler)
    {
        _obtenerMapaAsientosQueryHandler = obtenerMapaAsientosQueryHandler;
    }

    /// <summary>Obtiene el mapa de asientos de una función: filas y asientos disponibles.</summary>
    /// <remarks>Un asiento retenido por un carrito activo figura como no disponible.</remarks>
    /// <param name="funcionId">Función.</param>
    /// <response code="200">El mapa de asientos con el precio de la entrada.</response>
    /// <response code="404">La función no existe.</response>
    /// <response code="410">La función ya comenzó.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<MapaAsientosResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public async Task<ActionResult<MapaAsientosResponse>> ObtenerMapa(Guid funcionId, CancellationToken cancellationToken)
    {
        var resultado = await _obtenerMapaAsientosQueryHandler.Handle(new ObtenerMapaAsientosQuery(funcionId), cancellationToken);

        return resultado.Estado switch
        {
            EstadoMapaAsientos.Ok => Ok(MapaAsientosResponse.DesdeDto(resultado.Mapa!)),
            EstadoMapaAsientos.FuncionNoDisponible => StatusCode(StatusCodes.Status410Gone),
            _ => NotFound()
        };
    }
}
