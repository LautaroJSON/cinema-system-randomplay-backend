namespace CinemaSystemRandomPlay.Domain.Carritos;

public interface ICarritoRepository
{
    /// <summary>Carritos de la función que retienen asientos: activos o en pago sin vencer, y pagados.</summary>
    Task<IReadOnlyList<Carrito>> ListarQueRetienenAsientos(Guid funcionId, DateTimeOffset ahora, CancellationToken cancellationToken = default);

    Task<Carrito?> ObtenerPorId(Guid carritoId, CancellationToken cancellationToken = default);

    /// <summary>Función del carrito, sin seguimiento: sirve para tomar el bloqueo de la función antes de cargar el carrito.</summary>
    Task<Guid?> ObtenerFuncionId(Guid carritoId, CancellationToken cancellationToken = default);

    Task Agregar(Carrito carrito, CancellationToken cancellationToken = default);
}
