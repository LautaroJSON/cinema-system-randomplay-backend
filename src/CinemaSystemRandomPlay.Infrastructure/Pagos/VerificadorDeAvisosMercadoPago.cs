using System.Security.Cryptography;
using System.Text;
using CinemaSystemRandomPlay.Application.Pagos.Ports;

namespace CinemaSystemRandomPlay.Infrastructure.Pagos;

/// <summary>
/// Valida el header <c>x-signature</c> de los webhooks de Mercado Pago (specs/008-pago-mercado-pago/research.md, Decisión 7): el
/// manifest <c>id:{data.id};request-id:{x-request-id};ts:{ts};</c> firmado con HMAC-SHA256 y la clave secreta de avisos, en hex,
/// comparado en tiempo constante con <c>v1</c>. Las partes ausentes se omiten del manifest. Nunca lanza ante headers mal formados.
/// </summary>
public sealed class VerificadorDeAvisosMercadoPago : IVerificadorDeAvisos
{
    private readonly byte[] _clave;

    public VerificadorDeAvisosMercadoPago(OpcionesMercadoPago opciones)
    {
        _clave = Encoding.UTF8.GetBytes(opciones.ClaveSecretaAvisos);
    }

    public bool EsValido(string? firma, string? requestId, string? dataId)
    {
        if (_clave.Length == 0 || string.IsNullOrWhiteSpace(firma))
            return false;

        string? ts = null;
        string? v1 = null;

        foreach (var parte in firma.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separador = parte.IndexOf('=');
            if (separador <= 0)
                continue;

            var nombre = parte[..separador].Trim();
            var valor = parte[(separador + 1)..].Trim();

            if (nombre == "ts")
                ts = valor;
            else if (nombre == "v1")
                v1 = valor;
        }

        if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1))
            return false;

        byte[] recibida;
        try
        {
            recibida = Convert.FromHexString(v1);
        }
        catch (FormatException)
        {
            return false;
        }

        var esperada = HMACSHA256.HashData(_clave, Encoding.UTF8.GetBytes(Manifest(dataId, requestId, ts)));

        return CryptographicOperations.FixedTimeEquals(esperada, recibida);
    }

    /// <summary>
    /// Manifest firmado por Mercado Pago. El <c>data.id</c> alfanumérico se compara en minúsculas, como indica su documentación.
    /// </summary>
    public static string Manifest(string? dataId, string? requestId, string ts)
    {
        var manifest = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(dataId))
            manifest.Append("id:").Append(dataId.Trim().ToLowerInvariant()).Append(';');

        if (!string.IsNullOrWhiteSpace(requestId))
            manifest.Append("request-id:").Append(requestId.Trim()).Append(';');

        manifest.Append("ts:").Append(ts).Append(';');

        return manifest.ToString();
    }
}
