namespace CinemaSystemRandomPlay.Domain.Compartido;

/// <summary>
/// Monto en una moneda. Inmutable, exacto (<see cref="decimal"/>), sin negativos ni más de 2 decimales.
/// Igualdad por valor.
/// </summary>
public sealed record Dinero
{
    public decimal Monto { get; }
    public Moneda Moneda { get; }

    public Dinero(decimal monto, Moneda moneda)
    {
        if (monto < 0)
            throw new DomainException("El monto de dinero no puede ser negativo.");

        if (decimal.Round(monto, 2) != monto)
            throw new DomainException("El monto de dinero no puede tener más de 2 decimales.");

        if (!Enum.IsDefined(moneda))
            throw new DomainException("La moneda del dinero no es válida.");

        Monto = decimal.Round(monto, 2) + 0.00m;
        Moneda = moneda;
    }

    public static Dinero Ars(decimal monto) => new(monto, Moneda.ARS);

    public Dinero Multiplicar(int cantidad)
    {
        if (cantidad < 0)
            throw new DomainException("La cantidad por la que se multiplica el dinero no puede ser negativa.");

        return new Dinero(Monto * cantidad, Moneda);
    }
}
