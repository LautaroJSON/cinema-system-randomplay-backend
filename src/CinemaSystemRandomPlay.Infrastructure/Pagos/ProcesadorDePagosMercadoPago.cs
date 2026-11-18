using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using MercadoPago.Client;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Error;

namespace CinemaSystemRandomPlay.Infrastructure.Pagos;

/// <summary>
/// Mercado Pago Checkout Pro con el SDK oficial (spec 008). Devuelve <c>init_point</c>: Mercado Pago retiró el sandbox y
/// <c>sandbox_init_point</c> termina en una página de error. Lo que hace que un pago sea de prueba es el access token del
/// vendedor de prueba, no la URL. El access token viaja en cada pedido (<see cref="RequestOptions"/>), sin configuración
/// global estática, y nunca se registra en el log.
/// </summary>
public class ProcesadorDePagosMercadoPago : IProcesadorDePagos
{
    private readonly OpcionesMercadoPago _opciones;
    private readonly PreferenceClient _preferencias = new();
    private readonly PaymentClient _pagos = new();
    private readonly PaymentRefundClient _devoluciones = new();

    public ProcesadorDePagosMercadoPago(OpcionesMercadoPago opciones)
    {
        _opciones = opciones;
    }

    public async Task<PagoCreado> CrearPago(SolicitudDePago solicitud, CancellationToken cancellationToken = default)
    {
        var preferencia = await Llamar(() => _preferencias.CreateAsync(MapeoMercadoPago.APreferencia(solicitud, _opciones),
            Opciones(), cancellationToken), cancellationToken);

        var url = preferencia.InitPoint;
        if (string.IsNullOrWhiteSpace(preferencia.Id) || string.IsNullOrWhiteSpace(url))
            throw new ProcesadorDePagosNoDisponibleException("Mercado Pago no devolvió la preferencia de pago.");

        return new PagoCreado(preferencia.Id, url);
    }

    public async Task<PagoConsultado?> ObtenerPago(string pagoId, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(pagoId, out var id))
            return null;

        try
        {
            var pago = await _pagos.GetAsync(id, Opciones(), cancellationToken);
            return MapeoMercadoPago.APagoConsultado(pago);
        }
        catch (MercadoPagoApiException ex) when (ex.StatusCode is 404 or 400)
        {
            return null;
        }
        catch (Exception ex) when (EsFallaDeLaPasarela(ex, cancellationToken))
        {
            throw new ProcesadorDePagosNoDisponibleException("Mercado Pago no respondió al consultar el pago.", ex);
        }
    }

    public async Task<IReadOnlyList<PagoConsultado>> BuscarPagosDelCarrito(Guid carritoId, CancellationToken cancellationToken = default)
    {
        var busqueda = new SearchRequest
        {
            Limit = 30,
            Filters = new Dictionary<string, object> { ["external_reference"] = carritoId.ToString() }
        };

        var pagina = await Llamar(() => _pagos.SearchAsync(busqueda, Opciones(), cancellationToken), cancellationToken);

        return (pagina.Results ?? [])
            .Select(MapeoMercadoPago.APagoConsultado)
            .OfType<PagoConsultado>()
            .ToList();
    }

    public async Task Devolver(string pagoId, Guid claveIdempotencia, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(pagoId, out var id))
            throw new InvalidOperationException($"El id de pago '{pagoId}' no es de Mercado Pago.");

        var opciones = Opciones();
        opciones.CustomHeaders["X-Idempotency-Key"] = claveIdempotencia.ToString();

        await Llamar(() => _devoluciones.RefundAsync(id, opciones, cancellationToken), cancellationToken);
    }

    private RequestOptions Opciones() => new() { AccessToken = _opciones.AccessToken };

    private static async Task<T> Llamar<T>(Func<Task<T>> llamada, CancellationToken cancellationToken)
    {
        try
        {
            return await llamada();
        }
        catch (Exception ex) when (EsFallaDeLaPasarela(ex, cancellationToken))
        {
            throw new ProcesadorDePagosNoDisponibleException("Mercado Pago no respondió o rechazó el pedido.", ex);
        }
    }

    /// <summary>Errores de red, timeouts y errores de la API. No incluye la cancelación del propio pedido.</summary>
    private static bool EsFallaDeLaPasarela(Exception ex, CancellationToken cancellationToken) =>
        ex is MercadoPagoException or MercadoPagoApiException or HttpRequestException ||
        (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
