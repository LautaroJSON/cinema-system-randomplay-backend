using CinemaSystemRandomPlay.Application.Verificaciones.Ports;

namespace CinemaSystemRandomPlay.Api.Modulos.Verificaciones.Contracts;

public record CredencialResponse(string Credencial, string Tipo, DateTimeOffset VenceEn)
{
    public static CredencialResponse DesdeCredencial(CredencialEmitida credencial) =>
        new(credencial.Credencial, "Bearer", credencial.VenceEn);
}
