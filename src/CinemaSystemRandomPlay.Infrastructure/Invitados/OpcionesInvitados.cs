namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>Configuración de la sección <c>Invitados</c>: firma y vida del token de acceso al carrito del comprador anónimo.</summary>
public class OpcionesInvitados
{
    public const string Seccion = "Invitados";
    public const string Emisor = "cinema-api-invitados";
    public const string Audiencia = "cinema-api-invitados";
    public const string TipoToken = "portador-carrito";
    public const string ClaimCarrito = "carrito";
    public const int LargoMinimoClave = 32;

    public string ClaveFirma { get; set; } = string.Empty;
    public int TokenMinutosVigencia { get; set; } = 60;
}
