namespace CinemaSystemRandomPlay.Application.Pagos.Ports;

/// <summary>Valida la firma de un aviso (webhook) de la pasarela. No confía en nada más del aviso.</summary>
public interface IVerificadorDeAvisos
{
    /// <param name="firma">Header <c>x-signature</c>.</param>
    /// <param name="requestId">Header <c>x-request-id</c>.</param>
    /// <param name="dataId">Query param <c>data.id</c>.</param>
    bool EsValido(string? firma, string? requestId, string? dataId);
}
