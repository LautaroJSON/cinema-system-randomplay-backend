using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Application.Pagos.Ports;

/// <summary>Un ítem de la página de pago: un asiento con su precio unitario.</summary>
public record ItemDePago(string Titulo, decimal PrecioUnitario);

/// <param name="VenceEn">Momento en que la pasarela deja de aceptar pagos; es el vencimiento extendido del carrito.</param>
public record SolicitudDePago(
    Guid CarritoId,
    IReadOnlyList<ItemDePago> Items,
    Dinero Total,
    DateTimeOffset Desde,
    DateTimeOffset VenceEn,
    string EmailPagador);

public record PagoCreado(string PreferenciaId, string UrlPago);

public enum EstadoPagoExterno
{
    Aprobado,
    Rechazado,
    Pendiente,
    Devuelto,
    Otro
}

/// <param name="ReferenciaExterna">Id del carrito con el que se creó el pago (texto: puede venir de otra integración).</param>
/// <param name="AprobadoEn">Hora de aprobación informada por la pasarela; <c>null</c> si no está aprobado.</param>
public record PagoConsultado(
    string Id,
    EstadoPagoExterno Estado,
    Dinero Monto,
    string? ReferenciaExterna,
    DateTimeOffset? AprobadoEn);

/// <summary>
/// Pasarela de pago (spec 008: Mercado Pago Checkout Pro). El pago ocurre en la página de la pasarela: este puerto nunca recibe
/// datos de tarjeta. Las implementaciones lanzan <see cref="ProcesadorDePagosNoDisponibleException"/> ante fallas de red o de
/// la pasarela. Nunca se invoca dentro de una transacción de base de datos (Principio VI).
/// </summary>
public interface IProcesadorDePagos
{
    Task<PagoCreado> CrearPago(SolicitudDePago solicitud, CancellationToken cancellationToken = default);

    /// <returns><c>null</c> si el pago no existe.</returns>
    Task<PagoConsultado?> ObtenerPago(string pagoId, CancellationToken cancellationToken = default);

    /// <summary>Pagos creados para un carrito (búsqueda por referencia externa).</summary>
    Task<IReadOnlyList<PagoConsultado>> BuscarPagosDelCarrito(Guid carritoId, CancellationToken cancellationToken = default);

    /// <summary>Devolución total. La clave de idempotencia evita devolver dos veces si se reintenta.</summary>
    Task Devolver(string pagoId, Guid claveIdempotencia, CancellationToken cancellationToken = default);
}
