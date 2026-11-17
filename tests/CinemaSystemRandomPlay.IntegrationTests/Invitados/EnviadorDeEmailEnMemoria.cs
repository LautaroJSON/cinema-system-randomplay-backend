using System.Collections.Concurrent;
using CinemaSystemRandomPlay.Application.Compartido.Ports;

namespace CinemaSystemRandomPlay.IntegrationTests.Invitados;

/// <summary>Doble de <see cref="IEnviadorDeEmail"/> que guarda los mensajes y puede simular fallas del servicio de email.</summary>
public sealed class EnviadorDeEmailEnMemoria : IEnviadorDeEmail
{
    private readonly ConcurrentQueue<MensajeDeEmail> _mensajes = new();
    private int _fallasPendientes;

    public IReadOnlyList<MensajeDeEmail> Mensajes => _mensajes.ToList();

    /// <summary>Los próximos <paramref name="cantidad"/> envíos fallan.</summary>
    public void FallarProximosEnvios(int cantidad) => Interlocked.Exchange(ref _fallasPendientes, cantidad);

    public void Limpiar()
    {
        _mensajes.Clear();
        Interlocked.Exchange(ref _fallasPendientes, 0);
    }

    public Task Enviar(MensajeDeEmail mensaje, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref _fallasPendientes) >= 0)
            throw new InvalidOperationException("Servicio de email simulado no disponible.");

        Interlocked.Exchange(ref _fallasPendientes, 0);
        _mensajes.Enqueue(mensaje);
        return Task.CompletedTask;
    }
}
