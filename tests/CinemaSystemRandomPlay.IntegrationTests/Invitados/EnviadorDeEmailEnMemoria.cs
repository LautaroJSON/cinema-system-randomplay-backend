using System.Collections.Concurrent;
using CinemaSystemRandomPlay.Application.Compartido.Ports;

namespace CinemaSystemRandomPlay.IntegrationTests.Invitados;

/// <summary>Doble de <see cref="IEnviadorDeEmail"/> que guarda los mensajes, para comprobar que no se envía nada.</summary>
public sealed class EnviadorDeEmailEnMemoria : IEnviadorDeEmail
{
    private readonly ConcurrentQueue<MensajeDeEmail> _mensajes = new();

    public IReadOnlyList<MensajeDeEmail> Mensajes => _mensajes.ToList();

    public Task Enviar(MensajeDeEmail mensaje, CancellationToken cancellationToken = default)
    {
        _mensajes.Enqueue(mensaje);
        return Task.CompletedTask;
    }
}
