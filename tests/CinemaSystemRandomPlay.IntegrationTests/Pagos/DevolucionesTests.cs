using CinemaSystemRandomPlay.Domain.Pagos;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Carritos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

/// <summary>Devoluciones automáticas (spec 008, US4: FR-019 a FR-021, SC-005).</summary>
public class DevolucionesTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public DevolucionesTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid FuncionId, CarritoCreado Carrito, DateTimeOffset VenceEn)> CarritoEnPago()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var (carrito, respuesta) = await _factory.CrearCarritoEnPago(funcionId, "C5");
        return (funcionId, carrito, respuesta.GetProperty("carrito").GetProperty("venceEn").GetDateTimeOffset());
    }

    private void AvanzarHasta(DateTimeOffset momento) => _factory.Reloj.Avanzar(momento - _factory.Reloj.Ahora);

    private async Task<List<Devolucion>> Devoluciones()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CinemaDbContext>().Devoluciones.AsNoTracking().ToListAsync();
    }

    private async Task<string> Simular(CarritoCreado carrito, decimal? monto = null, DateTimeOffset? aprobadoEn = null)
    {
        var cuerpo = await (await _factory.CrearCliente().SimularPago(carrito.Id, monto: monto, aprobadoEn: aprobadoEn)).LeerJson();
        return cuerpo.GetProperty("estadoCarrito").GetString()!;
    }

    [Fact]
    public async Task AprobadoDespuesDelVencimiento_SeDevuelveComoPagoTardio()
    {
        var (_, carrito, venceEn) = await CarritoEnPago();
        AvanzarHasta(venceEn.AddMinutes(1));

        Assert.Equal("Expirado", await Simular(carrito, aprobadoEn: venceEn.AddMinutes(1)));
        await _factory.ProcesarOutbox();

        var devolucion = Assert.Single(await Devoluciones());
        Assert.Equal(MotivoDevolucion.PagoTardio, devolucion.Motivo);
        Assert.Equal(EstadoDevolucion.Completada, devolucion.Estado);
        Assert.Equal(devolucion.PagoId, Assert.Single(_factory.Pagos.Devoluciones).Value);
        Assert.Empty((await _factory.CarritoSegunPortador(carrito)).GetProperty("entradas").EnumerateArray());
    }

    [Fact]
    public async Task AprobadoATiempoConAvisoTardioYAsientosLibres_SePaga()
    {
        var (_, carrito, venceEn) = await CarritoEnPago();
        AvanzarHasta(venceEn.AddSeconds(20));

        Assert.Equal("Pagado", await Simular(carrito, aprobadoEn: venceEn.AddSeconds(-10)));
        Assert.Empty(await Devoluciones());
    }

    [Fact]
    public async Task AprobadoATiempoPeroOtroCarritoTomoUnAsiento_SeDevuelve()
    {
        var (funcionId, carrito, venceEn) = await CarritoEnPago();
        AvanzarHasta(venceEn.AddSeconds(5));
        await _factory.CrearCarritoOk(funcionId, "C5");

        Assert.Equal("Expirado", await Simular(carrito, aprobadoEn: venceEn.AddSeconds(-10)));

        Assert.Equal(MotivoDevolucion.AsientosNoDisponibles, Assert.Single(await Devoluciones()).Motivo);
    }

    [Fact]
    public async Task SegundoPagoDeUnCarritoPagado_SeDevuelveSinTocarLasEntradas()
    {
        var (_, carrito, _) = await CarritoEnPago();
        await Simular(carrito);
        var entradas = (await _factory.CarritoSegunPortador(carrito)).GetProperty("entradas").GetRawText();

        Assert.Equal("Pagado", await Simular(carrito));

        Assert.Equal(MotivoDevolucion.CarritoYaPagado, Assert.Single(await Devoluciones()).Motivo);
        Assert.Equal(entradas, (await _factory.CarritoSegunPortador(carrito)).GetProperty("entradas").GetRawText());
    }

    [Fact]
    public async Task MontoDistinto_SeDevuelveYElCarritoSigueEnPago()
    {
        var (_, carrito, _) = await CarritoEnPago();

        Assert.Equal("Pagando", await Simular(carrito, monto: 1m));

        var devolucion = Assert.Single(await Devoluciones());
        Assert.Equal(MotivoDevolucion.MontoDistinto, devolucion.Motivo);
        Assert.Equal(1m, devolucion.Monto.Monto);
    }

    [Fact]
    public async Task AvisoRepetidoDeUnPagoDevuelto_NoPideOtraDevolucion()
    {
        var (_, carrito, _) = await CarritoEnPago();
        var pagoId = (await (await _factory.CrearCliente().SimularPago(carrito.Id, monto: 1m)).LeerJson()).GetProperty("pagoId").GetString()!;
        await _factory.ProcesarOutbox();

        await _factory.CrearCliente().EnviarAviso(pagoId);
        await _factory.CrearCliente().EnviarAviso(pagoId);
        await _factory.ProcesarOutbox();

        Assert.Single(await Devoluciones());
        Assert.Single(_factory.Pagos.Devoluciones);
    }

    [Fact]
    public async Task FallaLaPasarelaAlDevolver_SeReintentaYSeDevuelveUnaSolaVez()
    {
        var (_, carrito, _) = await CarritoEnPago();
        await Simular(carrito, monto: 1m);
        _factory.Pagos.FallarProximasLlamadas(1);

        await _factory.ProcesarOutbox();
        Assert.Equal(EstadoDevolucion.Pendiente, Assert.Single(await Devoluciones()).Estado);

        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(1));
        await _factory.ProcesarOutbox();

        Assert.Equal(EstadoDevolucion.Completada, Assert.Single(await Devoluciones()).Estado);
        Assert.Single(_factory.Pagos.Devoluciones);
    }

    [Fact]
    public async Task FallaSiempre_QuedaFallidaAlAgotarLosIntentos()
    {
        var (_, carrito, _) = await CarritoEnPago();
        await Simular(carrito, monto: 1m);
        _factory.Pagos.FallarProximasLlamadas(1000);

        for (var intento = 0; intento < 8; intento++)
        {
            await _factory.ProcesarOutbox();
            _factory.Reloj.Avanzar(TimeSpan.FromHours(2));
        }

        _factory.Pagos.FallarProximasLlamadas(0);
        var devolucion = Assert.Single(await Devoluciones());
        Assert.Equal(EstadoDevolucion.Fallida, devolucion.Estado);
        Assert.NotNull(devolucion.UltimoError);
        Assert.Empty(_factory.Pagos.Devoluciones);
    }
}
