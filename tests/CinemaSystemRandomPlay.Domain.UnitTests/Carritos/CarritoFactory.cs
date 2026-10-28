using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Carritos;

internal static class CarritoFactory
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 28, 18, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>Sala por defecto: filas A (10), B (12) y C (12), 34 asientos; con el 50% el tope es 17.</summary>
    public static Funcion CrearFuncion(DateTimeOffset? inicio = null, params FilaSala[] filas)
    {
        var sala = new Sala(Guid.NewGuid(), Guid.NewGuid(), "Sala 1", filas.Length > 0 ? filas : [new FilaSala('A', 10), new FilaSala('B', 12), new FilaSala('C', 12)]);
        var funcion = new Funcion(Guid.NewGuid(), Guid.NewGuid(), sala.Id, inicio ?? Ahora.AddHours(3), Dinero.Ars(5500m));
        typeof(Funcion).GetProperty(nameof(Funcion.Sala))!.SetValue(funcion, sala);
        return funcion;
    }

    public static Carrito CarritoDe(Funcion funcion, DateTimeOffset ahora, params Asiento[] asientos)
    {
        var resultado = new AsignadorDeAsientos().Crear(funcion, [], asientos, ahora, new ReglasCarrito());
        return resultado.Carrito ?? throw new InvalidOperationException($"No se pudo crear el carrito: {resultado.Tipo}");
    }

    public static Asiento[] Fila(char fila, int desde, int cantidad) =>
        Enumerable.Range(desde, cantidad).Select(n => new Asiento(fila, n)).ToArray();
}
