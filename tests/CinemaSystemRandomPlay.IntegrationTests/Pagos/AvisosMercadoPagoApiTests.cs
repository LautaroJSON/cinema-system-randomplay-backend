using System.Net;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Carritos;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

/// <summary>Webhook firmado de punta a punta (spec 008, US5: FR-013, FR-014, FR-016, FR-018).</summary>
public class AvisosMercadoPagoApiTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public AvisosMercadoPagoApiTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<CarritoCreado> CarritoEnPago()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        return (await _factory.CrearCarritoEnPago(funcionId, "C5")).Carrito;
    }

    private async Task<string> EstadoDe(CarritoCreado carrito) =>
        (await _factory.CarritoSegunPortador(carrito)).GetProperty("estado").GetString()!;

    private string PagoAprobado(CarritoCreado carrito, decimal monto = 5500m) =>
        _factory.Pagos.RegistrarPago(carrito.Id, EstadoPagoExterno.Aprobado, Dinero.Ars(monto), _factory.Reloj.Ahora);

    [Theory]
    [InlineData("")]
    [InlineData("ts=1,v1=00")]
    [InlineData("basura")]
    public async Task SinFirmaOConFirmaInvalida_Responde401YNoCambiaNada(string firma)
    {
        var carrito = await CarritoEnPago();
        var pagoId = PagoAprobado(carrito);

        var response = await _factory.CrearCliente().EnviarAviso(pagoId, firma: firma);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("CarritoPagado", await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task FirmadoConOtraClave_Responde401()
    {
        var carrito = await CarritoEnPago();
        var pagoId = PagoAprobado(carrito);
        var firma = FirmaDeAvisos.Firmar(pagoId, "req-1", "1742505638683", "otra-clave");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CrearCliente().EnviarAviso(pagoId, firma: firma)).StatusCode);
    }

    [Fact]
    public async Task AvisoFirmadoDeUnPagoAprobado_PagaElCarrito()
    {
        var carrito = await CarritoEnPago();
        var pagoId = PagoAprobado(carrito);

        var response = await _factory.CrearCliente().EnviarAviso(pagoId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("CarritoPagado", await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task AvisoFirmadoDeUnPagoRechazado_DejaElCarritoEnPago()
    {
        var carrito = await CarritoEnPago();
        var pagoId = _factory.Pagos.RegistrarPago(carrito.Id, EstadoPagoExterno.Rechazado, Dinero.Ars(5500m), null);

        Assert.Equal(HttpStatusCode.OK, (await _factory.CrearCliente().EnviarAviso(pagoId)).StatusCode);
        Assert.Equal("Pagando", await EstadoDe(carrito));
    }

    [Fact]
    public async Task ElCuerpoDelAvisoNoSeUsaParaDecidir()
    {
        // El aviso dice "approved" en el cuerpo, pero la pasarela informa el pago como rechazado.
        var carrito = await CarritoEnPago();
        var pagoId = _factory.Pagos.RegistrarPago(carrito.Id, EstadoPagoExterno.Rechazado, Dinero.Ars(5500m), null);

        await _factory.CrearCliente().EnviarAviso(pagoId, accion: "approved");

        Assert.Equal("Pagando", await EstadoDe(carrito));
    }

    [Fact]
    public async Task OtroTipoDeRecurso_Responde200SinProcesar()
    {
        var carrito = await CarritoEnPago();
        var pagoId = PagoAprobado(carrito);

        Assert.Equal(HttpStatusCode.OK, (await _factory.CrearCliente().EnviarAviso(pagoId, tipo: "merchant_order")).StatusCode);
        Assert.DoesNotContain("CarritoPagado", await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task PagoInexistenteODeOtroSistema_Responde200SinCambios()
    {
        var carrito = await CarritoEnPago();
        var ajeno = _factory.Pagos.RegistrarPago(Guid.NewGuid(), EstadoPagoExterno.Aprobado, Dinero.Ars(5500m), _factory.Reloj.Ahora);

        Assert.Equal(HttpStatusCode.OK, (await _factory.CrearCliente().EnviarAviso("no-existe")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CrearCliente().EnviarAviso(ajeno)).StatusCode);
        Assert.Equal("Pagando", await EstadoDe(carrito));
    }

    [Fact]
    public async Task PasarelaCaida_Responde500YAlReenviarElAvisoSePaga()
    {
        var carrito = await CarritoEnPago();
        var pagoId = PagoAprobado(carrito);
        _factory.Pagos.FallarProximasLlamadas(1);

        Assert.Equal(HttpStatusCode.InternalServerError, (await _factory.CrearCliente().EnviarAviso(pagoId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CrearCliente().EnviarAviso(pagoId)).StatusCode);
        Assert.Equal("Pagado", await EstadoDe(carrito));
    }
}
