using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Application.Funciones.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;

/// <param name="TotalAsientos">Asientos de la sala.</param>
/// <param name="CantidadDisponibles">Asientos que todavía se pueden elegir.</param>
/// <param name="Filas">Filas de la sala en orden.</param>
/// <param name="PrecioEntrada">Precio de una entrada.</param>
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
    IReadOnlyList<FilaMapaResponse> Filas,
    DineroResponse PrecioEntrada)
{
    public static MapaAsientosResponse DesdeDto(MapaAsientosDto dto) =>
        new(dto.FuncionId, dto.HoraInicio, dto.PeliculaId, dto.PeliculaTitulo, dto.SucursalId, dto.SucursalNombre,
            dto.SalaId, dto.SalaNombre, dto.TotalAsientos, dto.CantidadDisponibles, dto.Filas.Select(FilaMapaResponse.DesdeDto).ToList(),
            DineroResponse.DesdeDto(dto.PrecioEntrada));
}

/// <param name="Fila">Letra de la fila.</param>
/// <param name="CantidadAsientos">Asientos de la fila, numerados de 1 a este valor.</param>
/// <param name="AsientosDisponibles">Números de los asientos libres de la fila; los que faltan están ocupados o retenidos.</param>
public record FilaMapaResponse(
    string Fila,
    int CantidadAsientos,
    IReadOnlyList<int> AsientosDisponibles)
{
    public static FilaMapaResponse DesdeDto(FilaMapaDto dto) =>
        new(dto.Fila, dto.CantidadAsientos, dto.AsientosDisponibles);
}
