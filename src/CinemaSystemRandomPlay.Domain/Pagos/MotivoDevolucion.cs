namespace CinemaSystemRandomPlay.Domain.Pagos;

public enum MotivoDevolucion
{
    /// <summary>Aprobado después del vencimiento del carrito, o con el carrito sin pago iniciado o abandonado.</summary>
    PagoTardio,

    /// <summary>El carrito ya estaba pagado con otro pago.</summary>
    CarritoYaPagado,

    /// <summary>Aprobado a tiempo, pero al procesarlo el carrito ya había vencido y otro carrito tomó alguno de sus asientos.</summary>
    AsientosNoDisponibles,

    /// <summary>El monto o la moneda no coinciden con el total del carrito.</summary>
    MontoDistinto
}
