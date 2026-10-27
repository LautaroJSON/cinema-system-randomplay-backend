using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using static CinemaSystemRandomPlay.Domain.UnitTests.Reservas.ReservaFactory;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Reservas;

public class ReservadorDeAsientosTests
{
    private readonly ReservadorDeAsientos _reservador = new();
    private readonly ReglasReserva _reglas = new();

    private ResultadoReserva Reservar(Funcion funcion, IReadOnlyList<Reserva> existentes, string cliente, params Asiento[] pedidos) =>
        _reservador.Reservar(funcion, existentes, cliente, pedidos, Ahora, _reglas);

    [Fact]
    public void Reservar_AsientosLibres_CreaReservaPendienteConVencimientoA10Minutos()
    {
        var funcion = CrearFuncion();

        var resultado = Reservar(funcion, [], "ana", new Asiento('C', 5), new Asiento('C', 6));

        Assert.Equal(TipoResultadoReserva.Ok, resultado.Tipo);
        var reserva = resultado.Reserva!;
        Assert.Equal("ana", reserva.ClienteId);
        Assert.Equal(funcion.Id, reserva.FuncionId);
        Assert.Equal(EstadoReserva.PendienteDePago, reserva.Estado);
        Assert.Equal(Ahora, reserva.CreadaEn);
        Assert.Equal(Ahora.AddMinutes(10), reserva.VenceEn);
        Assert.Equal([new Asiento('C', 5), new Asiento('C', 6)], reserva.Asientos);
    }

    [Fact]
    public void Reservar_FuncionQueEmpiezaAntesDeLosDiezMinutos_VenceAlInicioDeLaFuncion()
    {
        var funcion = CrearFuncion(Ahora.AddMinutes(4));

        var resultado = Reservar(funcion, [], "ana", new Asiento('A', 1));

        Assert.Equal(Ahora.AddMinutes(4), resultado.Reserva!.VenceEn);
    }

    [Fact]
    public void Reservar_FuncionYaComenzada_DevuelveFuncionNoDisponible()
    {
        var funcion = CrearFuncion(Ahora.AddMinutes(-1));

        var resultado = Reservar(funcion, [], "ana", new Asiento('A', 1));

        Assert.Equal(TipoResultadoReserva.FuncionNoDisponible, resultado.Tipo);
    }

    [Fact]
    public void Reservar_AsientosInexistentes_DevuelveSoloLosInexistentesOrdenados()
    {
        var funcion = CrearFuncion();

        var resultado = Reservar(funcion, [], "ana", new Asiento('Z', 1), new Asiento('A', 15), new Asiento('A', 1));

        Assert.Equal(TipoResultadoReserva.AsientosInexistentes, resultado.Tipo);
        Assert.Equal([new Asiento('A', 15), new Asiento('Z', 1)], resultado.Asientos);
    }

    [Fact]
    public void Reservar_AsientoRetenidoPorOtroCliente_DevuelveAsientosNoDisponibles()
    {
        var funcion = CrearFuncion();
        var existente = ReservaDe(funcion, "beto", Ahora, new Asiento('C', 5));

        var resultado = Reservar(funcion, [existente], "ana", new Asiento('C', 4), new Asiento('C', 5), new Asiento('C', 6));

        Assert.Equal(TipoResultadoReserva.AsientosNoDisponibles, resultado.Tipo);
        Assert.Equal([new Asiento('C', 5)], resultado.Asientos);
    }

    [Fact]
    public void Reservar_AsientoDeReservaVencida_EstaDisponible()
    {
        var funcion = CrearFuncion();
        var vencida = ReservaDe(funcion, "beto", Ahora.AddMinutes(-10), new Asiento('C', 5));

        var resultado = Reservar(funcion, [vencida], "ana", new Asiento('C', 5));

        Assert.Equal(TipoResultadoReserva.Ok, resultado.Tipo);
    }

    [Fact]
    public void Reservar_ReservaCanceladaNoRetieneAsientos()
    {
        var funcion = CrearFuncion();
        var cancelada = ReservaDe(funcion, "beto", Ahora, new Asiento('C', 5));
        cancelada.Cancelar(Ahora);

        var resultado = Reservar(funcion, [cancelada], "ana", new Asiento('C', 5));

        Assert.Equal(TipoResultadoReserva.Ok, resultado.Tipo);
    }

    [Fact]
    public void Reservar_SuperaElTopeDelCliente_DevuelveLimiteSuperadoConLosRestantes()
    {
        var funcion = CrearFuncion();
        var previa = ReservaDe(funcion, "ana", Ahora, Enumerable.Range(1, 7).Select(n => new Asiento('A', n)).ToArray());

        var resultado = Reservar(funcion, [previa], "ana", Enumerable.Range(1, 4).Select(n => new Asiento('B', n)).ToArray());

        Assert.Equal(TipoResultadoReserva.LimiteSuperado, resultado.Tipo);
        Assert.Equal(3, resultado.AsientosRestantes);
    }

    [Fact]
    public void Reservar_ElTopeNoCuentaLasReservasDeOtrosClientes()
    {
        var funcion = CrearFuncion();
        var deBeto = ReservaDe(funcion, "beto", Ahora, Enumerable.Range(1, 10).Select(n => new Asiento('A', n)).ToArray());

        var resultado = Reservar(funcion, [deBeto], "ana", Enumerable.Range(1, 10).Select(n => new Asiento('B', n)).ToArray());

        Assert.Equal(TipoResultadoReserva.Ok, resultado.Tipo);
    }

    [Fact]
    public void Reservar_ConVariosErrores_InformaPrimeroInexistentesLuegoNoDisponiblesYLuegoLimite()
    {
        var funcion = CrearFuncion();
        var previa = ReservaDe(funcion, "ana", Ahora, Enumerable.Range(1, 9).Select(n => new Asiento('A', n)).ToArray());

        var inexistentesYOcupados = Reservar(funcion, [previa], "ana", new Asiento('Z', 1), new Asiento('A', 1));
        var ocupadosYLimite = Reservar(funcion, [previa], "ana", new Asiento('A', 1), new Asiento('B', 1), new Asiento('B', 2));

        Assert.Equal(TipoResultadoReserva.AsientosInexistentes, inexistentesYOcupados.Tipo);
        Assert.Equal(TipoResultadoReserva.AsientosNoDisponibles, ocupadosYLimite.Tipo);
    }

    [Fact]
    public void Reservar_FuncionYaComenzadaConAsientosInexistentes_InformaPrimeroLaFuncion()
    {
        var funcion = CrearFuncion(Ahora.AddMinutes(-5));

        var resultado = Reservar(funcion, [], "ana", new Asiento('Z', 1));

        Assert.Equal(TipoResultadoReserva.FuncionNoDisponible, resultado.Tipo);
    }
}
