using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Funciones;

public class Sucursal
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;

    private Sucursal()
    {
    }

    public Sucursal(Guid id, string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El nombre de la Sucursal no puede estar vacío.");

        Id = id;
        Nombre = nombre;
    }
}
