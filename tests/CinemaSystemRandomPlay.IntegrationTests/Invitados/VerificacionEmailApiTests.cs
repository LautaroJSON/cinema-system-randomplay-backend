using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Reservas;
using static CinemaSystemRandomPlay.IntegrationTests.Reservas.ReservasTestHelpers;

namespace CinemaSystemRandomPlay.IntegrationTests.Invitados;

/// <summary>User Story 1: verificar el email y obtener una credencial de invitado.</summary>
public class VerificacionEmailApiTests : IClassFixture<ReservasApiFactory>
{
    private readonly ReservasApiFactory _factory;

    public VerificacionEmailApiTests(ReservasApiFactory factory)
    {
        _factory = factory;
    }

    private static string EmailNuevo() => $"u{Guid.NewGuid():N}@example.com";

    private Task<HttpResponseMessage> Pedir(string email) =>
        _factory.CrearCliente().PostAsJsonAsync("/api/verificaciones-email", new { email });

    private Task<HttpResponseMessage> Confirmar(string email, string codigo) =>
        _factory.CrearCliente().PostAsJsonAsync("/api/verificaciones-email/confirmacion", new { email, codigo });

    private static string OtroCodigo(string codigo) => ((int.Parse(codigo) + 1) % 1_000_000).ToString("D6");

    private async Task<string> ObtenerCredencial(string email)
    {
        Assert.Equal(HttpStatusCode.Accepted, (await Pedir(email)).StatusCode);
        var response = await Confirmar(email, _factory.Email.UltimoCodigoDe(email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.LeerJson()).GetProperty("credencial").GetString()!;
    }

    [Fact]
    public async Task FlujoCompleto_PedirConfirmarYReservarConLaCredencialObtenida()
    {
        var email = EmailNuevo();
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));

        var pedido = await Pedir(email);
        var confirmacion = await Confirmar(email, _factory.Email.UltimoCodigoDe(email));

        Assert.Equal(HttpStatusCode.Accepted, pedido.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmacion.StatusCode);
        var cuerpo = await confirmacion.LeerJson();
        Assert.Equal("Bearer", cuerpo.GetProperty("tipo").GetString());
        var venceEn = cuerpo.GetProperty("venceEn").GetDateTimeOffset();
        Assert.InRange(venceEn, _factory.Reloj.Ahora.AddMinutes(59), _factory.Reloj.Ahora.AddMinutes(61));

