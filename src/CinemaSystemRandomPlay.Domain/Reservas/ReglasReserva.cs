using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Reservas;

public sealed record ReglasReserva
{
    public int MinutosRetencion { get; }
    public int MaximoAsientosPorComprador { get; }

    public ReglasReserva(int minutosRetencion = 10, int maximoAsientosPorComprador = 10)
    {
        if (minutosRetencion <= 0)
            throw new DomainException("Los minutos de retención deben ser mayores a cero.");

        if (maximoAsientosPorComprador <= 0)
            throw new DomainException("El máximo de asientos por comprador debe ser mayor a cero.");

        MinutosRetencion = minutosRetencion;
        MaximoAsientosPorComprador = maximoAsientosPorComprador;
    }
}
