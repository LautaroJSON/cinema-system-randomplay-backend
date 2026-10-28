namespace CinemaSystemRandomPlay.Application.Carritos.Ports;

public record TokenDeCarrito(string Valor, DateTimeOffset VenceEn);

/// <summary>
/// Emite el token de acceso a un carrito: la única credencial del comprador anónimo. No debe poder fabricarse a partir del
/// id del carrito ni de otro dato visible.
/// </summary>
public interface IEmisorDeTokenDeCarrito
{
    TokenDeCarrito Emitir(Guid carritoId, DateTimeOffset ahora);
}
