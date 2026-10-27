namespace CinemaSystemRandomPlay.Application.Compartido.Ports;

public interface IOutbox
{
    /// <summary>
    /// Agrega el evento al outbox. Se persiste junto con el resto de los cambios de la transacción en curso.
    /// </summary>
    void Registrar(object evento);
}
