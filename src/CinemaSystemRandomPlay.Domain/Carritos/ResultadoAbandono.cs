namespace CinemaSystemRandomPlay.Domain.Carritos;

public sealed record ResultadoAbandono(bool Abandonado, EstadoCarrito EstadoActual)
{
    public static ResultadoAbandono Ok() => new(true, EstadoCarrito.Abandonado);

    public static ResultadoAbandono NoActivo(EstadoCarrito estadoActual) => new(false, estadoActual);

    /// <summary>El carrito está en pago: no se puede abandonar hasta que se pague o venza.</summary>
    public static ResultadoAbandono EnPago() => new(false, EstadoCarrito.Pagando);
}
