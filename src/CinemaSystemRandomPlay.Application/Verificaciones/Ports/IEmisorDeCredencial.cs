using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Application.Verificaciones.Ports;

public record CredencialEmitida(string Credencial, DateTimeOffset VenceEn);

public interface IEmisorDeCredencial
{
    /// <summary>Emite la credencial que prueba que quien la porta verificó <paramref name="email"/>, vigente desde <paramref name="ahora"/>.</summary>
    CredencialEmitida Emitir(EmailComprador email, DateTimeOffset ahora);
}
