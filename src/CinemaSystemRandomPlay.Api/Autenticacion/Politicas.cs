namespace CinemaSystemRandomPlay.Api.Autenticacion;

public static class Politicas
{
    /// <summary>Portador del token de un carrito (esquema <see cref="EsquemasDeAutenticacion.Invitado"/>).</summary>
    public const string PortadorDeCarrito = "PortadorDeCarrito";

    /// <summary>Personal interno autenticado por el proveedor OIDC con rol <c>CinemaManager</c> o <c>Admin</c>.</summary>
    public const string Personal = "Personal";

    public const string RolCinemaManager = "CinemaManager";
    public const string RolAdmin = "Admin";
}
