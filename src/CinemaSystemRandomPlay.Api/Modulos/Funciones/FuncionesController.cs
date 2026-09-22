using CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones;

[ApiController]
[Route("api/funciones/peliculas/{peliculaId:guid}")]
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

    [HttpGet("sucursales-hoy")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SucursalConFuncionesHoyResponse>>> ListarSucursalesConFuncionesHoy(Guid peliculaId, CancellationToken cancellationToken)
    {
        var sucursales = await _listarSucursalesConFuncionesHoyQueryHandler.Handle(new ListarSucursalesConFuncionesHoyQuery(peliculaId), cancellationToken);

        if (sucursales is null)
            return NotFound();

        return Ok(sucursales.Select(SucursalConFuncionesHoyResponse.DesdeDto).ToList());
    }

    [HttpGet("sucursales/{sucursalId:guid}/horarios-hoy")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<HorarioFuncionResponse>>> ListarHorariosDisponiblesHoy(Guid peliculaId, Guid sucursalId, CancellationToken cancellationToken)
    {
        var horarios = await _listarHorariosDisponiblesHoyQueryHandler.Handle(new ListarHorariosDisponiblesHoyQuery(peliculaId, sucursalId), cancellationToken);

        if (horarios is null)
            return NotFound();

        return Ok(horarios.Select(HorarioFuncionResponse.DesdeDto).ToList());
    }
}
