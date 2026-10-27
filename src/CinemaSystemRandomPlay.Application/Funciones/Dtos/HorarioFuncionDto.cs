using CinemaSystemRandomPlay.Application.Compartido.Dtos;

namespace CinemaSystemRandomPlay.Application.Funciones.Dtos;

public record HorarioFuncionDto(
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid SalaId,
    string SalaNombre,
    DineroDto PrecioEntrada);
