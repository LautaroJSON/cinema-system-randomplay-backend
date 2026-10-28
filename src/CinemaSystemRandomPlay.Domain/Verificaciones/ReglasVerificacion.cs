using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Verificaciones;

public sealed record ReglasVerificacion
{
    public int CodigoMinutosVigencia { get; }
    public int MaximoIntentosFallidos { get; }
    public int SolicitudesPorEmail { get; }
    public int VentanaSolicitudesMinutos { get; }

    public ReglasVerificacion(
        int codigoMinutosVigencia = 10,
        int maximoIntentosFallidos = 5,
        int solicitudesPorEmail = 5,
        int ventanaSolicitudesMinutos = 60)
    {
        if (codigoMinutosVigencia <= 0)
            throw new DomainException("Los minutos de vigencia del código deben ser mayores a cero.");

        if (maximoIntentosFallidos <= 0)
            throw new DomainException("El máximo de intentos fallidos debe ser mayor a cero.");

        if (solicitudesPorEmail <= 0)
            throw new DomainException("El máximo de solicitudes por email debe ser mayor a cero.");

        if (ventanaSolicitudesMinutos <= 0)
            throw new DomainException("La ventana de solicitudes debe ser mayor a cero minutos.");

        CodigoMinutosVigencia = codigoMinutosVigencia;
        MaximoIntentosFallidos = maximoIntentosFallidos;
        SolicitudesPorEmail = solicitudesPorEmail;
        VentanaSolicitudesMinutos = ventanaSolicitudesMinutos;
    }
}
