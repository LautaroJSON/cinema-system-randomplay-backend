using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Verificaciones.Commands;
using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Verificaciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.CompradoresDePrueba;

namespace CinemaSystemRandomPlay.Application.UnitTests.Verificaciones;

public class ConfirmarVerificacionEmailCommandHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 27, 18, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Vencimiento = Ahora.AddMinutes(60);

    private readonly Reservas.ReservasTestSupport.UnidadDeTrabajoFake _unidadDeTrabajo = new();
    private readonly IVerificacionEmailRepository _repository = Substitute.For<IVerificacionEmailRepository>();
    private readonly ICodigosDeVerificacion _codigos = Substitute.For<ICodigosDeVerificacion>();
    private readonly IEmisorDeCredencial _emisor = Substitute.For<IEmisorDeCredencial>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();

    public ConfirmarVerificacionEmailCommandHandlerTests()
    {
        _reloj.Ahora.Returns(Ahora);
        _codigos.Hashear(Arg.Any<EmailComprador>(), Arg.Any<string?>()).Returns(c => $"hash:{c.ArgAt<string?>(1)}");
        _emisor.Emitir(Arg.Any<EmailComprador>(), Arg.Any<DateTimeOffset>()).Returns(new CredencialEmitida("credencial-jwt", Vencimiento));
    }

    private ConfirmarVerificacionEmailCommandHandler CrearHandler() =>
        new(_unidadDeTrabajo, _repository, _codigos, _emisor, _reloj, new ReglasVerificacion());

    private VerificacionEmail ConVerificacionVigente(string codigo = "493017", DateTimeOffset? creadaEn = null)
    {
        var verificacion = VerificacionEmail.Crear(Ana, $"hash:{codigo}", creadaEn ?? Ahora.AddMinutes(-1), 10);
        _repository.ObtenerVigente(Ana, Arg.Any<CancellationToken>()).Returns(verificacion);
        return verificacion;
    }

    [Fact]
    public async Task Handle_CodigoCorrecto_EmiteLaCredencialDelEmailNormalizado()
    {
        var verificacion = ConVerificacionVigente();

        var resultado = await CrearHandler().Handle(new ConfirmarVerificacionEmailCommand(" ANA@example.com", "493017"));

        Assert.Equal(EstadoConfirmarVerificacion.Ok, resultado.Estado);
        Assert.Equal(new CredencialEmitida("credencial-jwt", Vencimiento), resultado.Credencial);
        _emisor.Received(1).Emitir(Ana, Ahora);
        Assert.Equal(EstadoVerificacion.Usada, verificacion.EstadoEn(Ahora));
    }

    [Fact]
    public async Task Handle_CodigoIncorrecto_RechazaSuma1IntentoYNoEmiteCredencial()
    {
        var verificacion = ConVerificacionVigente();

        var resultado = await CrearHandler().Handle(new ConfirmarVerificacionEmailCommand("ana@example.com", "000000"));

        Assert.Equal(EstadoConfirmarVerificacion.CodigoInvalido, resultado.Estado);
        Assert.Null(resultado.Credencial);
        Assert.Equal(1, verificacion.IntentosFallidos);
        Assert.Equal(1, _unidadDeTrabajo.Ejecuciones);
        _emisor.DidNotReceiveWithAnyArgs().Emitir(default!, default);
    }

    [Fact]
    public async Task Handle_SinVerificacionVigente_EsIndistinguibleDeUnCodigoIncorrecto()
    {
        _repository.ObtenerVigente(Ana, Arg.Any<CancellationToken>()).Returns((VerificacionEmail?)null);

        var resultado = await CrearHandler().Handle(new ConfirmarVerificacionEmailCommand("ana@example.com", "493017"));

        Assert.Equal(EstadoConfirmarVerificacion.CodigoInvalido, resultado.Estado);
        _emisor.DidNotReceiveWithAnyArgs().Emitir(default!, default);
    }

    [Fact]
    public async Task Handle_CodigoYaUsado_YaNoHayVigenteYSeRechaza()
    {
        var verificacion = ConVerificacionVigente();
        await CrearHandler().Handle(new ConfirmarVerificacionEmailCommand("ana@example.com", "493017"));
        _repository.ObtenerVigente(Ana, Arg.Any<CancellationToken>()).Returns((VerificacionEmail?)null);

        var segundo = await CrearHandler().Handle(new ConfirmarVerificacionEmailCommand("ana@example.com", "493017"));

        Assert.Equal(EstadoVerificacion.Usada, verificacion.EstadoEn(Ahora));
        Assert.Equal(EstadoConfirmarVerificacion.CodigoInvalido, segundo.Estado);
    }

    [Fact]
    public async Task Handle_CodigoVencido_RechazaIndicandoQueVencio()
    {
        ConVerificacionVigente(creadaEn: Ahora.AddMinutes(-11));

        var resultado = await CrearHandler().Handle(new ConfirmarVerificacionEmailCommand("ana@example.com", "493017"));

        Assert.Equal(EstadoConfirmarVerificacion.CodigoVencido, resultado.Estado);
        _emisor.DidNotReceiveWithAnyArgs().Emitir(default!, default);
    }

    [Fact]
    public async Task Handle_AlAgotarLosIntentos_InvalidaElCodigoYNingunIntentoEmiteCredencial()
    {
        var verificacion = ConVerificacionVigente();
        var handler = CrearHandler();

        var resultados = new List<EstadoConfirmarVerificacion>();
        for (var i = 0; i < 5; i++)
            resultados.Add((await handler.Handle(new ConfirmarVerificacionEmailCommand("ana@example.com", "000000"))).Estado);
        var conElCorrecto = await handler.Handle(new ConfirmarVerificacionEmailCommand("ana@example.com", "493017"));

        Assert.Equal([.. Enumerable.Repeat(EstadoConfirmarVerificacion.CodigoInvalido, 4), EstadoConfirmarVerificacion.CodigoInvalidado], resultados);
        Assert.Equal(EstadoVerificacion.Invalidada, verificacion.EstadoEn(Ahora));
        Assert.Equal(EstadoConfirmarVerificacion.CodigoInvalidado, conElCorrecto.Estado);
        _emisor.DidNotReceiveWithAnyArgs().Emitir(default!, default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-es-un-email")]
    public async Task Handle_EmailInvalido_NoConsultaNada(string? email)
    {
        var resultado = await CrearHandler().Handle(new ConfirmarVerificacionEmailCommand(email, "493017"));

        Assert.Equal(EstadoConfirmarVerificacion.EmailInvalido, resultado.Estado);
        Assert.Equal(0, _unidadDeTrabajo.Ejecuciones);
        await _repository.DidNotReceiveWithAnyArgs().ObtenerVigente(default!, default);
    }
}
