using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Funciones;

public class Funcion
{
    public Guid Id { get; private set; }
    public Guid PeliculaId { get; private set; }
    public Guid SalaId { get; private set; }
    public DateTimeOffset FechaHoraInicio { get; private set; }

    public Sala? Sala { get; private set; }

    private Funcion()
    {
    }

    public Funcion(Guid id, Guid peliculaId, Guid salaId, DateTimeOffset fechaHoraInicio)
    {
        if (peliculaId == Guid.Empty)
            throw new DomainException("La Funcion debe tener una Pelicula asociada.");

        if (salaId == Guid.Empty)
            throw new DomainException("La Funcion debe tener una Sala asociada.");

        Id = id;
        PeliculaId = peliculaId;
        SalaId = salaId;
        FechaHoraInicio = fechaHoraInicio;
    }
}
