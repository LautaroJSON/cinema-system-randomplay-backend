namespace CinemaSystemRandomPlay.Infrastructure.Pagos;

public enum ModoMercadoPago
{
    /// <summary>Procesador en memoria: tests y desarrollo sin túnel ni credenciales.</summary>
    Simulado,

    /// <summary>API de Mercado Pago con credenciales de prueba.</summary>
    Real
}

/// <summary>
/// Configuración de la pasarela. <see cref="AccessToken"/> y <see cref="ClaveSecretaAvisos"/> son secretos: solo por user-secrets
/// o variables de entorno, nunca en archivos versionados (spec 008, FR-028).
/// </summary>
public class OpcionesMercadoPago
{
    public const string Seccion = "MercadoPago";
    public const string MarcadorCarrito = "{carritoId}";

    public ModoMercadoPago Modo { get; set; } = ModoMercadoPago.Simulado;

    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Clave secreta de la firma de los webhooks (x-signature).</summary>
    public string ClaveSecretaAvisos { get; set; } = string.Empty;

    /// <summary>URL pública del endpoint de avisos (por ejemplo, un túnel).</summary>
    public string UrlAvisos { get; set; } = string.Empty;

    /// <summary>Vuelta al front después de pagar; <c>{carritoId}</c> se reemplaza.</summary>
    public string UrlRetorno { get; set; } = "http://localhost:5173/carritos/{carritoId}/pago";

    public int SegundosTimeoutConsulta { get; set; } = 3;

    /// <returns>Los errores de configuración; vacío si es válida.</returns>
    public IReadOnlyList<string> Validar()
    {
        var errores = new List<string>();

        // En modo simulado la clave es opcional: sin ella, el endpoint de avisos rechaza todo (401).
        if (Modo == ModoMercadoPago.Real)
        {
            if (string.IsNullOrWhiteSpace(ClaveSecretaAvisos))
                errores.Add($"Falta {Seccion}:ClaveSecretaAvisos.");

            if (string.IsNullOrWhiteSpace(AccessToken))
                errores.Add($"Falta {Seccion}:AccessToken (credencial de prueba).");

            if (!Uri.TryCreate(UrlAvisos, UriKind.Absolute, out _))
                errores.Add($"Falta {Seccion}:UrlAvisos o no es una URL absoluta.");
        }

        if (!Uri.TryCreate(UrlRetorno.Replace(MarcadorCarrito, Guid.Empty.ToString()), UriKind.Absolute, out _))
            errores.Add($"{Seccion}:UrlRetorno no es una URL absoluta.");

        if (SegundosTimeoutConsulta < 1)
            errores.Add($"{Seccion}:SegundosTimeoutConsulta debe ser al menos 1.");

        return errores;
    }

    public string UrlRetornoPara(Guid carritoId) => UrlRetorno.Replace(MarcadorCarrito, carritoId.ToString());
}
