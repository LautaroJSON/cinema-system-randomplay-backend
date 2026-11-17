namespace CinemaSystemRandomPlay.Infrastructure.Email;

public enum ProveedorEmail
{
    /// <summary>Solo registra destinatario y asunto en el log (Development y Testing).</summary>
    Registrador,

    /// <summary>Servidor SMTP: Mailpit en Development; Resend u otro proveedor por SMTP en el resto.</summary>
    Smtp
}

public class OpcionesEmail
{
    public const string Seccion = "Email";

    public ProveedorEmail Proveedor { get; set; } = ProveedorEmail.Registrador;

    public string Remitente { get; set; } = "entradas@cinema.local";

    public string NombreRemitente { get; set; } = "Cinema RandomPlay";

    public OpcionesSmtp Smtp { get; set; } = new();
}

/// <summary><see cref="Clave"/> es un secreto: solo por user-secrets o variables de entorno.</summary>
public class OpcionesSmtp
{
    public string Host { get; set; } = string.Empty;

    public int Puerto { get; set; } = 587;

    public string? Usuario { get; set; }

    public string? Clave { get; set; }

    /// <summary>TLS implícito en el puerto 465 y STARTTLS en el resto; <c>false</c> para Mailpit.</summary>
    public bool UsarTls { get; set; } = true;
}
