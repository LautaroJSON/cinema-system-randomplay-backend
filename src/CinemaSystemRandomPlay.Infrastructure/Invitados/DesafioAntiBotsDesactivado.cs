using CinemaSystemRandomPlay.Application.Carritos.Ports;

namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>
/// Implementación por defecto del punto de extensión anti-bots: aprueba siempre. Para sumar un CAPTCHA, registrar otra
/// implementación de <see cref="IDesafioAntiBots"/> en Program.cs.
/// </summary>
public class DesafioAntiBotsDesactivado : IDesafioAntiBots
{
    public Task<bool> Verificar(string? respuesta, string? origen, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
