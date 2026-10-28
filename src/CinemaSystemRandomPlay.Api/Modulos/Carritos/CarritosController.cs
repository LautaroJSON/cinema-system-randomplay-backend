using CinemaSystemRandomPlay.Api.Autenticacion;
using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Api.Modulos.Carritos.Contracts;
using CinemaSystemRandomPlay.Application.Carritos;
using CinemaSystemRandomPlay.Application.Carritos.Commands;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CinemaSystemRandomPlay.Api.Modulos.Carritos;

/// <summary>
/// Crear un carrito es público (con límite por origen); todo lo demás exige el token del carrito. Sin token válido responde
/// 401 para cualquier id; con el token de otro carrito, 404 igual que si no existiera.
/// </summary>
[ApiController]
[Authorize(Policy = Politicas.PortadorDeCarrito)]
public class CarritosController : ControllerBase
{
    public const string PoliticaCreacionPorOrigen = "CreacionDeCarritos";

    private readonly CrearCarritoCommandHandler _crearCarritoCommandHandler;
    private readonly ObtenerCarritoQueryHandler _obtenerCarritoQueryHandler;
    private readonly ReemplazarSeleccionCommandHandler _reemplazarSeleccionCommandHandler;
    private readonly AbandonarCarritoCommandHandler _abandonarCarritoCommandHandler;
    private readonly IndicarEmailEntregaCommandHandler _indicarEmailEntregaCommandHandler;

    public CarritosController(
        CrearCarritoCommandHandler crearCarritoCommandHandler,
        ObtenerCarritoQueryHandler obtenerCarritoQueryHandler,
        ReemplazarSeleccionCommandHandler reemplazarSeleccionCommandHandler,
        AbandonarCarritoCommandHandler abandonarCarritoCommandHandler,
        IndicarEmailEntregaCommandHandler indicarEmailEntregaCommandHandler)
    {
        _crearCarritoCommandHandler = crearCarritoCommandHandler;
        _obtenerCarritoQueryHandler = obtenerCarritoQueryHandler;
        _reemplazarSeleccionCommandHandler = reemplazarSeleccionCommandHandler;
        _abandonarCarritoCommandHandler = abandonarCarritoCommandHandler;
        _indicarEmailEntregaCommandHandler = indicarEmailEntregaCommandHandler;
    }

