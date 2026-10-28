namespace CinemaSystemRandomPlay.Domain.Carritos;

/// <summary>
/// <see cref="Expirado"/> nunca se guarda: es el estado efectivo de un carrito <see cref="Activo"/> cuyo vencimiento ya pasó.
/// </summary>
public enum EstadoCarrito
{
    Activo,
    Abandonado,
    Expirado
}
