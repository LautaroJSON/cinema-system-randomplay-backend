using CinemaSystemRandomPlay.Application.Funciones.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;

public record MapaAsientosResponse(
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
    IReadOnlyList<FilaMapaResponse> Filas)
{
    public static MapaAsientosResponse DesdeDto(MapaAsientosDto dto) =>
        new(dto.FuncionId, dto.HoraInicio, dto.PeliculaId, dto.PeliculaTitulo, dto.SucursalId, dto.SucursalNombre,
            dto.SalaId, dto.SalaNombre, dto.TotalAsientos, dto.CantidadDisponibles, dto.Filas.Select(FilaMapaResponse.DesdeDto).ToList());
}

public record FilaMapaResponse(
    string Fila,
    int CantidadAsientos,
    IReadOnlyList<int> AsientosDisponibles)
{
    public static FilaMapaResponse DesdeDto(FilaMapaDto dto) =>
        new(dto.Fila, dto.CantidadAsientos, dto.AsientosDisponibles);
}
