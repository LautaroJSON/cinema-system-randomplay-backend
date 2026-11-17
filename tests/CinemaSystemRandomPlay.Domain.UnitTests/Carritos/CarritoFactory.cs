using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Pagos;

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

    /// <summary>Carrito con email de entrega y pago iniciado en <paramref name="ahora"/>.</summary>
    public static Carrito CarritoEnPago(Funcion funcion, DateTimeOffset ahora, params Asiento[] asientos)
    {
        var carrito = CarritoDe(funcion, ahora, asientos);
        carrito.IndicarEmailEntrega(EmailComprador.Crear("ana@example.com"), ahora);
        var resultado = carrito.IniciarPago("pref-1", "https://pago/pref-1", carrito.Asientos, ahora, funcion.FechaHoraInicio);
        if (resultado.Tipo != TipoResultadoInicioPago.Iniciado)
            throw new InvalidOperationException($"No se pudo iniciar el pago: {resultado.Tipo}");
        return carrito;
    }

    public static Carrito CarritoPagado(Funcion funcion, DateTimeOffset ahora, params Asiento[] asientos)
    {
        var carrito = CarritoEnPago(funcion, ahora, asientos);
        carrito.MarcarPagado(new PagoAprobado("pago-1", carrito.Total, ahora), ahora, DatosDe(funcion), new GeneradorDeCodigosDePrueba());
        return carrito;
    }

    public static DatosDeLaFuncion DatosDe(Funcion funcion) =>
        new(funcion.Id, "Interstellar", "Centro", "Sala 1", funcion.FechaHoraInicio);
}

/// <summary>Códigos predecibles: COD-1, COD-2, ...</summary>
internal sealed class GeneradorDeCodigosDePrueba : IGeneradorDeCodigos
{
    private int _siguiente;

    public string Generar() => $"COD-{Interlocked.Increment(ref _siguiente)}";
}
