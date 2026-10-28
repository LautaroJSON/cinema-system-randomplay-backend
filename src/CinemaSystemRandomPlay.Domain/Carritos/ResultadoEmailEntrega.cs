namespace CinemaSystemRandomPlay.Domain.Carritos;

public sealed record ResultadoEmailEntrega(bool Indicado, EstadoCarrito EstadoActual)
{
    public static ResultadoEmailEntrega Ok() => new(true, EstadoCarrito.Activo);

    public static ResultadoEmailEntrega NoActivo(EstadoCarrito estadoActual) => new(false, estadoActual);
}
