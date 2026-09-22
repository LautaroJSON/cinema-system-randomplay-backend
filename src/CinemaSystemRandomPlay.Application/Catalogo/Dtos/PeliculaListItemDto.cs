namespace CinemaSystemRandomPlay.Application.Catalogo.Dtos;

public record PeliculaListItemDto(
    Guid Id,
    string Titulo,
    int DuracionMinutos,
    string Clasificacion,
    bool SinFuncionesDisponibles);
