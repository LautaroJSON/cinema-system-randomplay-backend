namespace CinemaSystemRandomPlay.Api.Modulos.Carritos.Contracts;

/// <param name="Desafio">Respuesta al desafío anti-bots (por ejemplo, el token de un widget de CAPTCHA). Opcional.</param>
public record CrearCarritoRequest(IReadOnlyList<AsientoRequest>? Asientos, string? Desafio);

public record ReemplazarAsientosRequest(IReadOnlyList<AsientoRequest>? Asientos);

public record EmailEntregaRequest(string? Email);

public record AsientoRequest(string? Fila, int Numero);
