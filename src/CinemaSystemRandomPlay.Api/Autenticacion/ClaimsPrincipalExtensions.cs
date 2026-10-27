using System.Security.Claims;

namespace CinemaSystemRandomPlay.Api.Autenticacion;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Identificador del Cliente: el claim <c>sub</c> del token. <c>null</c> si el token no lo trae.</summary>
    public static string? ObtenerClienteId(this ClaimsPrincipal usuario)
    {
        var sub = usuario.FindFirst("sub")?.Value ?? usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(sub) ? null : sub;
    }
}
