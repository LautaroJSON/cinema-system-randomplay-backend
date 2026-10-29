namespace CinemaSystemRandomPlay.Api.Modulos.Carritos.Contracts;

/// <param name="Asientos">Asientos elegidos: al menos uno, hasta el máximo por carrito (10 por defecto), sin repetir.</param>
/// <param name="Desafio">Respuesta al desafío anti-bots (por ejemplo, el token de un widget de CAPTCHA). Opcional.</param>
public record CrearCarritoRequest(IReadOnlyList<AsientoRequest>? Asientos, string? Desafio);

/// <param name="Asientos">La nueva selección completa, con las mismas reglas que al crear el carrito.</param>
public record ReemplazarAsientosRequest(IReadOnlyList<AsientoRequest>? Asientos);

/// <param name="Email">Email al que se enviarán las entradas.</param>
public record EmailEntregaRequest(string? Email);

/// <param name="Fila">Una letra mayúscula de A a Z.</param>
/// <param name="Numero">Número de asiento dentro de la fila, desde 1.</param>
public record AsientoRequest(string? Fila, int Numero);
