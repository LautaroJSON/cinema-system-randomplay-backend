using CinemaSystemRandomPlay.Application.Catalogo.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Catalogo.Contracts;

public record PeliculaListItemResponse(
    Guid Id,
    string Titulo,
    int DuracionMinutos,
    string Clasificacion,
    bool SinFuncionesDisponibles)
{
    public static PeliculaListItemResponse DesdeDto(PeliculaListItemDto dto) =>
        new(dto.Id, dto.Titulo, dto.DuracionMinutos, dto.Clasificacion, dto.SinFuncionesDisponibles);
}
