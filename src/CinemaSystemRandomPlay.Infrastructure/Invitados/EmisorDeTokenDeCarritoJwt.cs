using System.Text;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>
/// Emite el token de acceso al carrito como un JWT HS256 firmado con <c>Invitados:ClaveFirma</c>. Lo valida el esquema de
/// autenticación <c>Invitado</c> de la Api; no se guarda ningún token (specs/007-carrito-anonimo/research.md, Decisión 1).
/// </summary>
public class EmisorDeTokenDeCarritoJwt : IEmisorDeTokenDeCarrito
{
    private readonly OpcionesInvitados _opciones;
    private readonly SigningCredentials _firma;
    private readonly JsonWebTokenHandler _handler = new();

    public EmisorDeTokenDeCarritoJwt(OpcionesInvitados opciones)
    {
        _opciones = opciones;
        _firma = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.ClaveFirma)), SecurityAlgorithms.HmacSha256);
    }

    public TokenDeCarrito Emitir(Guid carritoId, DateTimeOffset ahora)
    {
        var venceEn = ahora.AddMinutes(_opciones.TokenMinutosVigencia);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = OpcionesInvitados.Emisor,
            Audience = OpcionesInvitados.Audiencia,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = carritoId.ToString(),
                [OpcionesInvitados.ClaimCarrito] = carritoId.ToString(),
                ["tipo"] = OpcionesInvitados.TipoToken,
                ["jti"] = Guid.NewGuid().ToString()
            },
            IssuedAt = ahora.UtcDateTime,
            Expires = venceEn.UtcDateTime,
            SigningCredentials = _firma
        });

        return new TokenDeCarrito(token, venceEn);
    }
}
