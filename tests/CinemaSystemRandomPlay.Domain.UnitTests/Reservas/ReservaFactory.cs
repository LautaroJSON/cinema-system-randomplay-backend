using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Reservas;

internal static class ReservaFactory
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 27, 18, 0, 0, TimeSpan.FromHours(-3));

    public static Funcion CrearFuncion(DateTimeOffset? inicio = null, params FilaSala[] filas)
    {
        var sala = new Sala(Guid.NewGuid(), Guid.NewGuid(), "Sala 1", filas.Length > 0 ? filas : [new FilaSala('A', 10), new FilaSala('B', 12), new FilaSala('C', 12)]);
        var funcion = new Funcion(Guid.NewGuid(), Guid.NewGuid(), sala.Id, inicio ?? Ahora.AddHours(3));
        typeof(Funcion).GetProperty(nameof(Funcion.Sala))!.SetValue(funcion, sala);
        return funcion;
    }

    public static Reserva ReservaDe(Funcion funcion, string clienteId, DateTimeOffset ahora, params Asiento[] asientos)
    {
        var resultado = new ReservadorDeAsientos().Reservar(funcion, [], clienteId, asientos, ahora, new ReglasReserva());
        return resultado.Reserva ?? throw new InvalidOperationException($"No se pudo reservar: {resultado.Tipo}");
    }
}
