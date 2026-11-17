namespace CinemaSystemRandomPlay.Domain.Carritos;

public sealed record ResultadoEmailEntrega(bool Indicado, EstadoCarrito EstadoActual)
{
    public static ResultadoEmailEntrega Ok() => new(true, EstadoCarrito.Activo);

    public static ResultadoEmailEntrega NoActivo(EstadoCarrito estadoActual) => new(false, estadoActual);

    /// <summary>El carrito está en pago: el email de entrega queda fijo.</summary>
    public static ResultadoEmailEntrega EnPago() => new(false, EstadoCarrito.Pagando);
}
