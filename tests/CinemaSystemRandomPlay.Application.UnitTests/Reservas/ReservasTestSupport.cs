using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.UnitTests.Reservas;

internal static class ReservasTestSupport
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 27, 18, 0, 0, TimeSpan.FromHours(-3));

    public static (Funcion Funcion, Pelicula Pelicula) CrearFuncion(bool peliculaActiva = true, DateTimeOffset? inicio = null)
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis", peliculaActiva);
        var sucursal = new Sucursal(Guid.NewGuid(), "Sucursal Centro");
        var sala = new Sala(Guid.NewGuid(), sucursal.Id, "Sala 1", [new FilaSala('A', 10), new FilaSala('B', 10)]);
        typeof(Sala).GetProperty(nameof(Sala.Sucursal))!.SetValue(sala, sucursal);
        var funcion = new Funcion(Guid.NewGuid(), pelicula.Id, sala.Id, inicio ?? Ahora.AddHours(3));
        typeof(Funcion).GetProperty(nameof(Funcion.Sala))!.SetValue(funcion, sala);
        return (funcion, pelicula);
    }

    /// <summary>Unidad de trabajo falsa que solo ejecuta la operación.</summary>
    public sealed class UnidadDeTrabajoFake : IUnidadDeTrabajo
    {
        public int Ejecuciones { get; private set; }

        public async Task<T> EjecutarEnTransaccion<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken cancellationToken = default)
        {
            Ejecuciones++;
            return await operacion(cancellationToken);
        }
    }
}
