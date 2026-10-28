using CinemaSystemRandomPlay.Application.Catalogo.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Catalogo.Contracts;

/// <param name="DuracionMinutos">Duración en minutos.</param>
/// <param name="Clasificacion">Calificación por edad: <c>ATP</c>, <c>Mas13</c>, <c>Mas16</c> o <c>Mas18</c>.</param>
/// <param name="Sinopsis">Resumen del argumento.</param>
/// <param name="SinFuncionesDisponibles"><c>true</c> si hoy no quedan funciones de la película.</param>
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
