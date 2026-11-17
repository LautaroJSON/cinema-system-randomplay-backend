using System.Text.Json;
using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Manejadores;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Pagos;
using CinemaSystemRandomPlay.Domain.Pagos.Eventos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Pagos;

public class EjecutarDevolucionTests
{
    private readonly IRegistroDePagos _registro = Substitute.For<IRegistroDePagos>();
    private readonly IProcesadorDePagos _procesador = Substitute.For<IProcesadorDePagos>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();
    private readonly Devolucion _devolucion;

    public EjecutarDevolucionTests()
    {
        _reloj.Ahora.Returns(Ahora);
        _devolucion = new Devolucion(Guid.NewGuid(), new PagoAprobado("p1", Dinero.Ars(5500m), Ahora), Guid.NewGuid(),
            MotivoDevolucion.PagoTardio, Ahora);
        _registro.ObtenerDevolucion(_devolucion.Id, Arg.Any<CancellationToken>()).Returns(_devolucion);
    }

    private EjecutarDevolucion CrearManejador() => new(_registro, _procesador, new UnidadDeTrabajoFake(), _reloj);

    private string Payload() =>
        JsonSerializer.Serialize(new DevolucionSolicitada(_devolucion.Id, "p1", _devolucion.CarritoId, _devolucion.Motivo, Ahora));

    [Fact]
    public async Task Manejar_Pendiente_DevuelveConLaClaveDeIdempotenciaYLaCompleta()
    {
        await CrearManejador().Manejar(Payload(), CancellationToken.None);

        await _procesador.Received(1).Devolver("p1", _devolucion.Id, Arg.Any<CancellationToken>());
        Assert.Equal(EstadoDevolucion.Completada, _devolucion.Estado);
        Assert.Equal(Ahora, _devolucion.ResueltaEn);
    }

    [Fact]
    public async Task Manejar_YaCompletada_NoLlamaALaPasarela()
    {
        _devolucion.Completar(Ahora);

        await CrearManejador().Manejar(Payload(), CancellationToken.None);

        await _procesador.DidNotReceive().Devolver(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manejar_FallaLaPasarela_PropagaYQuedaPendiente()
    {
        _procesador.Devolver("p1", _devolucion.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProcesadorDePagosNoDisponibleException("caída"));

        await Assert.ThrowsAsync<ProcesadorDePagosNoDisponibleException>(() => CrearManejador().Manejar(Payload(), CancellationToken.None));

        Assert.Equal(EstadoDevolucion.Pendiente, _devolucion.Estado);
    }

    [Fact]
    public async Task AlAgotarIntentos_LaMarcaFallidaConElError()
    {
        await CrearManejador().AlAgotarIntentos(Payload(), "Mercado Pago no respondió", CancellationToken.None);

        Assert.Equal(EstadoDevolucion.Fallida, _devolucion.Estado);
        Assert.Equal("Mercado Pago no respondió", _devolucion.UltimoError);
    }
}
