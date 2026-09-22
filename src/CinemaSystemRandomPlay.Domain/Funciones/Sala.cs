using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Funciones;

public class Sala
{
    public Guid Id { get; private set; }
    public Guid SucursalId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;

    private Sala()
    {
    }

    public Sala(Guid id, Guid sucursalId, string nombre)
    {
        if (sucursalId == Guid.Empty)
            throw new DomainException("La Sala debe pertenecer a una Sucursal.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El nombre de la Sala no puede estar vacío.");

        Id = id;
        SucursalId = sucursalId;
        Nombre = nombre;
    }
}
