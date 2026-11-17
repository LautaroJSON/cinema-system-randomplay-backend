using CinemaSystemRandomPlay.Infrastructure.Outbox;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Carritos;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

/// <summary>Email de entrega por el Outbox (spec 008, US3: FR-025, FR-026, SC-008).</summary>
public class EmailDeEntregaTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public EmailDeEntregaTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(CarritoCreado Carrito, string PagoId)> CarritoPagado()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var (carrito, _) = await _factory.CrearCarritoEnPago(funcionId, "C5", "C6");
        var simulado = await (await _factory.CrearCliente().SimularPago(carrito.Id)).LeerJson();
        return (carrito, simulado.GetProperty("pagoId").GetString()!);
    }

    [Fact]
    public async Task CompraConfirmada_EnviaUnSoloEmailConTodasLasEntradas()
    {
        var (carrito, _) = await CarritoPagado();
        var codigos = (await _factory.CarritoSegunPortador(carrito)).GetProperty("entradas").EnumerateArray()
            .Select(e => e.GetProperty("codigo").GetString()!).ToList();

        await _factory.ProcesarOutbox();
        await _factory.ProcesarOutbox();

        var mensaje = Assert.Single(_factory.Email.Mensajes);
        Assert.Equal("ana@example.com", mensaje.Destinatario.Valor);
        Assert.Contains("Interstellar", mensaje.Asunto);
        Assert.All(codigos, codigo => Assert.Contains(codigo, mensaje.Cuerpo));
    }

    [Fact]
    public async Task ElMismoPagoProcesadoDosVeces_EnviaUnSoloEmail()
    {
        var (_, pagoId) = await CarritoPagado();

        // El pago ya acreditado vuelve a llegar por el aviso: no genera otro CarritoPagado.
        await _factory.CrearCliente().EnviarAviso(pagoId);
        await _factory.ProcesarOutbox();

        Assert.Single(_factory.Email.Mensajes);
    }

    [Fact]
    public async Task FallaElEnvio_SeReintentaYLaCompraSigueValida()
    {
        var (carrito, _) = await CarritoPagado();
        _factory.Email.FallarProximosEnvios(1);

        await _factory.ProcesarOutbox();

        Assert.Empty(_factory.Email.Mensajes);
        Assert.Equal("Pagado", (await _factory.CarritoSegunPortador(carrito)).GetProperty("estado").GetString());

        _factory.Reloj.Avanzar(ProcesadorDeOutbox.Espera(1) + TimeSpan.FromSeconds(1));
        await _factory.ProcesarOutbox();

        Assert.Single(_factory.Email.Mensajes);
    }
}
