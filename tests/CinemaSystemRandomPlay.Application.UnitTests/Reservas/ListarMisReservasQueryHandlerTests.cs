using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Application.Reservas.Queries;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Reservas.ReservasTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Reservas;

public class ListarMisReservasQueryHandlerTests
{
    private readonly IConsultaReservas _consulta = Substitute.For<IConsultaReservas>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();

    public ListarMisReservasQueryHandlerTests()
    {
        _reloj.Ahora.Returns(Ahora);
    }

    private static ReservaLecturaDto Lectura(DateTimeOffset creadaEn, DateTimeOffset venceEn, EstadoReserva estado = EstadoReserva.PendienteDePago, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), estado, creadaEn, venceEn, estado == EstadoReserva.Cancelada ? creadaEn.AddMinutes(1) : null,
            [new Asiento('B', 2), new Asiento('A', 9)], Guid.NewGuid(), Ahora.AddHours(3), Guid.NewGuid(), "Interstellar",
            Guid.NewGuid(), "Sucursal Centro", Guid.NewGuid(), "Sala 1", Dinero.Ars(5500m));

    [Fact]
    public async Task Listar_OrdenaDeLaMasRecienteALaMasAntigua()
    {
        var vieja = Lectura(Ahora.AddMinutes(-30), Ahora.AddMinutes(-20));
        var nueva = Lectura(Ahora.AddMinutes(-1), Ahora.AddMinutes(9));
        var media = Lectura(Ahora.AddMinutes(-15), Ahora.AddMinutes(-5));
        _consulta.ListarDeCliente("ana", Arg.Any<CancellationToken>()).Returns(new[] { vieja, nueva, media });

        var resultado = await new ListarMisReservasQueryHandler(_consulta, _reloj).Handle(new ListarMisReservasQuery("ana"));

        Assert.Equal([nueva.Id, media.Id, vieja.Id], resultado.Select(r => r.Id));
    }

    [Fact]
    public async Task Listar_CalculaElEstadoEfectivoContraElReloj()
    {
        var vigente = Lectura(Ahora.AddMinutes(-1), Ahora.AddMinutes(9));
        var vencida = Lectura(Ahora.AddMinutes(-20), Ahora.AddMinutes(-10));
        var cancelada = Lectura(Ahora.AddMinutes(-5), Ahora.AddMinutes(5), EstadoReserva.Cancelada);
        _consulta.ListarDeCliente("ana", Arg.Any<CancellationToken>()).Returns(new[] { vigente, vencida, cancelada });

        var resultado = await new ListarMisReservasQueryHandler(_consulta, _reloj).Handle(new ListarMisReservasQuery("ana"));

        Assert.Equal(EstadoReserva.PendienteDePago, resultado.Single(r => r.Id == vigente.Id).Estado);
        Assert.Equal(EstadoReserva.Expirada, resultado.Single(r => r.Id == vencida.Id).Estado);
        Assert.Equal(EstadoReserva.Cancelada, resultado.Single(r => r.Id == cancelada.Id).Estado);
    }

    [Fact]
    public async Task Listar_OrdenaLosAsientosPorFilaYNumero()
    {
        _consulta.ListarDeCliente("ana", Arg.Any<CancellationToken>()).Returns(new[] { Lectura(Ahora, Ahora.AddMinutes(10)) });

        var resultado = await new ListarMisReservasQueryHandler(_consulta, _reloj).Handle(new ListarMisReservasQuery("ana"));

        Assert.Equal(["A9", "B2"], resultado.Single().Asientos.Select(a => $"{a.Fila}{a.Numero}"));
    }

    [Fact]
    public async Task Obtener_ReservaPropia_DevuelveElDetalleConEstadoEfectivo()
    {
        var vencida = Lectura(Ahora.AddMinutes(-20), Ahora.AddMinutes(-10));
        _consulta.ObtenerDeCliente(vencida.Id, "ana", Arg.Any<CancellationToken>()).Returns(vencida);

        var resultado = await new ObtenerMiReservaQueryHandler(_consulta, _reloj).Handle(new ObtenerMiReservaQuery(vencida.Id, "ana"));

        Assert.Equal(EstadoReserva.Expirada, resultado!.Estado);
    }

    [Fact]
    public async Task Obtener_ReservaAjenaOInexistente_DevuelveNull()
    {
        var resultado = await new ObtenerMiReservaQueryHandler(_consulta, _reloj).Handle(new ObtenerMiReservaQuery(Guid.NewGuid(), "beto"));

        Assert.Null(resultado);
    }
}
