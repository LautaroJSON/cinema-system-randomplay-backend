using System.Text.RegularExpressions;
using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Api.Modulos.Verificaciones.Contracts;
using CinemaSystemRandomPlay.Application.Verificaciones.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CinemaSystemRandomPlay.Api.Modulos.Verificaciones;

/// <summary>Acceso público: su función es dar acceso a quien todavía no lo tiene.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/verificaciones-email")]
public partial class VerificacionesEmailController : ControllerBase
{
    public const string PoliticaLimitePorOrigen = "solicitud-codigo";

    private readonly SolicitarVerificacionEmailCommandHandler _solicitarHandler;
    private readonly ConfirmarVerificacionEmailCommandHandler _confirmarHandler;

    public VerificacionesEmailController(
        SolicitarVerificacionEmailCommandHandler solicitarHandler,
        ConfirmarVerificacionEmailCommandHandler confirmarHandler)
    {
        _solicitarHandler = solicitarHandler;
        _confirmarHandler = confirmarHandler;
    }

    [HttpPost]
    [EnableRateLimiting(PoliticaLimitePorOrigen)]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarVerificacionRequest request, CancellationToken cancellationToken)
    {
        var estado = await _solicitarHandler.Handle(new SolicitarVerificacionEmailCommand(request.Email), cancellationToken);

        return estado switch
        {
            EstadoSolicitarVerificacion.Aceptada => Accepted(),
            EstadoSolicitarVerificacion.EmailInvalido => PedidoInvalido("email", "Debe indicar un email válido."),
            _ => ProblemasDeNegocio.Crear(StatusCodes.Status429TooManyRequests,
                "Se superó el máximo de pedidos de código para este email. Intente más tarde.", "demasiados-pedidos")
        };
    }

    [HttpPost("confirmacion")]
    public async Task<IActionResult> Confirmar([FromBody] ConfirmarVerificacionRequest request, CancellationToken cancellationToken)
    {
        if (request.Codigo is null || !SeisDigitos().IsMatch(request.Codigo))
            return PedidoInvalido("codigo", "El código debe tener seis dígitos.");

        var resultado = await _confirmarHandler.Handle(new ConfirmarVerificacionEmailCommand(request.Email, request.Codigo), cancellationToken);

        return resultado.Estado switch
        {
            EstadoConfirmarVerificacion.Ok => Ok(CredencialResponse.DesdeCredencial(resultado.Credencial!)),
            EstadoConfirmarVerificacion.EmailInvalido => PedidoInvalido("email", "Debe indicar un email válido."),
            EstadoConfirmarVerificacion.CodigoVencido => ProblemasDeNegocio.Crear(StatusCodes.Status410Gone,
                "El código venció. Pida uno nuevo.", "codigo-vencido"),
            EstadoConfirmarVerificacion.CodigoInvalidado => ProblemasDeNegocio.Crear(StatusCodes.Status400BadRequest,
                "Se superó el máximo de intentos. Pida un código nuevo.", "codigo-invalidado"),
            _ => ProblemasDeNegocio.Crear(StatusCodes.Status400BadRequest,
                "El código es inválido.", "codigo-invalido")
        };
    }

    private static ObjectResult PedidoInvalido(string campo, string mensaje) =>
        ProblemasDeNegocio.Crear(StatusCodes.Status400BadRequest, "El pedido es inválido.", "pedido-invalido",
            new Dictionary<string, object?> { ["errors"] = new Dictionary<string, string[]> { [campo] = [mensaje] } });

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex SeisDigitos();
}
