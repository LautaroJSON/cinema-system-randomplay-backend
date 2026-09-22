namespace CinemaSystemRandomPlay.Application.Catalogo.Dtos;

public record PeliculaDetalleDto(
    Guid Id,
    string Titulo,
    int DuracionMinutos,
    string Clasificacion,
    string Sinopsis,
    bool SinFuncionesDisponibles);
