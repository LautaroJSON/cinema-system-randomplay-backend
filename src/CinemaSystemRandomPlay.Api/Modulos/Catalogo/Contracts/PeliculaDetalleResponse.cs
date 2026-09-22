using CinemaSystemRandomPlay.Application.Catalogo.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Catalogo.Contracts;

public record PeliculaDetalleResponse(
    Guid Id,
    string Titulo,
    int DuracionMinutos,
    string Clasificacion,
    string Sinopsis,
    bool SinFuncionesDisponibles)
{
    public static PeliculaDetalleResponse DesdeDto(PeliculaDetalleDto dto) =>
        new(dto.Id, dto.Titulo, dto.DuracionMinutos, dto.Clasificacion, dto.Sinopsis, dto.SinFuncionesDisponibles);
}
