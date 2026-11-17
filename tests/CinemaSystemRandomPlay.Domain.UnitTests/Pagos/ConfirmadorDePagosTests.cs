using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Pagos;
using static CinemaSystemRandomPlay.Domain.UnitTests.Carritos.CarritoFactory;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Pagos;

public class ConfirmadorDePagosTests
{
    private readonly Funcion _funcion = CrearFuncion();
    private readonly ConfirmadorDePagos _confirmador = new();

    private static PagoAprobado Pago(Carrito carrito, DateTimeOffset aprobadoEn, Dinero? monto = null) =>
        new("pago-1", monto ?? carrito.Total, aprobadoEn);

    [Fact]
    public void CarritoEnPagoVigente_PorElTotal_Acredita()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));

        var decision = _confirmador.Decidir(carrito, [carrito], Pago(carrito, Ahora.AddMinutes(2)), Ahora.AddMinutes(2));

        Assert.True(decision.Acreditar);
        Assert.Null(decision.Motivo);
    }

    [Fact]
    public void CarritoYaPagado_Devuelve()
    {
        var carrito = CarritoPagado(_funcion, Ahora, new Asiento('C', 5));

        var decision = _confirmador.Decidir(carrito, [carrito], Pago(carrito, Ahora), Ahora);

        Assert.Equal(DecisionDePago.Devolver(MotivoDevolucion.CarritoYaPagado), decision);
    }

    [Fact]
    public void CarritoActivoSinPagoIniciado_DevuelveComoPagoTardio()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5));

        var decision = _confirmador.Decidir(carrito, [carrito], Pago(carrito, Ahora), Ahora);

        Assert.Equal(DecisionDePago.Devolver(MotivoDevolucion.PagoTardio), decision);
    }

    [Fact]
    public void CarritoAbandonado_DevuelveComoPagoTardio()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5));
        carrito.Abandonar(Ahora);

        var decision = _confirmador.Decidir(carrito, [], Pago(carrito, Ahora), Ahora);

        Assert.Equal(DecisionDePago.Devolver(MotivoDevolucion.PagoTardio), decision);
    }

    [Fact]
    public void MontoDistinto_Devuelve()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));

        var decision = _confirmador.Decidir(carrito, [carrito], Pago(carrito, Ahora, Dinero.Ars(5500m)), Ahora);

        Assert.Equal(DecisionDePago.Devolver(MotivoDevolucion.MontoDistinto), decision);
    }

    [Fact]
    public void MontoDistintoYAprobadoTarde_GanaElMontoPorElOrdenDeLasReglas()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));

        var decision = _confirmador.Decidir(carrito, [], Pago(carrito, carrito.VenceEn.AddMinutes(1), Dinero.Ars(1m)),
            carrito.VenceEn.AddMinutes(1));

        Assert.Equal(DecisionDePago.Devolver(MotivoDevolucion.MontoDistinto), decision);
    }

    [Fact]
    public void AprobadoDespuesDelVencimiento_DevuelveComoPagoTardio()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));

        var decision = _confirmador.Decidir(carrito, [], Pago(carrito, carrito.VenceEn.AddSeconds(1)), carrito.VenceEn.AddMinutes(1));

        Assert.Equal(DecisionDePago.Devolver(MotivoDevolucion.PagoTardio), decision);
    }

    [Fact]
    public void AprobadoJustoEnElVencimiento_Acredita()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));

        var decision = _confirmador.Decidir(carrito, [], Pago(carrito, carrito.VenceEn), carrito.VenceEn.AddSeconds(20));

        Assert.True(decision.Acreditar);
    }

    [Fact]
    public void AprobadoATiempoConAvisoTardioYAsientosLibres_Acredita()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));
        var procesadoEn = carrito.VenceEn.AddSeconds(20);

        var decision = _confirmador.Decidir(carrito, [], Pago(carrito, carrito.VenceEn.AddSeconds(-10)), procesadoEn);

        Assert.True(decision.Acreditar);
    }

    [Theory]
    [InlineData("activo")]
    [InlineData("pagando")]
    [InlineData("pagado")]
    public void AprobadoATiempoConAvisoTardioYUnAsientoTomado_DevuelveAsientosNoDisponibles(string estadoDelOtro)
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));
        var procesadoEn = carrito.VenceEn.AddSeconds(20);
        var otro = estadoDelOtro switch
        {
            "activo" => CarritoDe(_funcion, carrito.VenceEn, new Asiento('C', 6)),
            "pagando" => CarritoEnPago(_funcion, carrito.VenceEn, new Asiento('C', 6)),
            _ => CarritoPagado(_funcion, carrito.VenceEn, new Asiento('C', 6))
        };

        var decision = _confirmador.Decidir(carrito, [carrito, otro], Pago(carrito, carrito.VenceEn.AddSeconds(-10)), procesadoEn);

        Assert.Equal(DecisionDePago.Devolver(MotivoDevolucion.AsientosNoDisponibles), decision);
    }

    [Fact]
    public void CarritoVigente_NoMiraLosOtrosCarritos()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));
        // Un dato inconsistente (otro carrito con el mismo asiento) no puede darse con el bloqueo de la función; si el carrito
        // está vigente, sus asientos son suyos.
        var otro = CarritoDe(_funcion, Ahora, new Asiento('C', 5));

        var decision = _confirmador.Decidir(carrito, [carrito, otro], Pago(carrito, Ahora), Ahora.AddMinutes(1));

        Assert.True(decision.Acreditar);
    }
}
