using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Carritos;

/// <summary>
/// Derecho a un asiento de una función, emitido al pagar el carrito. Copia los datos de la función y el precio unitario
/// congelado del carrito.
/// </summary>
public class Entrada
{
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public Guid CarritoId { get; private set; }
    public Guid FuncionId { get; private set; }
    public char Fila { get; private set; }
    public int Numero { get; private set; }
    public Asiento Asiento => new(Fila, Numero);
    public string PeliculaTitulo { get; private set; } = string.Empty;
    public string SucursalNombre { get; private set; } = string.Empty;
    public string SalaNombre { get; private set; } = string.Empty;
    public DateTimeOffset HoraInicio { get; private set; }
    public Dinero PrecioUnitario { get; private set; } = null!;
    public DateTimeOffset EmitidaEn { get; private set; }

    private Entrada()
    {
    }

    internal Entrada(Guid id, string codigo, Guid carritoId, Asiento asiento, DatosDeLaFuncion funcion, Dinero precioUnitario,
        DateTimeOffset emitidaEn)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new DomainException("La Entrada debe tener un código.");

        if (string.IsNullOrWhiteSpace(funcion.PeliculaTitulo) || string.IsNullOrWhiteSpace(funcion.SucursalNombre) ||
            string.IsNullOrWhiteSpace(funcion.SalaNombre))
            throw new DomainException("La Entrada debe tener película, sucursal y sala.");

        Id = id;
        Codigo = codigo;
        CarritoId = carritoId;
        FuncionId = funcion.FuncionId;
        ArgumentNullException.ThrowIfNull(asiento);
        Fila = asiento.Fila;
        Numero = asiento.Numero;
        PeliculaTitulo = funcion.PeliculaTitulo;
        SucursalNombre = funcion.SucursalNombre;
        SalaNombre = funcion.SalaNombre;
        HoraInicio = funcion.HoraInicio;
        PrecioUnitario = precioUnitario ?? throw new DomainException("La Entrada debe tener un precio.");
        EmitidaEn = emitidaEn;
    }
}
