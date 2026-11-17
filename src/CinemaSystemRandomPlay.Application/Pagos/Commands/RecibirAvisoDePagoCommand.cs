using CinemaSystemRandomPlay.Application.Pagos.Ports;

namespace CinemaSystemRandomPlay.Application.Pagos.Commands;

/// <param name="Firma">Header <c>x-signature</c>.</param>
/// <param name="RequestId">Header <c>x-request-id</c>.</param>
/// <param name="DataId">Query param <c>data.id</c>: el id del recurso avisado.</param>
/// <param name="Tipo">Tipo de recurso avisado (<c>payment</c>, <c>merchant_order</c>, ...).</param>
public record RecibirAvisoDePagoCommand(string? Firma, string? RequestId, string? DataId, string? Tipo);

public enum ResultadoAviso
{
    FirmaInvalida,

    /// <summary>Aviso válido que no corresponde a un pago o no tiene efecto.</summary>
    Ignorado,

    Procesado,

    /// <summary>La pasarela no respondió al consultar el pago: conviene que reintente el aviso.</summary>
    ProcesadorNoDisponible
}

/// <summary>
/// Aviso (webhook) de la pasarela. No confía en el contenido: valida la firma y usa solo el id del pago, que consulta a la pasarela
/// (spec 008, FR-013 y FR-014).
/// </summary>
public class RecibirAvisoDePagoCommandHandler
{
    public const string TipoPago = "payment";

    private readonly IVerificadorDeAvisos _verificador;
    private readonly ProcesarPagoCommandHandler _procesarPago;

    public RecibirAvisoDePagoCommandHandler(IVerificadorDeAvisos verificador, ProcesarPagoCommandHandler procesarPago)
    {
        _verificador = verificador;
        _procesarPago = procesarPago;
    }

    public async Task<(ResultadoAviso Resultado, ResultadoProcesamientoPago? Procesamiento)> Handle(RecibirAvisoDePagoCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_verificador.EsValido(command.Firma, command.RequestId, command.DataId))
            return (ResultadoAviso.FirmaInvalida, null);

        if (!string.Equals(command.Tipo, TipoPago, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(command.DataId))
            return (ResultadoAviso.Ignorado, null);

        try
        {
            var procesamiento = await _procesarPago.Handle(new ProcesarPagoCommand(command.DataId.Trim()), cancellationToken);
            return (ResultadoAviso.Procesado, procesamiento);
        }
        catch (ProcesadorDePagosNoDisponibleException)
        {
            return (ResultadoAviso.ProcesadorNoDisponible, null);
        }
    }
}
