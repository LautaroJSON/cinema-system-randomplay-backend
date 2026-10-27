using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Verificaciones.Commands;
using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Verificaciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.CompradoresDePrueba;

namespace CinemaSystemRandomPlay.Application.UnitTests.Verificaciones;

public class SolicitarVerificacionEmailCommandHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 27, 18, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>Unidad de trabajo falsa que cuenta las transacciones ya confirmadas.</summary>
    private sealed class UnidadDeTrabajoFake : IUnidadDeTrabajo
    {
        public int Confirmadas { get; private set; }

        public async Task<T> EjecutarEnTransaccion<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken cancellationToken = default)
        {
            var resultado = await operacion(cancellationToken);
            Confirmadas++;
            return resultado;
        }
    }

    private readonly UnidadDeTrabajoFake _unidadDeTrabajo = new();
    private readonly IVerificacionEmailRepository _repository = Substitute.For<IVerificacionEmailRepository>();
    private readonly ICodigosDeVerificacion _codigos = Substitute.For<ICodigosDeVerificacion>();
    private readonly IEnviadorDeEmail _enviador = Substitute.For<IEnviadorDeEmail>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();

    public SolicitarVerificacionEmailCommandHandlerTests()
    {
        _reloj.Ahora.Returns(Ahora);
        _codigos.Generar().Returns("493017");
        _codigos.Hashear(Arg.Any<EmailComprador>(), Arg.Any<string?>()).Returns(c => $"hash:{c.ArgAt<EmailComprador>(0)}:{c.ArgAt<string?>(1)}");
        _repository.ContarDesde(Arg.Any<EmailComprador>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(0);
        _repository.ObtenerVigente(Arg.Any<EmailComprador>(), Arg.Any<CancellationToken>()).Returns((VerificacionEmail?)null);
    }

    private SolicitarVerificacionEmailCommandHandler CrearHandler() =>
        new(_unidadDeTrabajo, _repository, _codigos, _enviador, _reloj, new ReglasVerificacion());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-email")]
    public async Task Handle_EmailInvalido_NoCreaNiEnvia(string? email)
    {
        var resultado = await CrearHandler().Handle(new SolicitarVerificacionEmailCommand(email));

        Assert.Equal(EstadoSolicitarVerificacion.EmailInvalido, resultado);
        Assert.Equal(0, _unidadDeTrabajo.Confirmadas);
        await _repository.DidNotReceiveWithAnyArgs().Agregar(default!, default);
        await _enviador.DidNotReceiveWithAnyArgs().Enviar(default!, default);
    }

    [Fact]
    public async Task Handle_PedidoValido_GuardaElHashDelCodigoYLoEnviaAlEmailNormalizado()
    {
        VerificacionEmail? guardada = null;
        await _repository.Agregar(Arg.Do<VerificacionEmail>(v => guardada = v), Arg.Any<CancellationToken>());

        var resultado = await CrearHandler().Handle(new SolicitarVerificacionEmailCommand("  ANA@Example.com "));

        Assert.Equal(EstadoSolicitarVerificacion.Aceptada, resultado);
        Assert.NotNull(guardada);
        Assert.Equal(Ana, guardada.Email);
        Assert.Equal("hash:ana@example.com:493017", guardada.CodigoHash);
        Assert.Equal(Ahora.AddMinutes(10), guardada.VenceEn);
        await _repository.Received(1).BloquearPorEmail(Ana, Arg.Any<CancellationToken>());
        await _enviador.Received(1).Enviar(Arg.Is<MensajeDeVerificacion>(m => m.Destinatario == Ana && m.Codigo == "493017"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnviaElMensajeDespuesDeConfirmarLaTransaccion()
    {
        var confirmadasAlEnviar = -1;
        _enviador.Enviar(Arg.Any<MensajeDeVerificacion>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                confirmadasAlEnviar = _unidadDeTrabajo.Confirmadas;
                return Task.CompletedTask;
            });

        await CrearHandler().Handle(new SolicitarVerificacionEmailCommand("ana@example.com"));

        Assert.Equal(1, confirmadasAlEnviar);
    }

    [Fact]
    public async Task Handle_ConUnaVerificacionVigente_LaInvalida()
    {
        var anterior = VerificacionEmail.Crear(Ana, "hash-anterior", Ahora.AddMinutes(-2), 10);
        _repository.ObtenerVigente(Ana, Arg.Any<CancellationToken>()).Returns(anterior);

        var resultado = await CrearHandler().Handle(new SolicitarVerificacionEmailCommand("ana@example.com"));

        Assert.Equal(EstadoSolicitarVerificacion.Aceptada, resultado);
        Assert.Equal(EstadoVerificacion.Invalidada, anterior.EstadoEn(Ahora));
    }

    [Fact]
    public async Task Handle_ElResultadoEsElMismoHayaOnoUnaVerificacionPrevia()
    {
        var sinPrevia = await CrearHandler().Handle(new SolicitarVerificacionEmailCommand("ana@example.com"));

        _repository.ObtenerVigente(Beto, Arg.Any<CancellationToken>()).Returns(VerificacionEmail.Crear(Beto, "h", Ahora, 10));
        var conPrevia = await CrearHandler().Handle(new SolicitarVerificacionEmailCommand("beto@example.com"));

        Assert.Equal(sinPrevia, conPrevia);
    }

    [Fact]
    public async Task Handle_ConLaCuotaDeLaVentanaAgotada_RechazaSinCrearNiEnviar()
    {
        _repository.ContarDesde(Ana, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(5);

        var resultado = await CrearHandler().Handle(new SolicitarVerificacionEmailCommand("ana@example.com"));

        Assert.Equal(EstadoSolicitarVerificacion.LimiteExcedido, resultado);
        await _repository.Received(1).ContarDesde(Ana, Ahora.AddMinutes(-60), Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().Agregar(default!, default);
        await _enviador.DidNotReceiveWithAnyArgs().Enviar(default!, default);
    }

    [Fact]
    public async Task Handle_ConUnaSolicitudMenosQueElLimite_TodaviaAcepta()
    {
        _repository.ContarDesde(Ana, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(4);

        var resultado = await CrearHandler().Handle(new SolicitarVerificacionEmailCommand("ana@example.com"));

        Assert.Equal(EstadoSolicitarVerificacion.Aceptada, resultado);
    }

    [Fact]
    public async Task Handle_SiElEnvioFalla_LaVerificacionYaQuedoPersistida()
    {
        _enviador.Enviar(Arg.Any<MensajeDeVerificacion>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("sin servicio de email")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CrearHandler().Handle(new SolicitarVerificacionEmailCommand("ana@example.com")));

        Assert.Equal(1, _unidadDeTrabajo.Confirmadas);
        await _repository.Received(1).Agregar(Arg.Any<VerificacionEmail>(), Arg.Any<CancellationToken>());
    }
}
