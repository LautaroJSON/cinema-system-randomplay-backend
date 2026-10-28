namespace CinemaSystemRandomPlay.Domain.Verificaciones;

/// <summary>
/// Estado de una <see cref="VerificacionEmail"/>. <see cref="Vencida"/> nunca se guarda: es el estado efectivo de una
/// verificación vigente cuyo vencimiento ya pasó (se calcula contra el reloj).
/// </summary>
public enum EstadoVerificacion
{
    Vigente,
    Usada,
    Invalidada,
    Vencida
}
