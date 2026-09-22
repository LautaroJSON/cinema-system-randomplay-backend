using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Catalogo;

public class Pelicula
{
    public Guid Id { get; private set; }
    public string Titulo { get; private set; } = string.Empty;
    public int DuracionMinutos { get; private set; }
    public Clasificacion Clasificacion { get; private set; }
    public string Sinopsis { get; private set; } = string.Empty;
    public bool Activa { get; private set; }

    private Pelicula()
    {
    }

    public Pelicula(Guid id, string titulo, int duracionMinutos, Clasificacion clasificacion, string sinopsis, bool activa = true)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new DomainException("El título de la película no puede estar vacío.");

        if (duracionMinutos <= 0)
            throw new DomainException("La duración de la película debe ser mayor a cero.");

        Id = id;
        Titulo = titulo;
        DuracionMinutos = duracionMinutos;
        Clasificacion = clasificacion;
        Sinopsis = sinopsis ?? string.Empty;
        Activa = activa;
    }
}
