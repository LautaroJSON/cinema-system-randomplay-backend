namespace CinemaSystemRandomPlay.Application.Pagos;

/// <summary>La pasarela de pago no respondió o respondió con un error propio. La operación se puede reintentar.</summary>
public class ProcesadorDePagosNoDisponibleException : Exception
{
    public ProcesadorDePagosNoDisponibleException(string mensaje, Exception? causa = null)
        : base(mensaje, causa)
    {
    }
}
