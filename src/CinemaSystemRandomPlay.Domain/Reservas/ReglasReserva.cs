using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Reservas;

public sealed record ReglasReserva
{
    public int MinutosRetencion { get; }
    public int MaximoAsientosPorCliente { get; }

    public ReglasReserva(int minutosRetencion = 10, int maximoAsientosPorCliente = 10)
    {
        if (minutosRetencion <= 0)
            throw new DomainException("Los minutos de retención deben ser mayores a cero.");

        if (maximoAsientosPorCliente <= 0)
            throw new DomainException("El máximo de asientos por Cliente debe ser mayor a cero.");

        MinutosRetencion = minutosRetencion;
        MaximoAsientosPorCliente = maximoAsientosPorCliente;
    }
}
