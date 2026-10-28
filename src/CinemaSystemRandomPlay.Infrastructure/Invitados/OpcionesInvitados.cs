namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>Configuración de la sección <c>Invitados</c>: verificación del email y credencial del comprador invitado.</summary>
public class OpcionesInvitados
{
    public const string Seccion = "Invitados";
    public const string Emisor = "cinema-api-invitados";
    public const string Audiencia = "cinema-api-invitados";
    public const string TipoCredencial = "invitado-verificado";
    public const int LargoMinimoClave = 32;

    public string ClaveFirma { get; set; } = string.Empty;
    public int CodigoMinutosVigencia { get; set; } = 10;
    public int MaximoIntentosFallidos { get; set; } = 5;
    public int CredencialMinutosVigencia { get; set; } = 60;
    public int SolicitudesPorEmail { get; set; } = 5;
    public int SolicitudesPorOrigen { get; set; } = 20;
    public int VentanaSolicitudesMinutos { get; set; } = 60;
}
