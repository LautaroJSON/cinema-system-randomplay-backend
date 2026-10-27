namespace CinemaSystemRandomPlay.Domain.Reservas;

public sealed record ResultadoCancelacion(bool Cancelada, EstadoReserva EstadoActual)
{
    public static ResultadoCancelacion Ok() => new(true, EstadoReserva.Cancelada);

    public static ResultadoCancelacion NoCancelable(EstadoReserva estadoActual) => new(false, estadoActual);
}
