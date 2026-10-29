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
[Tags("Carritos")]
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

    /// <summary>Crea un carrito que retiene los asientos elegidos de una función.</summary>
    /// <remarks>
    /// Público y limitado por origen. Los asientos quedan retenidos hasta <c>venceEn</c> (unos minutos); después el carrito
    /// expira y se liberan. La respuesta trae el <c>token</c> del carrito: es la única vez que se entrega y lo exigen todas las
    /// demás operaciones como <c>Authorization: Bearer {token}</c>.
    /// </remarks>
    /// <param name="funcionId">Función cuyos asientos se retienen.</param>
    /// <param name="request">Asientos elegidos (entre 1 y el máximo por carrito, sin repetir) y, opcionalmente, el desafío anti-bots.</param>
    /// <response code="201">Carrito creado; incluye el token de acceso.</response>
    /// <response code="400"><c>pedido-invalido</c>: sin asientos, demasiados, repetidos o con fila/número mal formados. Ver <c>errors</c>.</response>
    /// <response code="403"><c>desafio-rechazado</c>: no se superó el desafío anti-bots.</response>
    /// <response code="404"><c>funcion-no-encontrada</c>: la función no existe o su película no está activa.</response>
    /// <response code="409"><c>asientos-no-disponibles</c> (ver <c>asientos</c>) o <c>funcion-sin-cupo</c>: la función alcanzó el máximo de asientos retenidos a la vez.</response>
    /// <response code="410"><c>funcion-no-disponible</c>: la función ya comenzó.</response>
    /// <response code="422"><c>asientos-inexistentes</c>: hay asientos que no existen en la sala (ver <c>asientos</c>).</response>
    /// <response code="429"><c>demasiados-pedidos</c>: se superó el límite de creaciones desde este origen. Respetar <c>Retry-After</c>.</response>
    [HttpPost("api/funciones/{funcionId:guid}/carritos")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaCreacionPorOrigen)]
    [ProducesResponseType<CarritoCreadoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Crear(Guid funcionId, [FromBody] CrearCarritoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _crearCarritoCommandHandler.Handle(
            new CrearCarritoCommand(funcionId, Asientos(request.Asientos), request.Desafio, HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return resultado.Estado == EstadoOperacionCarrito.Ok
            ? CreatedAtAction(nameof(Obtener), new { carritoId = resultado.Carrito!.Id }, CarritoCreadoResponse.DesdeDto(resultado.Carrito, resultado.Token!))
            : Problema(resultado);
    }

    /// <summary>Obtiene el carrito del token.</summary>
    /// <remarks>Sirve para refrescar el estado y el vencimiento; un carrito abandonado o expirado se sigue pudiendo consultar.</remarks>
    /// <param name="carritoId">Carrito del token.</param>
    /// <response code="200">El carrito.</response>
    /// <response code="404"><c>carrito-no-encontrado</c>: no existe o el token es de otro carrito.</response>
    [HttpGet("api/carritos/{carritoId:guid}")]
    [ProducesResponseType<CarritoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Obtener(Guid carritoId, CancellationToken cancellationToken)
    {
        if (User.ObtenerCarritoId() is not { } delToken)
            return Unauthorized();

        var carrito = await _obtenerCarritoQueryHandler.Handle(new ObtenerCarritoQuery(carritoId, delToken), cancellationToken);

        return carrito is null ? CarritoNoEncontrado() : Ok(CarritoResponse.DesdeDto(carrito));
    }

    /// <summary>Reemplaza la selección completa de asientos del carrito.</summary>
    /// <remarks>
    /// La lista enviada pasa a ser toda la selección: los asientos que no figuran se liberan. Solo para carritos activos.
    /// </remarks>
    /// <param name="carritoId">Carrito del token.</param>
    /// <param name="request">La nueva selección completa (mismas reglas que al crear).</param>
    /// <response code="200">El carrito con la nueva selección.</response>
    /// <response code="400"><c>pedido-invalido</c>: selección mal formada. Ver <c>errors</c>.</response>
    /// <response code="404"><c>carrito-no-encontrado</c>: no existe o el token es de otro carrito.</response>
    /// <response code="409"><c>asientos-no-disponibles</c> (ver <c>asientos</c>), <c>funcion-sin-cupo</c> o <c>carrito-no-activo</c> (ver <c>estadoActual</c>).</response>
    /// <response code="410"><c>funcion-no-disponible</c>: la función ya comenzó.</response>
    /// <response code="422"><c>asientos-inexistentes</c>: hay asientos que no existen en la sala (ver <c>asientos</c>).</response>
    [HttpPut("api/carritos/{carritoId:guid}/asientos")]
    [ProducesResponseType<CarritoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> ReemplazarAsientos(Guid carritoId, [FromBody] ReemplazarAsientosRequest request, CancellationToken cancellationToken)
    {
        if (User.ObtenerCarritoId() is not { } delToken)
            return Unauthorized();

        var resultado = await _reemplazarSeleccionCommandHandler.Handle(
            new ReemplazarSeleccionCommand(carritoId, delToken, Asientos(request.Asientos)), cancellationToken);

        return Respuesta(resultado);
    }

    /// <summary>Abandona el carrito y libera sus asientos de inmediato.</summary>
    /// <param name="carritoId">Carrito del token.</param>
    /// <response code="200">El carrito, ya en estado <c>Abandonado</c>.</response>
    /// <response code="404"><c>carrito-no-encontrado</c>: no existe o el token es de otro carrito.</response>
    /// <response code="409"><c>carrito-no-activo</c>: ya estaba abandonado o expirado (ver <c>estadoActual</c>).</response>
    [HttpPost("api/carritos/{carritoId:guid}/abandono")]
    [ProducesResponseType<CarritoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Abandonar(Guid carritoId, CancellationToken cancellationToken)
    {
        if (User.ObtenerCarritoId() is not { } delToken)
            return Unauthorized();

        return Respuesta(await _abandonarCarritoCommandHandler.Handle(new AbandonarCarritoCommand(carritoId, delToken), cancellationToken));
    }

    /// <summary>Indica o reemplaza el email al que se enviarán las entradas.</summary>
    /// <param name="carritoId">Carrito del token.</param>
    /// <param name="request">Email del comprador.</param>
    /// <response code="200">El carrito con el email de entrega.</response>
    /// <response code="400"><c>pedido-invalido</c>: el email es inválido (ver <c>errors.email</c>).</response>
    /// <response code="404"><c>carrito-no-encontrado</c>: no existe o el token es de otro carrito.</response>
    /// <response code="409"><c>carrito-no-activo</c>: el carrito está abandonado o expirado (ver <c>estadoActual</c>).</response>
    [HttpPut("api/carritos/{carritoId:guid}/email-entrega")]
    [ProducesResponseType<CarritoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
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
