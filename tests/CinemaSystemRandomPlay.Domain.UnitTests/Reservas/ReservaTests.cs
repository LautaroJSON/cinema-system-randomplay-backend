using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using static CinemaSystemRandomPlay.Domain.UnitTests.Reservas.ReservaFactory;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Reservas;

public class ReservaTests
{
    private static readonly Asiento A1 = new('A', 1);

    private static ResultadoReserva Reservar(string cliente, params Asiento[] asientos) =>
        new ReservadorDeAsientos().Reservar(CrearFuncion(), [], cliente, asientos, Ahora, new ReglasReserva());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_ConClienteVacio_LanzaDomainException(string cliente)
    {
        Assert.Throws<DomainException>(() => Reservar(cliente, A1));
    }

    [Fact]
    public void Crear_ConClienteDeMasDe255Caracteres_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Reservar(new string('x', 256), A1));
    }

    [Fact]
    public void Crear_SinAsientos_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Reservar("ana"));
    }

    [Fact]
    public void Crear_ConAsientosRepetidos_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Reservar("ana", A1, new Asiento('A', 1)));
    }

    [Fact]
    public void Asientos_SeExponenOrdenadosPorFilaYNumero()
    {
        var reserva = Reservar("ana", new Asiento('B', 2), new Asiento('A', 3), new Asiento('A', 1)).Reserva!;

        Assert.Equal([new Asiento('A', 1), new Asiento('A', 3), new Asiento('B', 2)], reserva.Asientos);
    }

    [Fact]
    public void EstadoEn_AntesDelVencimiento_EsPendienteDePago()
    {
        var reserva = Reservar("ana", A1).Reserva!;

        Assert.Equal(EstadoReserva.PendienteDePago, reserva.EstadoEn(reserva.VenceEn.AddTicks(-1)));
        Assert.True(reserva.RetieneAsientosEn(reserva.VenceEn.AddTicks(-1)));
    }

    [Fact]
    public void EstadoEn_EnElInstanteExactoDelVencimiento_EsExpirada()
    {
        var reserva = Reservar("ana", A1).Reserva!;

        Assert.Equal(EstadoReserva.Expirada, reserva.EstadoEn(reserva.VenceEn));
        Assert.False(reserva.RetieneAsientosEn(reserva.VenceEn));
    }

    [Fact]
    public void Cancelar_PendienteVigente_PasaACanceladaYGuardaElMomento()
    {
        var reserva = Reservar("ana", A1).Reserva!;
        var ahora = Ahora.AddMinutes(2);

        var resultado = reserva.Cancelar(ahora);

        Assert.True(resultado.Cancelada);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
        Assert.Equal(ahora, reserva.CanceladaEn);
        Assert.Equal(EstadoReserva.Cancelada, reserva.EstadoEn(ahora.AddHours(1)));
    }

    [Fact]
    public void Cancelar_Vencida_DevuelveNoCancelableExpiradaYNoCambiaNada()
    {
        var reserva = Reservar("ana", A1).Reserva!;

        var resultado = reserva.Cancelar(reserva.VenceEn);

        Assert.False(resultado.Cancelada);
        Assert.Equal(EstadoReserva.Expirada, resultado.EstadoActual);
        Assert.Equal(EstadoReserva.PendienteDePago, reserva.Estado);
        Assert.Null(reserva.CanceladaEn);
    }

    [Fact]
    public void Cancelar_YaCancelada_DevuelveNoCancelableCanceladaYConservaElMomento()
    {
        var reserva = Reservar("ana", A1).Reserva!;
        reserva.Cancelar(Ahora.AddMinutes(1));

        var resultado = reserva.Cancelar(Ahora.AddMinutes(2));

        Assert.False(resultado.Cancelada);
        Assert.Equal(EstadoReserva.Cancelada, resultado.EstadoActual);
        Assert.Equal(Ahora.AddMinutes(1), reserva.CanceladaEn);
    }
}
