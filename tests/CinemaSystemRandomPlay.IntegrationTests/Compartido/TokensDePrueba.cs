using System.Text;
using CinemaSystemRandomPlay.Infrastructure.Invitados;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CinemaSystemRandomPlay.IntegrationTests.Compartido;

/// <summary>
/// Emite JWT firmados con claves simétricas de test. <see cref="Crear"/> imita un token del personal (esquema OIDC);
/// <see cref="Carrito"/> imita el token de carrito que emite la API (la factory configura
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

    /// <summary>
    /// Token de carrito con la forma del que emite la API. Sirve para probar tokens alterados: vencidos, de otra clave u otro
    /// emisor, con otro tipo o sin el claim del carrito. Los tokens válidos de los tests salen de la respuesta de creación.
    /// </summary>
    public static string Carrito(
        Guid carritoId,
        bool vencido = false,
        string? claveFirma = null,
        string emisor = OpcionesInvitados.Emisor,
        string tipo = OpcionesInvitados.TipoToken,
        bool conClaimCarrito = true)
    {
        var ahora = DateTime.UtcNow;

        var claims = new Dictionary<string, object> { ["sub"] = carritoId.ToString(), ["tipo"] = tipo };
        if (conClaimCarrito)
            claims[OpcionesInvitados.ClaimCarrito] = carritoId.ToString();

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
