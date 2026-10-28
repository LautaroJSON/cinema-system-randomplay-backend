using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Domain.Verificaciones;

public interface IVerificacionEmailRepository
{
    /// <summary>
    /// Serializa las operaciones sobre un mismo email hasta el fin de la transacción en curso. Debe llamarse antes de
    /// contar, leer o escribir verificaciones de ese email.
    /// </summary>
    Task BloquearPorEmail(EmailComprador email, CancellationToken cancellationToken = default);

    /// <summary>La verificación guardada como vigente del email (puede estar vencida contra el reloj), o <c>null</c>.</summary>
    Task<VerificacionEmail?> ObtenerVigente(EmailComprador email, CancellationToken cancellationToken = default);

    /// <summary>Cantidad de verificaciones creadas para el email desde <paramref name="desde"/>, sea cual sea su estado.</summary>
    Task<int> ContarDesde(EmailComprador email, DateTimeOffset desde, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega la verificación. Antes persiste los cambios pendientes de la transacción (por ejemplo, la invalidación de la
    /// vigente anterior), para que el índice único de "una sola vigente por email" nunca vea dos a la vez.
    /// </summary>
    Task Agregar(VerificacionEmail verificacion, CancellationToken cancellationToken = default);
}
