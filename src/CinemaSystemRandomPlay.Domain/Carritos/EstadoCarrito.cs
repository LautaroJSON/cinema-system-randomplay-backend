namespace CinemaSystemRandomPlay.Domain.Carritos;

/// <summary>
/// En la base se guardan <c>Activo</c>, <c>Pagando</c>, <c>Pagado</c> y <c>Abandonado</c>. <c>Expirado</c> se calcula a
/// partir del vencimiento (ver <see cref="Carrito.EstadoEn(EstadoCarrito, DateTimeOffset, DateTimeOffset)"/>).
/// </summary>
public enum EstadoCarrito
{
    Activo,
    Abandonado,
    Expirado,
    Pagando,
    Pagado
}
