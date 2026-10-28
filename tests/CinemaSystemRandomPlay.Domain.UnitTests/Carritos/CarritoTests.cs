using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;
using static CinemaSystemRandomPlay.Domain.UnitTests.Carritos.CarritoFactory;
using static CinemaSystemRandomPlay.Domain.UnitTests.CompradoresDePrueba;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Carritos;

public class CarritoTests
{
    private readonly Funcion _funcion = CrearFuncion();

    [Fact]
    public void EstadoEn_AntesDelVencimiento_EsActivoYRetieneAsientos()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('A', 1));

        Assert.Equal(EstadoCarrito.Activo, carrito.EstadoEn(Ahora.AddMinutes(4)));
        Assert.True(carrito.RetieneAsientosEn(Ahora.AddMinutes(4)));
    }

    [Fact]
    public void EstadoEn_EnElVencimiento_EsExpiradoYNoRetiene()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('A', 1));

        Assert.Equal(EstadoCarrito.Expirado, carrito.EstadoEn(carrito.VenceEn));
        Assert.False(carrito.RetieneAsientosEn(carrito.VenceEn));
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
    }

    [Fact]
    public void ReemplazarAsientos_DejaExactamenteLaNuevaSeleccionYNoCambiaElVencimiento()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));
        var venceEn = carrito.VenceEn;

        var (liberados, retenidos) = carrito.ReemplazarAsientos([new Asiento('B', 2), new Asiento('B', 1)], Ahora.AddMinutes(3), 10);

        Assert.Equal([new Asiento('B', 1), new Asiento('B', 2)], carrito.Asientos);
        Assert.Equal([new Asiento('C', 5), new Asiento('C', 6)], liberados);
        Assert.Equal([new Asiento('B', 1), new Asiento('B', 2)], retenidos);
        Assert.Equal(venceEn, carrito.VenceEn);
        Assert.Equal(Ahora, carrito.CreadoEn);
    }

    [Fact]
    public void ReemplazarAsientos_ConAsientosComunes_SoloLiberaYRetieneLaDiferencia()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));

        var (liberados, retenidos) = carrito.ReemplazarAsientos([new Asiento('C', 6), new Asiento('C', 7)], Ahora, 10);

        Assert.Equal([new Asiento('C', 6), new Asiento('C', 7)], carrito.Asientos);
        Assert.Equal([new Asiento('C', 5)], liberados);
        Assert.Equal([new Asiento('C', 7)], retenidos);
    }

    [Fact]
    public void ReemplazarAsientos_ActualizaElTotal()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5));

        carrito.ReemplazarAsientos([new Asiento('C', 6), new Asiento('C', 7), new Asiento('C', 8)], Ahora, 10);

        Assert.Equal(_funcion.PrecioEntrada.Multiplicar(3), carrito.Total);
    }

    [Fact]
    public void ReemplazarAsientos_CarritoExpiradoOAbandonado_Lanza()
    {
        var expirado = CarritoDe(_funcion, Ahora, new Asiento('A', 1));
        var abandonado = CarritoDe(_funcion, Ahora, new Asiento('A', 2));
        abandonado.Abandonar(Ahora);

        Assert.Throws<DomainException>(() => expirado.ReemplazarAsientos([new Asiento('B', 1)], expirado.VenceEn, 10));
        Assert.Throws<DomainException>(() => abandonado.ReemplazarAsientos([new Asiento('B', 1)], Ahora, 10));
        Assert.Equal([new Asiento('A', 1)], expirado.Asientos);
    }

    [Fact]
    public void ReemplazarAsientos_SeleccionVaciaRepetidaOExcedida_LanzaSinCambiar()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('A', 1));

        Assert.Throws<DomainException>(() => carrito.ReemplazarAsientos([], Ahora, 10));
        Assert.Throws<DomainException>(() => carrito.ReemplazarAsientos([new Asiento('B', 1), new Asiento('B', 1)], Ahora, 10));
        Assert.Throws<DomainException>(() => carrito.ReemplazarAsientos(Fila('B', 1, 3), Ahora, 2));
        Assert.Equal([new Asiento('A', 1)], carrito.Asientos);
    }

    [Fact]
    public void Abandonar_CarritoActivo_PasaAAbandonadoYDejaDeRetener()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('A', 1));

        var resultado = carrito.Abandonar(Ahora.AddMinutes(1));

        Assert.True(resultado.Abandonado);
        Assert.Equal(EstadoCarrito.Abandonado, carrito.Estado);
        Assert.Equal(Ahora.AddMinutes(1), carrito.AbandonadoEn);
        Assert.False(carrito.RetieneAsientosEn(Ahora.AddMinutes(1)));
    }

    [Fact]
    public void Abandonar_CarritoExpiradoOYaAbandonado_DevuelveNoActivoConElEstado()
    {
        var expirado = CarritoDe(_funcion, Ahora, new Asiento('A', 1));
        var abandonado = CarritoDe(_funcion, Ahora, new Asiento('A', 2));
        abandonado.Abandonar(Ahora);

        var sobreExpirado = expirado.Abandonar(expirado.VenceEn);
        var sobreAbandonado = abandonado.Abandonar(Ahora.AddMinutes(1));

        Assert.False(sobreExpirado.Abandonado);
        Assert.Equal(EstadoCarrito.Expirado, sobreExpirado.EstadoActual);
        Assert.Equal(EstadoCarrito.Activo, expirado.Estado);
        Assert.False(sobreAbandonado.Abandonado);
        Assert.Equal(EstadoCarrito.Abandonado, sobreAbandonado.EstadoActual);
        Assert.Equal(Ahora, abandonado.AbandonadoEn);
    }

    [Fact]
    public void IndicarEmailEntrega_CarritoActivo_GuardaYPermiteReemplazarSinTocarEstadoNiVencimiento()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('A', 1));
        var venceEn = carrito.VenceEn;

        Assert.True(carrito.IndicarEmailEntrega(Ana, Ahora).Indicado);
        Assert.True(carrito.IndicarEmailEntrega(Beto, Ahora.AddMinutes(1)).Indicado);

        Assert.Equal(Beto, carrito.EmailEntrega);
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
        Assert.Equal(venceEn, carrito.VenceEn);
    }

    [Fact]
    public void IndicarEmailEntrega_CarritoNoActivo_DevuelveNoActivoSinGuardar()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('A', 1));

        var resultado = carrito.IndicarEmailEntrega(EmailComprador.Crear("ana@example.com"), carrito.VenceEn);

        Assert.False(resultado.Indicado);
        Assert.Equal(EstadoCarrito.Expirado, resultado.EstadoActual);
        Assert.Null(carrito.EmailEntrega);
    }
}
