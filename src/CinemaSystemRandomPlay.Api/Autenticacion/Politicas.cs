namespace CinemaSystemRandomPlay.Api.Autenticacion;

public static class Politicas
{
    /// <summary>Comprador invitado con una credencial de email verificado (esquema <see cref="EsquemasDeAutenticacion.Invitado"/>).</summary>
    public const string InvitadoVerificado = "InvitadoVerificado";

    /// <summary>Personal interno autenticado por el proveedor OIDC con rol <c>CinemaManager</c> o <c>Admin</c>.</summary>
    public const string Personal = "Personal";

    public const string RolCinemaManager = "CinemaManager";
    public const string RolAdmin = "Admin";
}
