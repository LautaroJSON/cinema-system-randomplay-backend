using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Funciones;

public class Sala
{
    private readonly List<FilaSala> _filas = new();

    public Guid Id { get; private set; }
    public Guid SucursalId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;

    public Sucursal? Sucursal { get; private set; }

    public IReadOnlyList<FilaSala> Filas => _filas.OrderBy(f => f.Letra).ToList();

    public int TotalAsientos => _filas.Sum(f => f.CantidadAsientos);

    private Sala()
    {
    }

    public Sala(Guid id, Guid sucursalId, string nombre, IEnumerable<FilaSala> filas)
    {
        if (sucursalId == Guid.Empty)
            throw new DomainException("La Sala debe pertenecer a una Sucursal.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El nombre de la Sala no puede estar vacío.");

        if (filas is null)
            throw new DomainException("La Sala debe tener al menos una fila.");

        var listaFilas = filas.ToList();

        if (listaFilas.Count == 0)
            throw new DomainException("La Sala debe tener al menos una fila.");

        if (listaFilas.Select(f => f.Letra).Distinct().Count() != listaFilas.Count)
            throw new DomainException("La Sala no puede tener dos filas con la misma letra.");

        Id = id;
        SucursalId = sucursalId;
        Nombre = nombre;
        _filas.AddRange(listaFilas);
    }

    /// <summary>
    /// Devuelve, por cada fila en orden alfabético, los números de asiento que no están ocupados.
    /// Los ocupados que no pertenecen a esta Sala se ignoran.
    /// </summary>
    public IReadOnlyList<FilaDisponibilidad> AsientosDisponibles(IEnumerable<Asiento> ocupados)
    {
        var ocupadosSet = ocupados.ToHashSet();

        return Filas
            .Select(fila => new FilaDisponibilidad(
                fila.Letra,
                fila.CantidadAsientos,
                Enumerable.Range(1, fila.CantidadAsientos)
                    .Where(numero => !ocupadosSet.Contains(new Asiento(fila.Letra, numero)))
                    .ToList()))
            .ToList();
    }
}
