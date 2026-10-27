using System.Security.Claims;
using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Api.Autenticacion;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Comprador de la credencial: el claim <c>email</c>, normalizado. <c>null</c> si falta o no es un email válido.
    /// La identidad sale únicamente de la credencial, nunca del pedido.
    /// </summary>
    public static EmailComprador? ObtenerComprador(this ClaimsPrincipal usuario)
    {
        var email = usuario.FindFirst("email")?.Value;

        return EmailComprador.TryCrear(email, out var comprador) ? comprador : null;
    }
}
