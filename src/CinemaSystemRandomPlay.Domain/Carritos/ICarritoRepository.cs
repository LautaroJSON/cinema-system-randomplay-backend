namespace CinemaSystemRandomPlay.Domain.Carritos;

public interface ICarritoRepository
{
    Task<IReadOnlyList<Carrito>> ListarActivosDeFuncion(Guid funcionId, DateTimeOffset ahora, CancellationToken cancellationToken = default);

    Task<Carrito?> ObtenerPorId(Guid carritoId, CancellationToken cancellationToken = default);

    /// <summary>Función del carrito, sin seguimiento: sirve para tomar el bloqueo de la función antes de cargar el carrito.</summary>
    Task<Guid?> ObtenerFuncionId(Guid carritoId, CancellationToken cancellationToken = default);

    Task Agregar(Carrito carrito, CancellationToken cancellationToken = default);
}
