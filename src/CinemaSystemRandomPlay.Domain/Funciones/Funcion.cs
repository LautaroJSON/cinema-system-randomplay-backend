using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Funciones;

public class Funcion
{
    public Guid Id { get; private set; }
    public Guid PeliculaId { get; private set; }
    public Guid SalaId { get; private set; }
    public DateTimeOffset FechaHoraInicio { get; private set; }
    public Dinero PrecioEntrada { get; private set; } = null!;

    public Sala? Sala { get; private set; }

    private Funcion()
    {
    }

    public Funcion(Guid id, Guid peliculaId, Guid salaId, DateTimeOffset fechaHoraInicio, Dinero precioEntrada)
    {
        if (peliculaId == Guid.Empty)
            throw new DomainException("La Funcion debe tener una Pelicula asociada.");

        if (salaId == Guid.Empty)
            throw new DomainException("La Funcion debe tener una Sala asociada.");

        if (precioEntrada is null || precioEntrada.Monto <= 0 || precioEntrada.Moneda != Moneda.ARS)
            throw new DomainException("La Funcion debe tener un precio por entrada mayor que cero en ARS.");

        Id = id;
        PeliculaId = peliculaId;
        SalaId = salaId;
        FechaHoraInicio = fechaHoraInicio;
        PrecioEntrada = precioEntrada;
    }

    public bool YaComenzo(DateTimeOffset ahora) => FechaHoraInicio < ahora;
}
