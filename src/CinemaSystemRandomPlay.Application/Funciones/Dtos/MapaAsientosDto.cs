namespace CinemaSystemRandomPlay.Application.Funciones.Dtos;

public record MapaAsientosDto(
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid PeliculaId,
    string PeliculaTitulo,
    Guid SucursalId,
    string SucursalNombre,
    Guid SalaId,
    string SalaNombre,
    int TotalAsientos,
    int CantidadDisponibles,
    IReadOnlyList<FilaMapaDto> Filas);

public record FilaMapaDto(
    string Fila,
    int CantidadAsientos,
    IReadOnlyList<int> AsientosDisponibles);
