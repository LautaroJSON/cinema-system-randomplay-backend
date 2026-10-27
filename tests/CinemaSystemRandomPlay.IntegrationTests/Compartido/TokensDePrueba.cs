using System.Text;
using CinemaSystemRandomPlay.Infrastructure.Invitados;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CinemaSystemRandomPlay.IntegrationTests.Compartido;

/// <summary>
/// Emite JWT firmados con claves simétricas de test. <see cref="Crear"/> imita un token del personal (esquema OIDC);
/// <see cref="Invitado"/> imita la credencial de comprador invitado que emite la API (la factory configura
/// <see cref="ClaveFirmaDePrueba"/> como <c>Invitados:ClaveFirma</c>).
/// </summary>
public static class TokensDePrueba
{
    public const string Emisor = "https://identidad.pruebas";
    public const string Audiencia = "cinema-api-pruebas";
    public const string ClaimRoles = "roles";

    public static readonly SymmetricSecurityKey Clave = new(Encoding.UTF8.GetBytes("clave-de-pruebas-de-al-menos-32-bytes-0123456789"));

    public const string ClaveFirmaDePrueba = "clave-de-firma-de-invitados-de-pruebas-0123456789";

    private static readonly JsonWebTokenHandler Handler = new();

    public static string Crear(string sub, string[]? roles = null, bool vencido = false, string emisor = Emisor, SecurityKey? clave = null)
    {
        var ahora = DateTime.UtcNow;

        var claims = new Dictionary<string, object> { ["sub"] = sub };
        if (roles is { Length: > 0 })
            claims[ClaimRoles] = roles;

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emisor,
            Audience = Audiencia,
            Claims = claims,
            NotBefore = vencido ? ahora.AddHours(-2) : ahora.AddMinutes(-1),
            Expires = vencido ? ahora.AddHours(-1) : ahora.AddHours(1),
            SigningCredentials = new SigningCredentials(clave ?? Clave, SecurityAlgorithms.HmacSha256)
        });
    }

    /// <summary>Credencial de invitado. <paramref name="email"/> se pone tal cual en el claim: la API debe normalizarlo.</summary>
    public static string Invitado(
        string email,
        bool vencido = false,
        string? claveFirma = null,
        string emisor = OpcionesInvitados.Emisor,
        string tipo = OpcionesInvitados.TipoCredencial,
        bool conEmail = true)
    {
        var ahora = DateTime.UtcNow;

        var claims = new Dictionary<string, object> { ["sub"] = email, ["tipo"] = tipo };
        if (conEmail)
            claims["email"] = email;

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emisor,
            Audience = OpcionesInvitados.Audiencia,
            Claims = claims,
            NotBefore = vencido ? ahora.AddHours(-2) : ahora.AddMinutes(-1),
            Expires = vencido ? ahora.AddHours(-1) : ahora.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveFirma ?? ClaveFirmaDePrueba)), SecurityAlgorithms.HmacSha256)
        });
    }
}
