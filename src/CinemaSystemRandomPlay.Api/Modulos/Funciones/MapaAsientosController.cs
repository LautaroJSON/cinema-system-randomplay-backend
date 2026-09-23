using CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones;

[ApiController]
[Route("api/funciones/{funcionId:guid}/asientos")]
public class MapaAsientosController : ControllerBase
{
    private readonly ObtenerMapaAsientosQueryHandler _obtenerMapaAsientosQueryHandler;

    public MapaAsientosController(ObtenerMapaAsientosQueryHandler obtenerMapaAsientosQueryHandler)
    {
        _obtenerMapaAsientosQueryHandler = obtenerMapaAsientosQueryHandler;
    }

    [HttpGet]
    [AllowAnonymous]
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
