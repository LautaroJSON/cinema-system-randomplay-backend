using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Pagos;
using static CinemaSystemRandomPlay.Domain.UnitTests.Carritos.CarritoFactory;
using static CinemaSystemRandomPlay.Domain.UnitTests.CompradoresDePrueba;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Carritos;

/// <summary>Estados <c>Pagando</c> y <c>Pagado</c> del carrito (feature 008).</summary>
public class CarritoPagoTests
{
    private readonly Funcion _funcion = CrearFuncion();

    // --- Estado efectivo y retención ---

    [Fact]
    public void EstadoEn_PagandoVigente_EsPagandoYRetiene()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));

        Assert.Equal(EstadoCarrito.Pagando, carrito.EstadoEn(Ahora.AddMinutes(5)));
        Assert.True(carrito.RetieneAsientosEn(Ahora.AddMinutes(5)));
    }

    [Fact]
    public void EstadoEn_PagandoVencido_EsExpiradoYNoRetiene()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));

        Assert.Equal(EstadoCarrito.Expirado, carrito.EstadoEn(carrito.VenceEn));
        Assert.False(carrito.RetieneAsientosEn(carrito.VenceEn));
        Assert.Equal(EstadoCarrito.Pagando, carrito.Estado);
    }

    [Fact]
    public void EstadoEn_Pagado_EsPagadoYRetieneAunDespuesDelVencimiento()
    {
        var carrito = CarritoPagado(_funcion, Ahora, new Asiento('C', 5));

        Assert.Equal(EstadoCarrito.Pagado, carrito.EstadoEn(carrito.VenceEn.AddDays(1)));
        Assert.True(carrito.RetieneAsientosEn(carrito.VenceEn.AddDays(1)));
    }

    [Fact]
    public void EstadoEn_Abandonado_NoRetiene()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5));
        carrito.Abandonar(Ahora);

        Assert.False(carrito.RetieneAsientosEn(Ahora));
    }

    // --- Vencimiento al iniciar el pago ---

    [Fact]
    public void VencimientoAlIniciarPago_SumaUnMinuto()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5));

        Assert.Equal(carrito.VenceEn.AddMinutes(1), carrito.VencimientoAlIniciarPago(_funcion.FechaHoraInicio));
    }

    [Fact]
    public void VencimientoAlIniciarPago_NoPasaDelInicioDeLaFuncion()
    {
        var funcion = CrearFuncion(inicio: Ahora.AddMinutes(5).AddSeconds(30));
        var carrito = CarritoDe(funcion, Ahora, new Asiento('C', 5));

        Assert.Equal(Ahora.AddMinutes(5), carrito.VenceEn);
        Assert.Equal(funcion.FechaHoraInicio, carrito.VencimientoAlIniciarPago(funcion.FechaHoraInicio));
    }

    // --- IniciarPago ---

    [Fact]
    public void IniciarPago_CarritoActivoConEmail_PasaAPagandoYExtiendeElVencimiento()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));
        carrito.IndicarEmailEntrega(Ana, Ahora);
        var esperado = carrito.VenceEn.AddMinutes(1);

        var resultado = carrito.IniciarPago("pref-1", "https://pago/pref-1", carrito.Asientos, Ahora.AddMinutes(3), _funcion.FechaHoraInicio);

        Assert.Equal(TipoResultadoInicioPago.Iniciado, resultado.Tipo);
        Assert.Equal("https://pago/pref-1", resultado.UrlPago);
        Assert.Equal(EstadoCarrito.Pagando, carrito.Estado);
        Assert.Equal(esperado, carrito.VenceEn);
        Assert.Equal("pref-1", carrito.PreferenciaPagoId);
        Assert.Equal("https://pago/pref-1", carrito.UrlPago);
        Assert.Equal(Ahora.AddMinutes(3), carrito.PagoIniciadoEn);
    }

    [Fact]
    public void IniciarPago_YaEnPago_DevuelveLaUrlGuardadaSinExtenderDeNuevo()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));
        var venceEn = carrito.VenceEn;

        var resultado = carrito.IniciarPago("pref-2", "https://pago/pref-2", carrito.Asientos, Ahora.AddMinutes(1), _funcion.FechaHoraInicio);

        Assert.Equal(TipoResultadoInicioPago.YaEnPago, resultado.Tipo);
        Assert.Equal("https://pago/pref-1", resultado.UrlPago);
        Assert.Equal(venceEn, carrito.VenceEn);
        Assert.Equal("pref-1", carrito.PreferenciaPagoId);
    }

    [Fact]
    public void IniciarPago_SinEmail_DevuelveSinEmailEntregaSinCambios()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5));
        var venceEn = carrito.VenceEn;

        var resultado = carrito.IniciarPago("pref-1", "https://pago/pref-1", carrito.Asientos, Ahora, _funcion.FechaHoraInicio);

        Assert.Equal(TipoResultadoInicioPago.SinEmailEntrega, resultado.Tipo);
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
        Assert.Equal(venceEn, carrito.VenceEn);
    }

    [Fact]
    public void IniciarPago_ExpiradoOAbandonado_DevuelveNoActivoConElEstado()
    {
        var expirado = CarritoDe(_funcion, Ahora, new Asiento('C', 5));
        expirado.IndicarEmailEntrega(Ana, Ahora);
        var abandonado = CarritoDe(_funcion, Ahora, new Asiento('C', 6));
        abandonado.IndicarEmailEntrega(Ana, Ahora);
        abandonado.Abandonar(Ahora);

        var sobreExpirado = expirado.IniciarPago("p", "u", expirado.Asientos, expirado.VenceEn, _funcion.FechaHoraInicio);
        var sobreAbandonado = abandonado.IniciarPago("p", "u", abandonado.Asientos, Ahora, _funcion.FechaHoraInicio);

        Assert.Equal((TipoResultadoInicioPago.NoActivo, EstadoCarrito.Expirado), (sobreExpirado.Tipo, sobreExpirado.EstadoActual));
        Assert.Equal((TipoResultadoInicioPago.NoActivo, EstadoCarrito.Abandonado), (sobreAbandonado.Tipo, sobreAbandonado.EstadoActual));
    }

    [Fact]
    public void IniciarPago_ConOtraSeleccion_DevuelveSeleccionModificada()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));
        carrito.IndicarEmailEntrega(Ana, Ahora);

        var resultado = carrito.IniciarPago("p", "u", [new Asiento('C', 5)], Ahora, _funcion.FechaHoraInicio);

        Assert.Equal(TipoResultadoInicioPago.SeleccionModificada, resultado.Tipo);
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
    }

    // --- Cambios bloqueados en Pagando ---

    [Fact]
    public void EnPago_NoSePuedeReemplazarAbandonarNiCambiarElEmail()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));
        var reglas = new ReglasCarrito();

        var reemplazo = new AsignadorDeAsientos().Reemplazar(_funcion, carrito, [carrito], [new Asiento('A', 1)], Ahora.AddMinutes(1), reglas);
        var abandono = carrito.Abandonar(Ahora.AddMinutes(1));
        var email = carrito.IndicarEmailEntrega(Beto, Ahora.AddMinutes(1));

        Assert.Equal(TipoResultadoSeleccion.CarritoEnPago, reemplazo.Tipo);
        Assert.False(abandono.Abandonado);
        Assert.Equal(EstadoCarrito.Pagando, abandono.EstadoActual);
        Assert.False(email.Indicado);
        Assert.Equal(EstadoCarrito.Pagando, email.EstadoActual);
        Assert.Equal([new Asiento('C', 5)], carrito.Asientos);
        Assert.Equal(Ana, carrito.EmailEntrega);
        Assert.Equal(EstadoCarrito.Pagando, carrito.Estado);
    }

    [Fact]
    public void ReemplazarAsientos_EnUnCarritoPagado_Lanza()
    {
        var carrito = CarritoPagado(_funcion, Ahora, new Asiento('C', 5));

        Assert.Throws<DomainException>(() => carrito.ReemplazarAsientos([new Asiento('A', 1)], Ahora, 10));
    }

    // --- MarcarPagado ---

    [Fact]
    public void MarcarPagado_EmiteUnaEntradaPorAsientoConLosDatosDeLaFuncion()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 6), new Asiento('C', 5));
        var aprobadoEn = Ahora.AddMinutes(2);

        carrito.MarcarPagado(new PagoAprobado("pago-9", carrito.Total, aprobadoEn), Ahora.AddMinutes(3), DatosDe(_funcion),
            new GeneradorDeCodigosDePrueba());

        Assert.Equal(EstadoCarrito.Pagado, carrito.Estado);
        Assert.Equal("pago-9", carrito.PagoId);
        Assert.Equal(aprobadoEn, carrito.PagoAprobadoEn);
        Assert.Equal(Ahora.AddMinutes(3), carrito.PagadoEn);
        Assert.Collection(carrito.Entradas,
            e =>
            {
                Assert.Equal(new Asiento('C', 5), e.Asiento);
                Assert.Equal("COD-1", e.Codigo);
                Assert.Equal(carrito.Id, e.CarritoId);
                Assert.Equal(_funcion.Id, e.FuncionId);
                Assert.Equal("Interstellar", e.PeliculaTitulo);
                Assert.Equal("Centro", e.SucursalNombre);
                Assert.Equal("Sala 1", e.SalaNombre);
                Assert.Equal(_funcion.FechaHoraInicio, e.HoraInicio);
                Assert.Equal(Dinero.Ars(5500m), e.PrecioUnitario);
            },
            e => Assert.Equal(new Asiento('C', 6), e.Asiento));
    }

    [Fact]
    public void MarcarPagado_CarritoQueNoEstaEnPago_Lanza()
    {
        var carrito = CarritoDe(_funcion, Ahora, new Asiento('C', 5));

        Assert.Throws<DomainException>(() => carrito.MarcarPagado(new PagoAprobado("p", carrito.Total, Ahora), Ahora,
            DatosDe(_funcion), new GeneradorDeCodigosDePrueba()));
    }

    [Fact]
    public void MarcarPagado_ConDatosDeOtraFuncion_Lanza()
    {
        var carrito = CarritoEnPago(_funcion, Ahora, new Asiento('C', 5));

        Assert.Throws<DomainException>(() => carrito.MarcarPagado(new PagoAprobado("p", carrito.Total, Ahora), Ahora,
            DatosDe(CrearFuncion()), new GeneradorDeCodigosDePrueba()));
    }

    [Fact]
    public void MarcarEntregaEnviada_EsIdempotente()
    {
        var carrito = CarritoPagado(_funcion, Ahora, new Asiento('C', 5));

        carrito.MarcarEntregaEnviada(Ahora.AddMinutes(1));
        carrito.MarcarEntregaEnviada(Ahora.AddMinutes(2));

        Assert.Equal(Ahora.AddMinutes(1), carrito.EntregaEnviadaEn);
    }
}
