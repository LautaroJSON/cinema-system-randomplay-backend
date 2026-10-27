using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CinemaSystemRandomPlay.IntegrationTests.Compartido;

/// <summary>Emite JWT firmados con una clave simétrica de test, que la factory configura como la válida.</summary>
public static class TokensDePrueba
{
    public const string Emisor = "https://identidad.pruebas";
    public const string Audiencia = "cinema-api-pruebas";
    public const string ClaimRoles = "roles";

    public static readonly SymmetricSecurityKey Clave = new(Encoding.UTF8.GetBytes("clave-de-pruebas-de-al-menos-32-bytes-0123456789"));

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

    public static string Customer(string sub) => Crear(sub, ["Customer"]);
}
