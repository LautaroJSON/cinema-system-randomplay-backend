using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Application.Funciones.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;

/// <param name="FuncionId">Función; se usa para pedir el mapa de asientos y crear el carrito.</param>
/// <param name="HoraInicio">Inicio de la función.</param>
/// <param name="PrecioEntrada">Precio de una entrada.</param>
public record HorarioFuncionResponse(
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid SalaId,
    string SalaNombre,
    DineroResponse PrecioEntrada)
{
    public static HorarioFuncionResponse DesdeDto(HorarioFuncionDto dto) =>
        new(dto.FuncionId, dto.HoraInicio, dto.SalaId, dto.SalaNombre, DineroResponse.DesdeDto(dto.PrecioEntrada));
}
