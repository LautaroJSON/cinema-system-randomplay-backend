namespace CinemaSystemRandomPlay.Application.Compartido.Ports;

/// <summary>
/// Procesa un tipo de mensaje del Outbox fuera de la transacción que lo registró. La entrega es at-least-once: el manejador
/// debe tolerar ejecutarse más de una vez para el mismo mensaje.
/// </summary>
public interface IManejadorDeMensaje
{
    /// <summary>Nombre del tipo de evento (igual al nombre de la clase del evento registrado en el Outbox).</summary>
    string Tipo { get; }

    /// <summary>Lanza si falla: el Outbox lo reintenta más tarde.</summary>
    Task Manejar(string payload, CancellationToken cancellationToken);

    /// <summary>Se invoca una vez cuando el mensaje agota sus reintentos.</summary>
    Task AlAgotarIntentos(string payload, string error, CancellationToken cancellationToken) => Task.CompletedTask;
}
