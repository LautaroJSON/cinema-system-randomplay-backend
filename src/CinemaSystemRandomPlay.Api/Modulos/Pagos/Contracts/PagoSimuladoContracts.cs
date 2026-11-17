using System.Text.Json.Serialization;

namespace CinemaSystemRandomPlay.Api.Modulos.Pagos.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<EstadoPagoSimulado>))]
public enum EstadoPagoSimulado
{
    Aprobado,
    Rechazado,
    Pendiente
}

/// <param name="CarritoId">Carrito que referencia el pago.</param>
/// <param name="Estado"><c>Aprobado</c>, <c>Rechazado</c> o <c>Pendiente</c>.</param>
/// <param name="Monto">Monto pagado; por defecto, el total del carrito.</param>
/// <param name="AprobadoEn">Hora de aprobación; por defecto, ahora.</param>
public record PagoSimuladoRequest(Guid CarritoId, EstadoPagoSimulado Estado, decimal? Monto, DateTimeOffset? AprobadoEn);

/// <param name="PagoId">Id del pago simulado.</param>
/// <param name="EstadoCarrito">Estado del carrito después de procesar el pago.</param>
public record PagoSimuladoResponse(string PagoId, string EstadoCarrito);
