using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Pagos;
using MercadoPago.Resource.Payment;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

/// <summary>La traducción al SDK de Mercado Pago, sin red (research.md, Decisión 3).</summary>
public class MapeoMercadoPagoTests
{
    private static readonly Guid CarritoId = Guid.Parse("6f1c0000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Desde = new(2026, 11, 16, 20, 3, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset VenceEn = new(2026, 11, 16, 20, 6, 0, TimeSpan.FromHours(-3));

    private static SolicitudDePago Solicitud() => new(
        CarritoId,
        [new ItemDePago("Interstellar — Sala 1 — Fila C Asiento 5", 4500m), new ItemDePago("Interstellar — Sala 1 — Fila C Asiento 6", 4500m)],
        Dinero.Ars(9000m),
        Desde,
        VenceEn,
        "ana@example.com");

    private static OpcionesMercadoPago Opciones(string urlRetorno = "http://localhost:5173/carritos/{carritoId}/pago") => new()
    {
        UrlAvisos = "https://tunel.example/api/pagos/mercadopago/avisos?source_news=webhooks",
        UrlRetorno = urlRetorno
    };

    [Fact]
    public void APreferencia_LlevaItemsReferenciaVencimientoYExclusiones()
    {
        var preferencia = MapeoMercadoPago.APreferencia(Solicitud(), Opciones());

        Assert.Equal(CarritoId.ToString(), preferencia.ExternalReference);
        Assert.Equal(2, preferencia.Items.Count);
        Assert.All(preferencia.Items, i =>
        {
            Assert.Equal(1, i.Quantity);
            Assert.Equal(4500m, i.UnitPrice);
            Assert.Equal("ARS", i.CurrencyId);
        });
        Assert.Equal("Interstellar — Sala 1 — Fila C Asiento 5", preferencia.Items[0].Title);
        Assert.Equal("ana@example.com", preferencia.Payer.Email);
        Assert.True(preferencia.Expires);
        Assert.Equal(Desde.UtcDateTime, preferencia.ExpirationDateFrom);
        Assert.Equal(VenceEn.UtcDateTime, preferencia.ExpirationDateTo);
        Assert.Equal(DateTimeKind.Utc, preferencia.ExpirationDateTo!.Value.Kind);
        Assert.Equal(["ticket", "atm"], preferencia.PaymentMethods.ExcludedPaymentTypes.Select(t => t.Id));
        Assert.Equal(1, preferencia.PaymentMethods.Installments);
        Assert.True(preferencia.BinaryMode);
        Assert.Equal("https://tunel.example/api/pagos/mercadopago/avisos?source_news=webhooks", preferencia.NotificationUrl);
        Assert.Equal($"http://localhost:5173/carritos/{CarritoId}/pago", preferencia.BackUrls.Success);
        Assert.Equal(preferencia.BackUrls.Success, preferencia.BackUrls.Failure);
        Assert.Equal(preferencia.BackUrls.Success, preferencia.BackUrls.Pending);
    }

    [Fact]
    public void APreferencia_ConUrlDeRetornoHttp_NoPideAutoReturn()
    {
        Assert.Null(MapeoMercadoPago.APreferencia(Solicitud(), Opciones()).AutoReturn);
    }

    [Fact]
    public void APreferencia_ConUrlDeRetornoHttps_PideAutoReturnAprobado()
    {
        var preferencia = MapeoMercadoPago.APreferencia(Solicitud(), Opciones("https://cine.example/carritos/{carritoId}/pago"));

        Assert.Equal("approved", preferencia.AutoReturn);
    }

    [Theory]
    [InlineData("approved", EstadoPagoExterno.Aprobado)]
    [InlineData("rejected", EstadoPagoExterno.Rechazado)]
    [InlineData("pending", EstadoPagoExterno.Pendiente)]
    [InlineData("in_process", EstadoPagoExterno.Pendiente)]
    [InlineData("refunded", EstadoPagoExterno.Devuelto)]
    [InlineData("cancelled", EstadoPagoExterno.Otro)]
    [InlineData(null, EstadoPagoExterno.Otro)]
    public void AEstado_TraduceLosEstadosDeMercadoPago(string? estado, EstadoPagoExterno esperado)
    {
        Assert.Equal(esperado, MapeoMercadoPago.AEstado(estado));
    }

    [Fact]
    public void APagoConsultado_TraduceIdMontoReferenciaYAprobacionEnUtc()
    {
        var pago = new Payment
        {
            Id = 1234567890,
            Status = "approved",
            CurrencyId = "ARS",
            TransactionAmount = 9000m,
            ExternalReference = CarritoId.ToString(),
            DateApproved = new DateTime(2026, 11, 16, 23, 5, 50, DateTimeKind.Utc)
        };

        var consultado = MapeoMercadoPago.APagoConsultado(pago)!;

        Assert.Equal("1234567890", consultado.Id);
        Assert.Equal(EstadoPagoExterno.Aprobado, consultado.Estado);
        Assert.Equal(Dinero.Ars(9000m), consultado.Monto);
        Assert.Equal(CarritoId.ToString(), consultado.ReferenciaExterna);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 23, 5, 50, TimeSpan.Zero), consultado.AprobadoEn);
    }

    [Fact]
    public void APagoConsultado_EnOtraMoneda_NoEsAprobado()
    {
        var pago = new Payment { Id = 1, Status = "approved", CurrencyId = "BRL", TransactionAmount = 10m };

        Assert.Equal(EstadoPagoExterno.Otro, MapeoMercadoPago.APagoConsultado(pago)!.Estado);
    }

    [Fact]
    public void APagoConsultado_SinIdOMonto_DevuelveNull()
    {
        Assert.Null(MapeoMercadoPago.APagoConsultado(new Payment { Status = "approved" }));
    }
}
