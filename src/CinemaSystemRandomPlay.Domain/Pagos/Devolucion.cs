using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Pagos;

public enum EstadoDevolucion
{
    Pendiente,
    Completada,
    Fallida
}

/// <summary>
/// Devolución completa y automática de un pago aprobado que no pudo pagar un carrito. Su <see cref="Id"/> es también la clave
/// de idempotencia ante la pasarela: un reintento nunca devuelve dos veces.
/// </summary>
public class Devolucion
{
    public const int LargoMaximoError = 500;

    public Guid Id { get; private set; }
    public string PagoId { get; private set; } = string.Empty;
    public Guid CarritoId { get; private set; }
    public Dinero Monto { get; private set; } = null!;
    public MotivoDevolucion Motivo { get; private set; }
    public EstadoDevolucion Estado { get; private set; }
    public DateTimeOffset SolicitadaEn { get; private set; }
    public DateTimeOffset? ResueltaEn { get; private set; }
    public string? UltimoError { get; private set; }

    private Devolucion()
    {
    }

    public Devolucion(Guid id, PagoAprobado pago, Guid carritoId, MotivoDevolucion motivo, DateTimeOffset solicitadaEn)
    {
        ArgumentNullException.ThrowIfNull(pago);

        Id = id;
        PagoId = pago.PagoId;
        CarritoId = carritoId;
        Monto = pago.Monto;
        Motivo = motivo;
        Estado = EstadoDevolucion.Pendiente;
        SolicitadaEn = solicitadaEn;
    }

    /// <summary>Idempotente: una devolución ya resuelta no cambia.</summary>
    public void Completar(DateTimeOffset ahora)
    {
        if (Estado != EstadoDevolucion.Pendiente)
            return;

        Estado = EstadoDevolucion.Completada;
        ResueltaEn = ahora;
        UltimoError = null;
    }

    /// <summary>Se agotaron los reintentos. Idempotente: una devolución ya resuelta no cambia.</summary>
    public void Fallar(string error, DateTimeOffset ahora)
    {
        if (Estado != EstadoDevolucion.Pendiente)
            return;

        Estado = EstadoDevolucion.Fallida;
        ResueltaEn = ahora;
        UltimoError = Recortar(error);
    }

    private static string Recortar(string error) =>
        error.Length <= LargoMaximoError ? error : error[..LargoMaximoError];
}
