using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Pagos;

/// <summary>Pago aprobado según la pasarela (consultado a ella, nunca tomado de un aviso).</summary>
public sealed record PagoAprobado
{
    public string PagoId { get; }
    public Dinero Monto { get; }
    public DateTimeOffset AprobadoEn { get; }

    public PagoAprobado(string pagoId, Dinero monto, DateTimeOffset aprobadoEn)
    {
        if (string.IsNullOrWhiteSpace(pagoId))
            throw new DomainException("El pago debe tener un identificador.");

        PagoId = pagoId;
        Monto = monto ?? throw new DomainException("El pago debe tener un monto.");
        AprobadoEn = aprobadoEn;
    }
}
