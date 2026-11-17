namespace CinemaSystemRandomPlay.Domain.Carritos;

public enum TipoResultadoInicioPago
{
    Iniciado,
    YaEnPago,
    NoActivo,
    SinEmailEntrega,
    SeleccionModificada
}

public sealed record ResultadoInicioPago(TipoResultadoInicioPago Tipo, string? UrlPago = null, EstadoCarrito? EstadoActual = null)
{
    public static ResultadoInicioPago Iniciado(string urlPago) => new(TipoResultadoInicioPago.Iniciado, urlPago);

    /// <summary>Ya estaba en pago: se devuelve la URL guardada y nada cambia.</summary>
    public static ResultadoInicioPago YaEnPago(string urlPago) => new(TipoResultadoInicioPago.YaEnPago, urlPago);

    public static ResultadoInicioPago NoActivo(EstadoCarrito estadoActual) => new(TipoResultadoInicioPago.NoActivo, EstadoActual: estadoActual);

    public static ResultadoInicioPago SinEmailEntrega() => new(TipoResultadoInicioPago.SinEmailEntrega);

    /// <summary>La selección cambió desde que se creó el pago en la pasarela.</summary>
    public static ResultadoInicioPago SeleccionModificada() => new(TipoResultadoInicioPago.SeleccionModificada);
}
