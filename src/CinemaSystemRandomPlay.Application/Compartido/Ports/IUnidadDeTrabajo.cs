namespace CinemaSystemRandomPlay.Application.Compartido.Ports;

public interface IUnidadDeTrabajo
{
    /// <summary>
    /// Abre una transacción, ejecuta <paramref name="operacion"/>, guarda los cambios y confirma.
    /// Si la operación lanza una excepción, deshace todo.
    /// </summary>
    Task<T> EjecutarEnTransaccion<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken cancellationToken = default);
}
