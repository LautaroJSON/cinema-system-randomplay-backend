using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using MercadoPago.Client.Preference;
using MercadoPago.Resource.Payment;

namespace CinemaSystemRandomPlay.Infrastructure.Pagos;

/// <summary>
/// Traducción entre el puerto de pagos y el SDK de Mercado Pago (specs/008-pago-mercado-pago/research.md, Decisión 3). Es pura:
/// se testea sin red.
/// </summary>
public static class MapeoMercadoPago
{
    public const string Moneda = "ARS";

    /// <summary>Efectivo en puntos de pago (Rapipago, Pago Fácil) y cajero: no son instantáneos (FR-007).</summary>
    public static readonly IReadOnlyList<string> TiposDePagoExcluidos = ["ticket", "atm"];

    public static PreferenceRequest APreferencia(SolicitudDePago solicitud, OpcionesMercadoPago opciones)
    {
        var urlRetorno = opciones.UrlRetornoPara(solicitud.CarritoId);

        return new PreferenceRequest
        {
            Items = solicitud.Items
                .Select(i => new PreferenceItemRequest
                {
                    Title = i.Titulo,
                    Quantity = 1,
                    UnitPrice = i.PrecioUnitario,
                    CurrencyId = Moneda
                })
                .ToList(),
            Payer = new PreferencePayerRequest { Email = solicitud.EmailPagador },
            ExternalReference = solicitud.CarritoId.ToString(),
            NotificationUrl = string.IsNullOrWhiteSpace(opciones.UrlAvisos) ? null : opciones.UrlAvisos,
            BackUrls = new PreferenceBackUrlsRequest { Success = urlRetorno, Failure = urlRetorno, Pending = urlRetorno },
            // Mercado Pago rechaza auto_return con URLs que no son https (por ejemplo, http://localhost).
            AutoReturn = urlRetorno.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "approved" : null,
            Expires = true,
            ExpirationDateFrom = solicitud.Desde.UtcDateTime,
            ExpirationDateTo = solicitud.VenceEn.UtcDateTime,
            PaymentMethods = new PreferencePaymentMethodsRequest
            {
                ExcludedPaymentTypes = TiposDePagoExcluidos.Select(t => new PreferencePaymentTypeRequest { Id = t }).ToList(),
                Installments = 1
            },
            // Aprueba o rechaza en el momento, sin pagos "en revisión".
            BinaryMode = true
        };
    }

    /// <returns><c>null</c> si el pago no trae los datos mínimos (id y monto).</returns>
    public static PagoConsultado? APagoConsultado(Payment pago)
    {
        if (pago.Id is null || pago.TransactionAmount is null)
            return null;

        // El sistema cobra solo en pesos: un pago en otra moneda no puede pagar un carrito y no se procesa.
        var estado = pago.CurrencyId == Moneda ? AEstado(pago.Status) : EstadoPagoExterno.Otro;

        return new PagoConsultado(
            pago.Id.Value.ToString(),
            estado,
            new Dinero(decimal.Round(pago.TransactionAmount.Value, 2), Domain.Compartido.Moneda.ARS),
            pago.ExternalReference,
            pago.DateApproved is { } aprobado ? AUtc(aprobado) : null);
    }

    /// <summary>El SDK entrega las fechas como <see cref="DateTime"/>; sin zona se interpretan como UTC.</summary>
    public static DateTimeOffset AUtc(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(fecha, TimeSpan.Zero),
        DateTimeKind.Local => new DateTimeOffset(fecha.ToUniversalTime(), TimeSpan.Zero),
        _ => new DateTimeOffset(DateTime.SpecifyKind(fecha, DateTimeKind.Utc), TimeSpan.Zero)
    };

    public static EstadoPagoExterno AEstado(string? estado) => estado switch
    {
        "approved" => EstadoPagoExterno.Aprobado,
        "rejected" => EstadoPagoExterno.Rechazado,
        "pending" or "in_process" or "authorized" => EstadoPagoExterno.Pendiente,
        "refunded" or "charged_back" => EstadoPagoExterno.Devuelto,
        _ => EstadoPagoExterno.Otro
    };
}
