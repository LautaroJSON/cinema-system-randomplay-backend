using System.Globalization;
using CinemaSystemRandomPlay.Application.Compartido.Dtos;

namespace CinemaSystemRandomPlay.Api.Compartido;

/// <summary>
/// Monto para el cliente externo: texto con exactamente 2 decimales y punto decimal, nunca un número.
/// </summary>
public record DineroResponse(string Monto, string Moneda)
{
    public static DineroResponse DesdeDto(DineroDto dto) =>
        new(dto.Monto.ToString("0.00", CultureInfo.InvariantCulture), dto.Moneda);
}