        var cliente = _factory.CrearCliente(cuerpo.GetProperty("credencial").GetString());
        var reserva = await cliente.Reservar(funcionId, "C5", "C6");
        Assert.Equal(HttpStatusCode.Created, reserva.StatusCode);
        var lista = await (await cliente.GetAsync("/api/reservas")).LeerJson();
        Assert.Equal(1, lista.GetArrayLength());
    }

    [Fact]
    public async Task Pedir_EmailConMayusculasYEspacios_SeTrataComoElMismoEmail()
    {
        var email = EmailNuevo();

        var pedido = await Pedir($"  {email.ToUpperInvariant()} ");
        var confirmacion = await Confirmar(email, _factory.Email.UltimoCodigoDe(email));

        Assert.Equal(HttpStatusCode.Accepted, pedido.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmacion.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-email")]
    [InlineData("   ")]
    public async Task Pedir_EmailInvalido_Devuelve400YNoEnviaNada(string email)
    {
        var antes = _factory.Email.Mensajes.Count;

        var response = await Pedir(email);

        await response.AssertProblema(400, "pedido-invalido");
        Assert.Equal(antes, _factory.Email.Mensajes.Count);
    }

    [Fact]
    public async Task Pedir_LaRespuestaEsIdenticaParaUnEmailConReservasYUnoSinEllas()
    {
        var conReservas = EmailNuevo();
        var sinReservas = EmailNuevo();
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var credencial = await ObtenerCredencial(conReservas);
        await _factory.CrearCliente(credencial).ReservarOk(funcionId, "A1");

        var a = await Pedir(conReservas);
        var b = await Pedir(sinReservas);

        Assert.Equal(HttpStatusCode.Accepted, a.StatusCode);
        Assert.Equal(a.StatusCode, b.StatusCode);
        Assert.Equal(await a.Content.ReadAsStringAsync(), await b.Content.ReadAsStringAsync());
        Assert.Equal(a.Content.Headers.ContentType, b.Content.Headers.ContentType);
    }

    [Fact]
    public async Task Pedir_UnSegundoPedido_InvalidaElCodigoAnterior()
    {
        var email = EmailNuevo();
        await Pedir(email);
        var primero = _factory.Email.UltimoCodigoDe(email);
        await Pedir(email);
        var segundo = _factory.Email.UltimoCodigoDe(email);
        Assert.NotEqual(primero, segundo);

        var conElViejo = await Confirmar(email, primero);
        var conElNuevo = await Confirmar(email, segundo);

        await conElViejo.AssertProblema(400, "codigo-invalido");
        Assert.Equal(HttpStatusCode.OK, conElNuevo.StatusCode);
    }

    [Fact]
    public async Task Confirmar_UnCodigoYaUsado_SeRechazaYNoEntregaCredencial()
    {
        var email = EmailNuevo();
        await Pedir(email);
        var codigo = _factory.Email.UltimoCodigoDe(email);
        Assert.Equal(HttpStatusCode.OK, (await Confirmar(email, codigo)).StatusCode);

        var segundo = await Confirmar(email, codigo);

        var problema = await segundo.AssertProblema(400, "codigo-invalido");
        Assert.False(problema.TryGetProperty("credencial", out _));
    }

    [Fact]
    public async Task Confirmar_CodigoIncorrecto_Devuelve400()
    {
        var email = EmailNuevo();
        await Pedir(email);

        var response = await Confirmar(email, OtroCodigo(_factory.Email.UltimoCodigoDe(email)));

        await response.AssertProblema(400, "codigo-invalido");
    }

    [Fact]
    public async Task Confirmar_CodigoVencido_Devuelve410IndicandoQueVencio()
    {
        var email = EmailNuevo();
        await Pedir(email);
        var codigo = _factory.Email.UltimoCodigoDe(email);
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(11));

        var response = await Confirmar(email, codigo);

        await response.AssertProblema(410, "codigo-vencido");
    }

    [Fact]
    public async Task Confirmar_AlAgotarLosIntentos_InvalidaElCodigoYNiElCorrectoFunciona()
    {
        var email = EmailNuevo();
        await Pedir(email);
        var codigo = _factory.Email.UltimoCodigoDe(email);
        var incorrecto = OtroCodigo(codigo);

        for (var i = 1; i < 5; i++)
            await (await Confirmar(email, incorrecto)).AssertProblema(400, "codigo-invalido");
        await (await Confirmar(email, incorrecto)).AssertProblema(400, "codigo-invalidado");
        var conElCorrecto = await Confirmar(email, codigo);

        // El código invalidado ya no está vigente: el rechazo es el genérico, sin dar pistas.
        var problema = await conElCorrecto.AssertProblema(400, "codigo-invalido");
        Assert.False(problema.TryGetProperty("credencial", out _));
    }

    [Fact]
    public async Task Confirmar_ElCodigoDeOtroEmailNoDaAccesoAEsteEmail()
    {
        var ana = EmailNuevo();
        var beto = EmailNuevo();
        await Pedir(ana);
        await Pedir(beto);

        var response = await Confirmar(beto, _factory.Email.UltimoCodigoDe(ana));

        await response.AssertProblema(400, "codigo-invalido");
    }

    [Fact]
    public async Task Confirmar_ConUnEmailQueNuncaPidioCodigo_Devuelve400CodigoInvalido()
    {
        var response = await Confirmar(EmailNuevo(), "123456");

        await response.AssertProblema(400, "codigo-invalido");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("")]
    public async Task Confirmar_CodigoQueNoSonSeisDigitos_Devuelve400PedidoInvalido(string codigo)
    {
        var response = await Confirmar(EmailNuevo(), codigo);

        await response.AssertProblema(400, "pedido-invalido");
    }

    [Fact]
    public async Task Pedir_ElPedidoSeisDelMismoEmailEnLaVentana_Devuelve429SinEnviarNada()
    {
        var email = EmailNuevo();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await Pedir(email)).StatusCode);

        var sexto = await Pedir(email);

        await sexto.AssertProblema(429, "demasiados-pedidos");
        Assert.Equal(5, _factory.Email.CantidadDe(email));
    }

    [Fact]
    public async Task Pedir_ElLimiteDeUnEmailNoAfectaAOtro()
    {
        var saturado = EmailNuevo();
        for (var i = 0; i < 5; i++)
            await Pedir(saturado);

        var otro = await Pedir(EmailNuevo());

        Assert.Equal(HttpStatusCode.Accepted, otro.StatusCode);
    }
}
