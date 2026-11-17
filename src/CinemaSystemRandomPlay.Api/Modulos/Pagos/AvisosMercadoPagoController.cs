using System.Text.Json;
using CinemaSystemRandomPlay.Api.Modulos.Pagos.Contracts;
using CinemaSystemRandomPlay.Application.Pagos.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Pagos;

/// <summary>
/// Webhook de Mercado Pago. Público de forma explícita (lo llama Mercado Pago, sin token): lo protege la firma <c>x-signature</c>,
/// y aun con firma válida la decisión se toma con el pago consultado a Mercado Pago, nunca con el contenido del aviso.
/// </summary>
[ApiController]
[AllowAnonymous]
[Tags("Pagos")]
public class AvisosMercadoPagoController : ControllerBase
{
    private readonly RecibirAvisoDePagoCommandHandler _recibirAvisoDePagoCommandHandler;
    private readonly ILogger<AvisosMercadoPagoController> _logger;

    public AvisosMercadoPagoController(RecibirAvisoDePagoCommandHandler recibirAvisoDePagoCommandHandler,
        ILogger<AvisosMercadoPagoController> logger)
    {
        _recibirAvisoDePagoCommandHandler = recibirAvisoDePagoCommandHandler;
        _logger = logger;
    }

    /// <summary>Recibe un aviso de Mercado Pago (webhook).</summary>
    /// <remarks>
    /// Lo llama Mercado Pago. Valida la firma <c>x-signature</c> con la clave secreta del webhook y consulta el pago a Mercado Pago
    /// antes de hacer nada. Procesar el mismo pago más de una vez no repite entradas, emails ni devoluciones.
    /// </remarks>
    /// <param name="dataId">Id del recurso avisado (<c>data.id</c>).</param>
    /// <param name="type">Tipo de recurso avisado; solo se procesa <c>payment</c>.</param>
    /// <response code="200">Aviso recibido (procesado o sin efecto).</response>
    /// <response code="401">Firma ausente o inválida: no se procesó.</response>
    /// <response code="500">Mercado Pago no respondió al consultar el pago; Mercado Pago reintenta el aviso.</response>
    [HttpPost("api/pagos/mercadopago/avisos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Recibir(
        [FromQuery(Name = "data.id")] string? dataId,
        [FromQuery(Name = "type")] string? type,
        CancellationToken cancellationToken)
    {
        // El cuerpo solo completa el tipo o el id si no vinieron en el query. Se lee a mano para que un aviso sin cuerpo o sin
        // Content-Type llegue igual a la validación de la firma.
        var aviso = dataId is null || type is null ? await LeerCuerpo(cancellationToken) : null;

        var command = new RecibirAvisoDePagoCommand(
            Request.Headers["x-signature"].FirstOrDefault(),
            Request.Headers["x-request-id"].FirstOrDefault(),
            dataId ?? aviso?.Data?.Id,
            type ?? aviso?.Type);

        var (resultado, procesamiento) = await _recibirAvisoDePagoCommandHandler.Handle(command, cancellationToken);

        _logger.LogInformation("Aviso de Mercado Pago {Tipo} {DataId}: {Resultado} {Procesamiento}.", command.Tipo, command.DataId,
            resultado, procesamiento);

        return resultado switch
        {
            ResultadoAviso.FirmaInvalida => Unauthorized(),
            ResultadoAviso.ProcesadorNoDisponible => StatusCode(StatusCodes.Status500InternalServerError),
            _ => Ok()
        };
    }

    private async Task<AvisoMercadoPagoRequest?> LeerCuerpo(CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<AvisoMercadoPagoRequest>(Request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
