using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Carritos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

/// <summary>Volver de la página de pago antes del aviso (spec 008, US2: FR-017b, FR-024).</summary>
public class ConsultaDuranteElPagoTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public ConsultaDuranteElPagoTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid FuncionId, CarritoCreado Carrito)> CarritoEnPago()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        return (funcionId, (await _factory.CrearCarritoEnPago(funcionId, "C5")).Carrito);
    }

    [Fact]
    public async Task SinPago_MuestraPagandoConLaUrlYSinEntradas()
    {
        var (_, carrito) = await CarritoEnPago();

        var estado = await _factory.CarritoSegunPortador(carrito);

        Assert.Equal("Pagando", estado.GetProperty("estado").GetString());
        Assert.StartsWith("/api/dev/pagos-simulados/", estado.GetProperty("pago").GetProperty("urlPago").GetString());
        Assert.Empty(estado.GetProperty("entradas").EnumerateArray());
    }

    [Fact]
    public async Task PagoAprobadoSinAviso_LaConsultaLoProcesaYElAvisoPosteriorNoDuplica()
    {
        var (_, carrito) = await CarritoEnPago();
        var pagoId = _factory.Pagos.RegistrarPago(carrito.Id, EstadoPagoExterno.Aprobado, Dinero.Ars(5500m), _factory.Reloj.Ahora);

        var estado = await _factory.CarritoSegunPortador(carrito);
        await _factory.CrearCliente().EnviarAviso(pagoId);
        await _factory.CarritoSegunPortador(carrito);

        Assert.Equal("Pagado", estado.GetProperty("estado").GetString());
        Assert.Single(estado.GetProperty("entradas").EnumerateArray());
        Assert.Single(await _factory.TiposDeOutbox(), t => t == "CarritoPagado");
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.Equal(1, await dbContext.PagosProcesados.CountAsync());
        Assert.Equal(1, await dbContext.Entradas.CountAsync());
    }

    [Fact]
    public async Task PasarelaLentaOCaida_RespondeElEstadoConocido()
    {
        var (_, carrito) = await CarritoEnPago();

        _factory.Pagos.DemoraDeConsulta = TimeSpan.FromSeconds(10);
        var lenta = await _factory.CarritoSegunPortador(carrito);
        _factory.Pagos.DemoraDeConsulta = TimeSpan.Zero;
        _factory.Pagos.FallarProximasLlamadas(1);
        var caida = await _factory.CarritoSegunPortador(carrito);

        Assert.Equal("Pagando", lenta.GetProperty("estado").GetString());
        Assert.Equal("Pagando", caida.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task VencidoSinPago_MuestraExpiradoYLiberaLosAsientos()
    {
        var (funcionId, carrito) = await CarritoEnPago();

        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(7));

        Assert.Equal("Expirado", (await _factory.CarritoSegunPortador(carrito)).GetProperty("estado").GetString());
        Assert.Contains("C5", await _factory.Disponibles(funcionId));
    }

    [Fact]
    public async Task AprobadoATiempoYConsultadoDespuesDelVencimiento_SePagaSiLosAsientosSiguenLibres()
    {
        var (_, carrito) = await CarritoEnPago();
        var venceEn = (await _factory.CarritoSegunPortador(carrito)).GetProperty("venceEn").GetDateTimeOffset();
        _factory.Pagos.RegistrarPago(carrito.Id, EstadoPagoExterno.Aprobado, Dinero.Ars(5500m), venceEn.AddSeconds(-10));

        _factory.Reloj.Avanzar(venceEn - _factory.Reloj.Ahora + TimeSpan.FromSeconds(20));

        Assert.Equal("Pagado", (await _factory.CarritoSegunPortador(carrito)).GetProperty("estado").GetString());
    }
}
