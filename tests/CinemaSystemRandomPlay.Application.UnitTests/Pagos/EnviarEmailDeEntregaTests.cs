using System.Text.Json;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Pagos.Manejadores;
using CinemaSystemRandomPlay.Application.UnitTests.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Pagos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Pagos;

public class EnviarEmailDeEntregaTests : OperacionesSobreCarritoTestBase
{
    private readonly IEnviadorDeEmail _enviador = Substitute.For<IEnviadorDeEmail>();

    private EnviarEmailDeEntrega CrearManejador() => new(ConsultaCarritos, CarritoRepository, _enviador, UnidadDeTrabajo, Reloj);

    private Carrito Pagado(params Asiento[] asientos)
    {
        var carrito = EnPago(Existente(Ahora, asientos), Funcion, Ahora);
        var codigos = new Queue<string>(["K7M2Q-X9PDA", "ABCDE-12345", "ZZZZZ-00000"]);
        var generador = Substitute.For<IGeneradorDeCodigos>();
        generador.Generar().Returns(_ => codigos.Dequeue());
        carrito.MarcarPagado(new PagoAprobado("p1", carrito.Total, Ahora), Ahora,
            new DatosDeLaFuncion(Funcion.Id, "Interstellar", "Sucursal Centro", "Sala 1", Funcion.FechaHoraInicio), generador);
        return carrito;
    }

    private static string Payload(Carrito carrito) =>
        JsonSerializer.Serialize(new CarritoPagado(carrito.Id, carrito.FuncionId, "p1", carrito.Asientos, Ahora));

    [Fact]
    public void Tipo_EsElNombreDelEvento() => Assert.Equal("CarritoPagado", CrearManejador().Tipo);

    [Fact]
    public async Task Manejar_CarritoPagado_EnviaUnEmailConTodasLasEntradasYMarcaLaEntrega()
    {
        var carrito = Pagado(new Asiento('A', 1), new Asiento('A', 2));

        await CrearManejador().Manejar(Payload(carrito), CancellationToken.None);

        await _enviador.Received(1).Enviar(Arg.Is<MensajeDeEmail>(m =>
            m.Destinatario.Valor == "ana@example.com" &&
            m.Asunto.Contains("Interstellar") &&
            m.Cuerpo.Contains("K7M2Q-X9PDA") && m.Cuerpo.Contains("ABCDE-12345") &&
            m.Cuerpo.Contains("Sucursal Centro") && m.Cuerpo.Contains("Sala 1") &&
            m.Cuerpo.Contains("fila A, asiento 1") && m.Cuerpo.Contains("fila A, asiento 2") &&
            m.CuerpoHtml!.Contains("K7M2Q-X9PDA")), Arg.Any<CancellationToken>());
        Assert.Equal(Ahora, carrito.EntregaEnviadaEn);
    }

    [Fact]
    public async Task Manejar_EntregaYaEnviada_NoReenvia()
    {
        var carrito = Pagado(new Asiento('A', 1));
        carrito.MarcarEntregaEnviada(Ahora);

        await CrearManejador().Manejar(Payload(carrito), CancellationToken.None);

        await _enviador.DidNotReceive().Enviar(Arg.Any<MensajeDeEmail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manejar_FallaElEnvio_PropagaYNoMarcaLaEntrega()
    {
        var carrito = Pagado(new Asiento('A', 1));
        _enviador.Enviar(Arg.Any<MensajeDeEmail>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("smtp caído"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CrearManejador().Manejar(Payload(carrito), CancellationToken.None));

        Assert.Null(carrito.EntregaEnviadaEn);
        Assert.Equal(EstadoCarrito.Pagado, carrito.Estado);
    }
}
