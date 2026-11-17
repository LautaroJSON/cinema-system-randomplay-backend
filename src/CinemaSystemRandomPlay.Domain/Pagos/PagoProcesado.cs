using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Pagos;

public enum ResultadoPagoProcesado
{
    Acreditado,
    Devuelto
}

/// <summary>
/// Registro de un pago aprobado que ya tuvo efecto (acreditó un carrito o generó una devolución). Su clave es el id del pago:
/// garantiza que avisos repetidos o concurrentes no repitan el efecto.
/// </summary>
public class PagoProcesado
{
    public string PagoId { get; private set; } = string.Empty;
    public Guid CarritoId { get; private set; }
    public Dinero Monto { get; private set; } = null!;
    public DateTimeOffset AprobadoEn { get; private set; }
    public ResultadoPagoProcesado Resultado { get; private set; }
    public DateTimeOffset ProcesadoEn { get; private set; }

    private PagoProcesado()
    {
    }

    public PagoProcesado(PagoAprobado pago, Guid carritoId, ResultadoPagoProcesado resultado, DateTimeOffset procesadoEn)
    {
        ArgumentNullException.ThrowIfNull(pago);

        PagoId = pago.PagoId;
        CarritoId = carritoId;
        Monto = pago.Monto;
        AprobadoEn = pago.AprobadoEn;
        Resultado = resultado;
        ProcesadoEn = procesadoEn;
    }
}
