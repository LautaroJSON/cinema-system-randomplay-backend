namespace CinemaSystemRandomPlay.Application.Funciones.Dtos;

public record HorarioFuncionDto(
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid SalaId,
    string SalaNombre);
