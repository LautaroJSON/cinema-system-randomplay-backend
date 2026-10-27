using System.Text;
using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using CinemaSystemRandomPlay.Domain.Compradores;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>
/// Emite la credencial del comprador invitado como un JWT HS256 firmado con <c>Invitados:ClaveFirma</c>. La valida el
/// esquema de autenticación <c>Invitado</c> de la Api; no se guarda ninguna credencial (research.md, Decisión 1).
/// </summary>
public class EmisorDeCredencialJwt : IEmisorDeCredencial
{
    private readonly OpcionesInvitados _opciones;
    private readonly SigningCredentials _firma;
    private readonly JsonWebTokenHandler _handler = new();

    public EmisorDeCredencialJwt(OpcionesInvitados opciones)
    {
        _opciones = opciones;
        _firma = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.ClaveFirma)), SecurityAlgorithms.HmacSha256);
    }

    public CredencialEmitida Emitir(EmailComprador email, DateTimeOffset ahora)
    {
        var venceEn = ahora.AddMinutes(_opciones.CredencialMinutosVigencia);

        var credencial = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = OpcionesInvitados.Emisor,
            Audience = OpcionesInvitados.Audiencia,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = email.Valor,
                ["email"] = email.Valor,
                ["tipo"] = OpcionesInvitados.TipoCredencial
            },
            IssuedAt = ahora.UtcDateTime,
            Expires = venceEn.UtcDateTime,
            SigningCredentials = _firma
        });

        return new CredencialEmitida(credencial, venceEn);
    }
}