    [HttpPost("api/funciones/{funcionId:guid}/carritos")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaCreacionPorOrigen)]
    public async Task<IActionResult> Crear(Guid funcionId, [FromBody] CrearCarritoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _crearCarritoCommandHandler.Handle(
            new CrearCarritoCommand(funcionId, Asientos(request.Asientos), request.Desafio, HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return resultado.Estado == EstadoOperacionCarrito.Ok
            ? CreatedAtAction(nameof(Obtener), new { carritoId = resultado.Carrito!.Id }, CarritoCreadoResponse.DesdeDto(resultado.Carrito, resultado.Token!))
            : Problema(resultado);
    }

    [HttpGet("api/carritos/{carritoId:guid}")]
    public async Task<IActionResult> Obtener(Guid carritoId, CancellationToken cancellationToken)
    {
        if (User.ObtenerCarritoId() is not { } delToken)
            return Unauthorized();

        var carrito = await _obtenerCarritoQueryHandler.Handle(new ObtenerCarritoQuery(carritoId, delToken), cancellationToken);

        return carrito is null ? CarritoNoEncontrado() : Ok(CarritoResponse.DesdeDto(carrito));
    }

    [HttpPut("api/carritos/{carritoId:guid}/asientos")]
    public async Task<IActionResult> ReemplazarAsientos(Guid carritoId, [FromBody] ReemplazarAsientosRequest request, CancellationToken cancellationToken)
    {
        if (User.ObtenerCarritoId() is not { } delToken)
            return Unauthorized();

        var resultado = await _reemplazarSeleccionCommandHandler.Handle(
            new ReemplazarSeleccionCommand(carritoId, delToken, Asientos(request.Asientos)), cancellationToken);

        return Respuesta(resultado);
    }

    [HttpPost("api/carritos/{carritoId:guid}/abandono")]
    public async Task<IActionResult> Abandonar(Guid carritoId, CancellationToken cancellationToken)
    {
        if (User.ObtenerCarritoId() is not { } delToken)
            return Unauthorized();

        return Respuesta(await _abandonarCarritoCommandHandler.Handle(new AbandonarCarritoCommand(carritoId, delToken), cancellationToken));
    }

    [HttpPut("api/carritos/{carritoId:guid}/email-entrega")]
    public async Task<IActionResult> IndicarEmailEntrega(Guid carritoId, [FromBody] EmailEntregaRequest request, CancellationToken cancellationToken)
    {
        if (User.ObtenerCarritoId() is not { } delToken)
            return Unauthorized();

        var resultado = await _indicarEmailEntregaCommandHandler.Handle(
            new IndicarEmailEntregaCommand(carritoId, delToken, request.Email), cancellationToken);

        return Respuesta(resultado);
    }

    private IActionResult Respuesta(ResultadoOperacionCarrito resultado) =>
        resultado.Estado == EstadoOperacionCarrito.Ok ? Ok(CarritoResponse.DesdeDto(resultado.Carrito!)) : Problema(resultado);

    private static ObjectResult Problema(ResultadoOperacionCarrito resultado) => resultado.Estado switch
    {
        EstadoOperacionCarrito.PedidoInvalido => ProblemasDeNegocio.Crear(StatusCodes.Status400BadRequest,
            "El pedido es inválido.", "pedido-invalido",
            new Dictionary<string, object?> { ["errors"] = resultado.Errores }),
        EstadoOperacionCarrito.DesafioRechazado => ProblemasDeNegocio.Crear(StatusCodes.Status403Forbidden,
            "No se superó el desafío anti-bots.", "desafio-rechazado"),
        EstadoOperacionCarrito.FuncionNoEncontrada => ProblemasDeNegocio.Crear(StatusCodes.Status404NotFound,
            "La función no existe.", "funcion-no-encontrada"),
        EstadoOperacionCarrito.CarritoNoEncontrado => CarritoNoEncontrado(),
        EstadoOperacionCarrito.FuncionNoDisponible => ProblemasDeNegocio.Crear(StatusCodes.Status410Gone,
            "La función ya comenzó.", "funcion-no-disponible"),
        EstadoOperacionCarrito.AsientosInexistentes => ProblemasDeNegocio.Crear(StatusCodes.Status422UnprocessableEntity,
            "Hay asientos que no existen en la sala.", "asientos-inexistentes", AsientosDelProblema(resultado.Asientos!)),
        EstadoOperacionCarrito.AsientosNoDisponibles => ProblemasDeNegocio.Crear(StatusCodes.Status409Conflict,
            "Hay asientos que ya no están disponibles.", "asientos-no-disponibles", AsientosDelProblema(resultado.Asientos!)),
        EstadoOperacionCarrito.FuncionSinCupo => ProblemasDeNegocio.Crear(StatusCodes.Status409Conflict,
            "La función alcanzó el máximo de asientos retenidos a la vez. Intente más tarde.", "funcion-sin-cupo"),
        EstadoOperacionCarrito.CarritoNoActivo => ProblemasDeNegocio.Crear(StatusCodes.Status409Conflict,
            "El carrito ya no está activo.", "carrito-no-activo",
            new Dictionary<string, object?> { ["estadoActual"] = resultado.EstadoActual!.Value.ToString() }),
        _ => throw new InvalidOperationException($"{resultado.Estado} no es un rechazo.")
    };

    private static ObjectResult CarritoNoEncontrado() =>
        ProblemasDeNegocio.Crear(StatusCodes.Status404NotFound, "El carrito no existe.", "carrito-no-encontrado");

    private static IReadOnlyList<AsientoPedidoDto>? Asientos(IReadOnlyList<AsientoRequest?>? asientos) =>
        asientos?.Select(a => new AsientoPedidoDto(a?.Fila ?? string.Empty, a?.Numero ?? 0)).ToList();

    private static Dictionary<string, object?> AsientosDelProblema(IReadOnlyList<AsientoPedidoDto> asientos) =>
        new() { ["asientos"] = asientos.Select(AsientoResponse.DesdeDto).ToList() };
}
