using CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones;

[ApiController]
[Route("api/funciones/peliculas/{peliculaId:guid}")]
[Tags("Funciones")]
public class FuncionesController : ControllerBase
{
    private readonly ListarSucursalesConFuncionesHoyQueryHandler _listarSucursalesConFuncionesHoyQueryHandler;
    private readonly ListarHorariosDisponiblesHoyQueryHandler _listarHorariosDisponiblesHoyQueryHandler;

    public FuncionesController(
        ListarSucursalesConFuncionesHoyQueryHandler listarSucursalesConFuncionesHoyQueryHandler,
        ListarHorariosDisponiblesHoyQueryHandler listarHorariosDisponiblesHoyQueryHandler)
    {
        _listarSucursalesConFuncionesHoyQueryHandler = listarSucursalesConFuncionesHoyQueryHandler;
        _listarHorariosDisponiblesHoyQueryHandler = listarHorariosDisponiblesHoyQueryHandler;
    }

    /// <summary>Lista las sucursales que tienen hoy funciones disponibles de la película.</summary>
    /// <param name="peliculaId">Película.</param>
    /// <response code="200">Las sucursales ordenadas por nombre; lista vacía si hoy no quedan funciones.</response>
    /// <response code="404">La película no existe o no está activa.</response>
    [HttpGet("sucursales-hoy")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<SucursalConFuncionesHoyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<SucursalConFuncionesHoyResponse>>> ListarSucursalesConFuncionesHoy(Guid peliculaId, CancellationToken cancellationToken)
    {
        var sucursales = await _listarSucursalesConFuncionesHoyQueryHandler.Handle(new ListarSucursalesConFuncionesHoyQuery(peliculaId), cancellationToken);

        if (sucursales is null)
            return NotFound();

        return Ok(sucursales.Select(SucursalConFuncionesHoyResponse.DesdeDto).ToList());
    }

    /// <summary>Lista los horarios de hoy, todavía no comenzados, de la película en una sucursal.</summary>
    /// <param name="peliculaId">Película.</param>
    /// <param name="sucursalId">Sucursal.</param>
    /// <response code="200">Los horarios ordenados por hora de inicio, con sala y precio; lista vacía si no quedan.</response>
    /// <response code="404">La película no existe o no está activa, o la sucursal no tiene funciones de la película hoy.</response>
    [HttpGet("sucursales/{sucursalId:guid}/horarios-hoy")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<HorarioFuncionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<HorarioFuncionResponse>>> ListarHorariosDisponiblesHoy(Guid peliculaId, Guid sucursalId, CancellationToken cancellationToken)
    {
        var horarios = await _listarHorariosDisponiblesHoyQueryHandler.Handle(new ListarHorariosDisponiblesHoyQuery(peliculaId, sucursalId), cancellationToken);

        if (horarios is null)
            return NotFound();

        return Ok(horarios.Select(HorarioFuncionResponse.DesdeDto).ToList());
    }
}
