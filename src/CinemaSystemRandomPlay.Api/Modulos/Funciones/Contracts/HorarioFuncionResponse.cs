using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Application.Funciones.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;

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
