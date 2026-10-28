using System.Security.Claims;
using CinemaSystemRandomPlay.Infrastructure.Invitados;

namespace CinemaSystemRandomPlay.Api.Autenticacion;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Carrito al que da acceso el token: el claim <c>carrito</c>. <c>null</c> si falta o no es un Guid.
    /// El acceso sale únicamente del token, nunca del pedido.
    /// </summary>
    public static Guid? ObtenerCarritoId(this ClaimsPrincipal usuario) =>
        Guid.TryParse(usuario.FindFirst(OpcionesInvitados.ClaimCarrito)?.Value, out var carritoId) ? carritoId : null;
}
