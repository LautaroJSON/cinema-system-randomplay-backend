using System.Security.Cryptography;
using System.Text;
using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>
/// Códigos de 6 dígitos generados con un RNG criptográfico. Solo se guarda el HMAC-SHA256 del par email + código, con una
/// clave derivada de <c>ClaveFirma</c> distinta de la que firma las credenciales.
/// </summary>
public class CodigosDeVerificacionHmac : ICodigosDeVerificacion
{
    private const string ContextoDeLaClave = "cinema-api/codigo-de-verificacion/v1";

    private readonly byte[] _clave;

    public CodigosDeVerificacionHmac(OpcionesInvitados opciones)
    {
        // Derivar con un contexto propio evita usar la misma clave para firmar credenciales y para hashear códigos.
        _clave = HMACSHA256.HashData(Encoding.UTF8.GetBytes(opciones.ClaveFirma), Encoding.UTF8.GetBytes(ContextoDeLaClave));
    }

    public string Generar() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public string Hashear(EmailComprador email, string? codigo)
    {
        var datos = Encoding.UTF8.GetBytes($"{email.Valor}:{codigo ?? string.Empty}");

        return Convert.ToBase64String(HMACSHA256.HashData(_clave, datos));
    }
}
