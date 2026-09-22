using CinemaSystemRandomPlay.Application.Funciones.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Funciones.Contracts;

public record SucursalConFuncionesHoyResponse(
    Guid Id,
    string Nombre)
{
    public static SucursalConFuncionesHoyResponse DesdeDto(SucursalConFuncionesHoyDto dto) =>
        new(dto.Id, dto.Nombre);
}
