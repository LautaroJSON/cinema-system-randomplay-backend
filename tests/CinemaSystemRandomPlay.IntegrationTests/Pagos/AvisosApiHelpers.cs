using System.Net.Http.Json;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

internal static class AvisosApiHelpers
{
    /// <summary>Envía un aviso de Mercado Pago firmado con la clave de pruebas (o sin firma si <paramref name="firma"/> es "").</summary>
    public static async Task<HttpResponseMessage> EnviarAviso(this HttpClient cliente, string dataId, string tipo = "payment",
        string? firma = null, string requestId = "req-1", string? accion = "payment.updated")
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/pagos/mercadopago/avisos?data.id={dataId}&type={tipo}")
        {
            Content = JsonContent.Create(new { action = accion, type = tipo, data = new { id = dataId } })
        };

        var valorFirma = firma ?? FirmaDeAvisos.Firmar(dataId, requestId, ts, TokensDePrueba.ClaveAvisosDePrueba);
        if (valorFirma != string.Empty)
            request.Headers.Add("x-signature", valorFirma);
        request.Headers.Add("x-request-id", requestId);

        return await cliente.SendAsync(request);
    }
}
