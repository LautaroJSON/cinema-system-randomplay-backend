namespace CinemaSystemRandomPlay.Domain.Carritos;

public sealed record ResultadoAbandono(bool Abandonado, EstadoCarrito EstadoActual)
{
    public static ResultadoAbandono Ok() => new(true, EstadoCarrito.Abandonado);

    public static ResultadoAbandono NoActivo(EstadoCarrito estadoActual) => new(false, estadoActual);
}
