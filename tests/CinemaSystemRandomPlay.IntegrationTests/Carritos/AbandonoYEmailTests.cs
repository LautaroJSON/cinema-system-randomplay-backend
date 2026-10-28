using System.Net;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>US3: abandonar libera de inmediato; el email de entrega se guarda normalizado, sin verificar ni enviar nada.</summary>
public class AbandonoYEmailTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public AbandonoYEmailTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Abandonar_LiberaLosAsientosYRegistraElEvento()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5", "C6");

        var response = await _factory.ClienteDe(creado).Abandonar(creado.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var carrito = await response.LeerJson();
        Assert.Equal("Abandonado", carrito.GetProperty("estado").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, carrito.GetProperty("abandonadoEn").ValueKind);
        var disponibles = await _factory.Disponibles(funcionId);
        Assert.Contains("C5", disponibles);
        Assert.Contains("C6", disponibles);
        Assert.Equal(["CarritoCreado", "CarritoAbandonado"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Abandonar_DosVeces_LaSegundaDevuelve409ConElEstado()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");
        var cliente = _factory.ClienteDe(creado);
        await cliente.Abandonar(creado.Id);

        var problema = await (await cliente.Abandonar(creado.Id)).AssertProblema(409, "carrito-no-activo");

        Assert.Equal("Abandonado", problema.GetProperty("estadoActual").GetString());
    }

    [Fact]
    public async Task IndicarEmail_LoGuardaNormalizadoSinCambiarElVencimientoNiEnviarNada()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");
        var cliente = _factory.ClienteDe(creado);
        var venceEn = (await (await cliente.ObtenerCarrito(creado.Id)).LeerJson()).GetProperty("venceEn").GetDateTimeOffset();

        var response = await cliente.IndicarEmailEntrega(creado.Id, " Ana@Example.com ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var carrito = await response.LeerJson();
        Assert.Equal("ana@example.com", carrito.GetProperty("emailEntrega").GetString());
        Assert.Equal("Activo", carrito.GetProperty("estado").GetString());
        Assert.Equal(venceEn, carrito.GetProperty("venceEn").GetDateTimeOffset());
        Assert.Equal("ana@example.com", (await (await cliente.ObtenerCarrito(creado.Id)).LeerJson()).GetProperty("emailEntrega").GetString());
        Assert.Empty(_factory.Email.Mensajes);
        Assert.Equal(["CarritoCreado"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task IndicarEmail_SePuedeCorregirMientrasEstaActivo()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");
        var cliente = _factory.ClienteDe(creado);

        await cliente.IndicarEmailEntrega(creado.Id, "ana@example.com");
        var response = await cliente.IndicarEmailEntrega(creado.Id, "beto@example.com");

        Assert.Equal("beto@example.com", (await response.LeerJson()).GetProperty("emailEntrega").GetString());
    }

    [Fact]
    public async Task IndicarEmail_Invalido_Devuelve400YConservaElAnterior()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");
        var cliente = _factory.ClienteDe(creado);
        await cliente.IndicarEmailEntrega(creado.Id, "ana@example.com");

        await (await cliente.IndicarEmailEntrega(creado.Id, "no-es-un-email")).AssertProblema(400, "pedido-invalido");
        await (await cliente.IndicarEmailEntrega(creado.Id, null)).AssertProblema(400, "pedido-invalido");

        Assert.Equal("ana@example.com", (await (await cliente.ObtenerCarrito(creado.Id)).LeerJson()).GetProperty("emailEntrega").GetString());
    }

    [Fact]
    public async Task IndicarEmail_CarritoExpirado_Devuelve409()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(5));

        var problema = await (await _factory.ClienteDe(creado).IndicarEmailEntrega(creado.Id, "ana@example.com")).AssertProblema(409, "carrito-no-activo");

        Assert.Equal("Expirado", problema.GetProperty("estadoActual").GetString());
    }
}
