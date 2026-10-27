using System.Collections.Concurrent;
using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.IntegrationTests.Invitados;

/// <summary>Doble de <see cref="IEnviadorDeEmail"/> que guarda los mensajes para que los tests lean el código.</summary>
public sealed class EnviadorDeEmailEnMemoria : IEnviadorDeEmail
{
    private readonly ConcurrentQueue<MensajeDeVerificacion> _mensajes = new();

    public IReadOnlyList<MensajeDeVerificacion> Mensajes => _mensajes.ToList();

    public Task Enviar(MensajeDeVerificacion mensaje, CancellationToken cancellationToken = default)
    {
        _mensajes.Enqueue(mensaje);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> CodigosDe(string email)
    {
        var comprador = EmailComprador.Crear(email);
        return _mensajes.Where(m => m.Destinatario == comprador).Select(m => m.Codigo).ToList();
    }

    public string UltimoCodigoDe(string email) =>
        CodigosDe(email).LastOrDefault() ?? throw new InvalidOperationException($"No se envió ningún código a {email}.");

    public int CantidadDe(string email) => CodigosDe(email).Count;
}
