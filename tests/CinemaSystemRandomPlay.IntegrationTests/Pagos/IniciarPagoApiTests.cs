using System.Net;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Carritos;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

/// <summary><c>POST /api/carritos/{id}/pago</c> (spec 008, US1: FR-001 a FR-009).</summary>
public class IniciarPagoApiTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public IniciarPagoApiTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    private Task<Guid> SembrarFuncion(TimeSpan? desdeAhora = null) =>
        _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, desdeAhora ?? TimeSpan.FromHours(3));

    [Fact]
    public async Task CarritoActivoConEmail_DevuelveLaUrlYPasaAPagandoConUnMinutoMas()
    {
        var funcionId = await SembrarFuncion();
        var carrito = await _factory.CrearCarritoConEmail(funcionId, "C5", "C6");
        var venceEn = (await _factory.CarritoSegunPortador(carrito)).GetProperty("venceEn").GetDateTimeOffset();

        var response = await _factory.ClienteDe(carrito).IniciarPago(carrito.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cuerpo = await response.LeerJson();
        var urlPago = cuerpo.GetProperty("urlPago").GetString()!;
        Assert.StartsWith("/api/dev/pagos-simulados/", urlPago);
        var enPago = cuerpo.GetProperty("carrito");
        Assert.Equal("Pagando", enPago.GetProperty("estado").GetString());
        Assert.Equal(venceEn.AddMinutes(1), enPago.GetProperty("venceEn").GetDateTimeOffset());
        Assert.Equal(urlPago, enPago.GetProperty("pago").GetProperty("urlPago").GetString());
        Assert.Empty(enPago.GetProperty("entradas").EnumerateArray());

        var solicitud = Assert.Single(_factory.Pagos.Solicitudes.Values);
        Assert.Equal(carrito.Id, solicitud.CarritoId);
        Assert.Equal(2, solicitud.Items.Count);
        Assert.Equal(11000m, solicitud.Total.Monto);
        Assert.Equal(venceEn.AddMinutes(1), solicitud.VenceEn);
        Assert.Contains("PagoIniciado", await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task SegundaLlamada_DevuelveLaMismaUrlSinExtenderNiCrearOtroPago()
    {
        var funcionId = await SembrarFuncion();
        var (carrito, primera) = await _factory.CrearCarritoEnPago(funcionId, "C5");

        var segunda = await (await _factory.ClienteDe(carrito).IniciarPago(carrito.Id)).LeerJson();

        Assert.Equal(primera.GetProperty("urlPago").GetString(), segunda.GetProperty("urlPago").GetString());
        Assert.Equal(primera.GetProperty("carrito").GetProperty("venceEn").GetDateTimeOffset(),
            segunda.GetProperty("carrito").GetProperty("venceEn").GetDateTimeOffset());
        Assert.Single(_factory.Pagos.Solicitudes);
    }

    [Fact]
    public async Task FuncionQueEmpiezaAntesDelMinutoExtra_VenceEnElInicio()
    {
        var funcionId = await SembrarFuncion(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)));
        var carrito = await _factory.CrearCarritoConEmail(funcionId, "C5");
        var inicio = (await _factory.CarritoSegunPortador(carrito)).GetProperty("funcion").GetProperty("horaInicio").GetDateTimeOffset();

        var cuerpo = await (await _factory.ClienteDe(carrito).IniciarPago(carrito.Id)).LeerJson();

        Assert.Equal(inicio, cuerpo.GetProperty("carrito").GetProperty("venceEn").GetDateTimeOffset());
    }

    [Fact]
    public async Task SinEmail_Responde409EmailEntregaFaltante()
    {
        var funcionId = await SembrarFuncion();
        var carrito = await _factory.CrearCarritoOk(funcionId, "C5");

        await (await _factory.ClienteDe(carrito).IniciarPago(carrito.Id)).AssertProblema(409, "email-entrega-faltante");

        Assert.Equal("Activo", (await _factory.CarritoSegunPortador(carrito)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task CarritoAbandonadoOExpirado_Responde409CarritoNoActivo()
    {
        var funcionId = await SembrarFuncion();
        var abandonado = await _factory.CrearCarritoConEmail(funcionId, "C5");
        await _factory.ClienteDe(abandonado).Abandonar(abandonado.Id);
        var expirado = await _factory.CrearCarritoConEmail(funcionId, "C6");
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(6));

        var sobreAbandonado = await (await _factory.ClienteDe(abandonado).IniciarPago(abandonado.Id)).AssertProblema(409, "carrito-no-activo");
        var sobreExpirado = await (await _factory.ClienteDe(expirado).IniciarPago(expirado.Id)).AssertProblema(409, "carrito-no-activo");

        Assert.Equal("Abandonado", sobreAbandonado.GetProperty("estadoActual").GetString());
        Assert.Equal("Expirado", sobreExpirado.GetProperty("estadoActual").GetString());
    }

    [Fact]
    public async Task PasarelaCaida_Responde503YElCarritoNoCambia()
    {
        var funcionId = await SembrarFuncion();
        var carrito = await _factory.CrearCarritoConEmail(funcionId, "C5");
        var antes = await _factory.CarritoSegunPortador(carrito);
        _factory.Pagos.FallarProximasLlamadas(1);

        await (await _factory.ClienteDe(carrito).IniciarPago(carrito.Id)).AssertProblema(503, "pago-no-disponible");

        var despues = await _factory.CarritoSegunPortador(carrito);
        Assert.Equal("Activo", despues.GetProperty("estado").GetString());
        Assert.Equal(antes.GetProperty("venceEn").GetDateTimeOffset(), despues.GetProperty("venceEn").GetDateTimeOffset());
    }

    [Fact]
    public async Task SinTokenOConTokenAjeno_Responde401O404()
    {
        var funcionId = await SembrarFuncion();
        var carrito = await _factory.CrearCarritoConEmail(funcionId, "C5");
        var otro = await _factory.CrearCarritoOk(funcionId, "C6");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CrearCliente().IniciarPago(carrito.Id)).StatusCode);
        await (await _factory.ClienteDe(otro).IniciarPago(carrito.Id)).AssertProblema(404, "carrito-no-encontrado");
    }

    [Fact]
    public async Task EnPago_NoSePuedeReemplazarAbandonarNiCambiarElEmail()
    {
        var funcionId = await SembrarFuncion();
        var (carrito, _) = await _factory.CrearCarritoEnPago(funcionId, "C5");
        var cliente = _factory.ClienteDe(carrito);

        await (await cliente.ReemplazarAsientos(carrito.Id, "C7")).AssertProblema(409, "carrito-en-pago");
        await (await cliente.Abandonar(carrito.Id)).AssertProblema(409, "carrito-en-pago");
        await (await cliente.IndicarEmailEntrega(carrito.Id, "beto@example.com")).AssertProblema(409, "carrito-en-pago");

        var estado = await _factory.CarritoSegunPortador(carrito);
        Assert.Equal(["C5"], estado.Asientos());
        Assert.Equal("ana@example.com", estado.GetProperty("emailEntrega").GetString());
    }
}
