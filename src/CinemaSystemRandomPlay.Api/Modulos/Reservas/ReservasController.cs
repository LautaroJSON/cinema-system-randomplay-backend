using CinemaSystemRandomPlay.Api.Autenticacion;
using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Api.Modulos.Reservas.Contracts;
using CinemaSystemRandomPlay.Application.Reservas.Commands;
using CinemaSystemRandomPlay.Application.Reservas.Dtos;
using CinemaSystemRandomPlay.Application.Reservas.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Reservas;

[ApiController]
[Authorize(Policy = Politicas.Customer)]
public class ReservasController : ControllerBase
{
    private readonly ReservarAsientosCommandHandler _reservarAsientosCommandHandler;
    private readonly CancelarReservaCommandHandler _cancelarReservaCommandHandler;
    private readonly ListarMisReservasQueryHandler _listarMisReservasQueryHandler;
    private readonly ObtenerMiReservaQueryHandler _obtenerMiReservaQueryHandler;

    public ReservasController(
        ReservarAsientosCommandHandler reservarAsientosCommandHandler,
        CancelarReservaCommandHandler cancelarReservaCommandHandler,
        ListarMisReservasQueryHandler listarMisReservasQueryHandler,
        ObtenerMiReservaQueryHandler obtenerMiReservaQueryHandler)
    {
        _reservarAsientosCommandHandler = reservarAsientosCommandHandler;
        _cancelarReservaCommandHandler = cancelarReservaCommandHandler;
        _listarMisReservasQueryHandler = listarMisReservasQueryHandler;
        _obtenerMiReservaQueryHandler = obtenerMiReservaQueryHandler;
    }

    [HttpPost("api/funciones/{funcionId:guid}/reservas")]
    public async Task<IActionResult> Reservar(Guid funcionId, [FromBody] CrearReservaRequest request, CancellationToken cancellationToken)
    {
        var clienteId = User.ObtenerClienteId();
        if (clienteId is null)
            return Unauthorized();

        var asientos = request.Asientos?
            .Select(a => new AsientoPedidoDto(a?.Fila ?? string.Empty, a?.Numero ?? 0))
            .ToList();

        var resultado = await _reservarAsientosCommandHandler.Handle(new ReservarAsientosCommand(funcionId, clienteId, asientos), cancellationToken);

        return resultado.Estado switch
        {
            EstadoReservarAsientos.Ok => CreatedAtAction(nameof(ObtenerReserva), new { reservaId = resultado.Reserva!.Id }, ReservaResponse.DesdeDto(resultado.Reserva)),
            EstadoReservarAsientos.PedidoInvalido => ProblemasDeNegocio.Crear(StatusCodes.Status400BadRequest,
                "El pedido de reserva es inválido.", "pedido-invalido",
                new Dictionary<string, object?> { ["errors"] = resultado.Errores }),
            EstadoReservarAsientos.FuncionNoEncontrada => ProblemasDeNegocio.Crear(StatusCodes.Status404NotFound,
                "La función no existe.", "funcion-no-encontrada"),
            EstadoReservarAsientos.FuncionNoDisponible => ProblemasDeNegocio.Crear(StatusCodes.Status410Gone,
                "La función ya comenzó.", "funcion-no-disponible"),
            EstadoReservarAsientos.AsientosInexistentes => ProblemasDeNegocio.Crear(StatusCodes.Status422UnprocessableEntity,
                "Hay asientos que no existen en la sala.", "asientos-inexistentes", Asientos(resultado.Asientos!)),
            EstadoReservarAsientos.AsientosNoDisponibles => ProblemasDeNegocio.Crear(StatusCodes.Status409Conflict,
                "Hay asientos que ya no están disponibles.", "asientos-no-disponibles", Asientos(resultado.Asientos!)),
            _ => ProblemasDeNegocio.Crear(StatusCodes.Status409Conflict,
                "El pedido superaría el máximo de asientos retenidos por Cliente en la función.", "limite-asientos-superado",
                new Dictionary<string, object?> { ["asientosRestantes"] = resultado.AsientosRestantes })
        };
    }

    [HttpGet("api/reservas")]
    public async Task<ActionResult<IReadOnlyList<ReservaResponse>>> ListarMisReservas(CancellationToken cancellationToken)
    {
        var clienteId = User.ObtenerClienteId();
        if (clienteId is null)
            return Unauthorized();

        var reservas = await _listarMisReservasQueryHandler.Handle(new ListarMisReservasQuery(clienteId), cancellationToken);

        return Ok(reservas.Select(ReservaResponse.DesdeDto).ToList());
    }

    [HttpGet("api/reservas/{reservaId:guid}")]
    public async Task<ActionResult<ReservaResponse>> ObtenerReserva(Guid reservaId, CancellationToken cancellationToken)
    {
        var clienteId = User.ObtenerClienteId();
        if (clienteId is null)
            return Unauthorized();

        var reserva = await _obtenerMiReservaQueryHandler.Handle(new ObtenerMiReservaQuery(reservaId, clienteId), cancellationToken);

        return reserva is null ? ReservaNoEncontrada() : Ok(ReservaResponse.DesdeDto(reserva));
    }

    [HttpPost("api/reservas/{reservaId:guid}/cancelacion")]
    public async Task<IActionResult> Cancelar(Guid reservaId, CancellationToken cancellationToken)
    {
        var clienteId = User.ObtenerClienteId();
        if (clienteId is null)
            return Unauthorized();

        var resultado = await _cancelarReservaCommandHandler.Handle(new CancelarReservaCommand(reservaId, clienteId), cancellationToken);

        return resultado.Estado switch
        {
            EstadoCancelarReserva.Ok => Ok(ReservaResponse.DesdeDto(resultado.Reserva!)),
            EstadoCancelarReserva.NoCancelable => ProblemasDeNegocio.Crear(StatusCodes.Status409Conflict,
                "La reserva no se puede cancelar en su estado actual.", "reserva-no-cancelable",
                new Dictionary<string, object?> { ["estadoActual"] = resultado.EstadoActual!.Value.ToString() }),
            _ => ReservaNoEncontrada()
        };
    }

    private static ObjectResult ReservaNoEncontrada() =>
        ProblemasDeNegocio.Crear(StatusCodes.Status404NotFound, "La reserva no existe.", "reserva-no-encontrada");

    private static Dictionary<string, object?> Asientos(IReadOnlyList<AsientoPedidoDto> asientos) =>
        new() { ["asientos"] = asientos.Select(AsientoResponse.DesdeDto).ToList() };
}
