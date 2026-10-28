using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Carritos;

public sealed record ReglasCarrito
{
    public int MinutosRetencion { get; }
    public int MaximoAsientosPorCarrito { get; }
    public int PorcentajeMaximoRetenidoPorFuncion { get; }

    public ReglasCarrito(int minutosRetencion = 5, int maximoAsientosPorCarrito = 10, int porcentajeMaximoRetenidoPorFuncion = 50)
    {
        if (minutosRetencion <= 0)
            throw new DomainException("Los minutos de retención deben ser mayores a cero.");

        if (maximoAsientosPorCarrito <= 0)
            throw new DomainException("El máximo de asientos por carrito debe ser mayor a cero.");

        if (porcentajeMaximoRetenidoPorFuncion is < 1 or > 100)
            throw new DomainException("El porcentaje máximo retenido por función debe estar entre 1 y 100.");

        MinutosRetencion = minutosRetencion;
        MaximoAsientosPorCarrito = maximoAsientosPorCarrito;
        PorcentajeMaximoRetenidoPorFuncion = porcentajeMaximoRetenidoPorFuncion;
    }

    /// <summary>Asientos que pueden estar retenidos a la vez en una función, sumando todos sus carritos activos.</summary>
    public int TopeRetenidoPara(int totalAsientosSala) =>
        Math.Max(1, totalAsientosSala * PorcentajeMaximoRetenidoPorFuncion / 100);
}
