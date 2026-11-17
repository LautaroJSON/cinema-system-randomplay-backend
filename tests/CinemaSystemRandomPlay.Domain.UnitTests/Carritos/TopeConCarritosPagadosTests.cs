using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Funciones;
using static CinemaSystemRandomPlay.Domain.UnitTests.Carritos.CarritoFactory;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Carritos;

/// <summary>Los asientos vendidos no están disponibles, pero no cuentan para el tope de retención temporal.</summary>
public class TopeConCarritosPagadosTests
{
    [Fact]
    public void AsientosPagados_NoCuentanParaElTopePeroNoEstanDisponibles()
    {
        // Sala de 34 asientos: tope 17. Ya se vendieron 17.
        var funcion = CrearFuncion();
        var pagado = CarritoPagado(funcion, Ahora, Fila('B', 1, 10));
        var otroPagado = CarritoPagado(funcion, Ahora, Fila('C', 1, 7));
        var asignador = new AsignadorDeAsientos();

        var libre = asignador.Crear(funcion, [pagado, otroPagado], Fila('A', 1, 10), Ahora, new ReglasCarrito());
        var vendido = asignador.Crear(funcion, [pagado, otroPagado], [new Asiento('B', 1)], Ahora, new ReglasCarrito());

        Assert.Equal(TipoResultadoSeleccion.Ok, libre.Tipo);
        Assert.Equal(TipoResultadoSeleccion.AsientosNoDisponibles, vendido.Tipo);
    }

    [Fact]
    public void CarritosEnPago_CuentanParaElTope()
    {
        var funcion = CrearFuncion();
        var enPago = CarritoEnPago(funcion, Ahora, Fila('B', 1, 10));
        var activo = CarritoDe(funcion, Ahora, Fila('C', 1, 7));

        var resultado = new AsignadorDeAsientos().Crear(funcion, [enPago, activo], [new Asiento('A', 1)], Ahora, new ReglasCarrito());

        Assert.Equal(TipoResultadoSeleccion.FuncionSinCupo, resultado.Tipo);
    }
}
