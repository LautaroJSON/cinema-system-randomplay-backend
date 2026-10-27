using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Application.Compartido.Dtos;

public record DineroDto(decimal Monto, string Moneda)
{
    public static DineroDto DesdeDominio(Dinero dinero) => new(dinero.Monto, dinero.Moneda.ToString());
}
