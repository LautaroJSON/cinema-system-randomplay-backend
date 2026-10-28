namespace CinemaSystemRandomPlay.Application.Carritos.Ports;

/// <summary>
/// Punto de extensión para un desafío anti-bots (por ejemplo, un CAPTCHA) al crear un carrito. Puede hacer una llamada de
/// red, así que se evalúa antes de abrir la transacción.
/// </summary>
public interface IDesafioAntiBots
{
    /// <param name="respuesta">Lo que envió el cliente en el pedido (por ejemplo, el token del widget); puede faltar.</param>
    /// <param name="origen">Dirección de red del pedido, si se conoce.</param>
    Task<bool> Verificar(string? respuesta, string? origen, CancellationToken cancellationToken = default);
}
