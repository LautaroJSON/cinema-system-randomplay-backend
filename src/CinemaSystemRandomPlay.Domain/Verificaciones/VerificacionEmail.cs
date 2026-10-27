using System.Security.Cryptography;
using System.Text;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Domain.Verificaciones;

/// <summary>
/// Intento de probar que alguien controla un email. El código nunca se guarda: solo su hash. Un código se usa una
/// sola vez, vence, y se invalida al agotar los intentos fallidos o cuando se pide uno nuevo para el mismo email.
/// </summary>
public class VerificacionEmail
{
    public Guid Id { get; private set; }
    public EmailComprador Email { get; private set; } = null!;
    public string CodigoHash { get; private set; } = string.Empty;
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public int IntentosFallidos { get; private set; }

    /// <summary>Estado guardado: nunca es <see cref="EstadoVerificacion.Vencida"/>. Usar <see cref="EstadoEn"/>.</summary>
    public EstadoVerificacion Estado { get; private set; }

    private VerificacionEmail()
    {
    }

    public static VerificacionEmail Crear(EmailComprador email, string codigoHash, DateTimeOffset ahora, int minutosVigencia)
    {
        if (email is null)
            throw new DomainException("La Verificacion debe tener un email.");

        if (string.IsNullOrWhiteSpace(codigoHash))
            throw new DomainException("La Verificacion debe tener el hash de su código.");

        if (minutosVigencia <= 0)
            throw new DomainException("Los minutos de vigencia del código deben ser mayores a cero.");

        return new VerificacionEmail
        {
            Id = Guid.NewGuid(),
            Email = email,
            CodigoHash = codigoHash,
            CreadaEn = ahora,
            VenceEn = ahora.AddMinutes(minutosVigencia),
            IntentosFallidos = 0,
            Estado = EstadoVerificacion.Vigente
        };
    }

    public EstadoVerificacion EstadoEn(DateTimeOffset ahora) =>
        Estado == EstadoVerificacion.Vigente && VenceEn <= ahora ? EstadoVerificacion.Vencida : Estado;

    /// <summary>Pasa una verificación vigente a invalidada (se pidió un código nuevo). No afecta a las demás.</summary>
    public void Invalidar()
    {
        if (Estado == EstadoVerificacion.Vigente)
            Estado = EstadoVerificacion.Invalidada;
    }

    /// <summary>
    /// Compara el hash del código ingresado. Un código correcto se consume. Cada intento incorrecto suma un fallo y, al
    /// llegar al máximo, invalida la verificación: ya no acepta ni el código correcto.
    /// </summary>
    public ResultadoVerificacion Verificar(string hashIngresado, DateTimeOffset ahora, int maximoIntentosFallidos)
    {
        switch (EstadoEn(ahora))
        {
            case EstadoVerificacion.Usada:
                return ResultadoVerificacion.YaUsado;
            case EstadoVerificacion.Invalidada:
                return ResultadoVerificacion.Invalidado;
            case EstadoVerificacion.Vencida:
                return ResultadoVerificacion.Vencido;
        }

        if (HashCoincide(hashIngresado))
        {
            Estado = EstadoVerificacion.Usada;
            return ResultadoVerificacion.Correcto;
        }

        IntentosFallidos++;

        if (IntentosFallidos >= maximoIntentosFallidos)
        {
            Estado = EstadoVerificacion.Invalidada;
            return ResultadoVerificacion.Invalidado;
        }

        return ResultadoVerificacion.Incorrecto;
    }

    private bool HashCoincide(string hashIngresado) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(CodigoHash), Encoding.UTF8.GetBytes(hashIngresado ?? string.Empty));
}
