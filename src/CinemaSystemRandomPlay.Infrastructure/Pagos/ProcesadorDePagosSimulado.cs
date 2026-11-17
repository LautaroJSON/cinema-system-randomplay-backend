using System.Collections.Concurrent;
using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Infrastructure.Pagos;

/// <summary>
/// Pasarela en memoria para tests y desarrollo sin túnel (specs/008-pago-mercado-pago/research.md, Decisión 2). Se registra como
/// <c>Singleton</c>: los pagos que crean el endpoint de simulación o los tests los ve el resto del sistema.
/// </summary>
public sealed class ProcesadorDePagosSimulado : IProcesadorDePagos
{
    public const string RutaPagina = "/api/dev/pagos-simulados/";

    private readonly ConcurrentDictionary<string, SolicitudDePago> _solicitudes = new();
    private readonly ConcurrentDictionary<string, PagoConsultado> _pagos = new();
    private readonly ConcurrentDictionary<Guid, string> _devoluciones = new();
    private int _fallasPendientes;

    /// <summary>Solicitudes recibidas, por id de preferencia.</summary>
    public IReadOnlyDictionary<string, SolicitudDePago> Solicitudes => _solicitudes;

    /// <summary>Devoluciones pedidas, por clave de idempotencia (id del pago devuelto).</summary>
    public IReadOnlyDictionary<Guid, string> Devoluciones => _devoluciones;

    /// <summary>Demora artificial de las consultas, para probar los timeouts.</summary>
    public TimeSpan DemoraDeConsulta { get; set; } = TimeSpan.Zero;

    /// <summary>Las próximas <paramref name="cantidad"/> llamadas fallan como si la pasarela no respondiera.</summary>
    public void FallarProximasLlamadas(int cantidad) => Interlocked.Exchange(ref _fallasPendientes, cantidad);

    public void Reiniciar()
    {
        _solicitudes.Clear();
        _pagos.Clear();
        _devoluciones.Clear();
        DemoraDeConsulta = TimeSpan.Zero;
        Interlocked.Exchange(ref _fallasPendientes, 0);
    }

    /// <summary>Registra un pago como si el comprador lo hubiera hecho en la página de pago.</summary>
    public string RegistrarPago(Guid carritoId, EstadoPagoExterno estado, Dinero monto, DateTimeOffset? aprobadoEn)
    {
        var id = $"sim-{Guid.NewGuid():N}";
        _pagos[id] = new PagoConsultado(id, estado, monto, carritoId.ToString(),
            estado == EstadoPagoExterno.Aprobado ? aprobadoEn : null);
        return id;
    }

    public Task<PagoCreado> CrearPago(SolicitudDePago solicitud, CancellationToken cancellationToken = default)
    {
        FallarSiCorresponde();

        var preferenciaId = $"sim-pref-{Guid.NewGuid():N}";
        _solicitudes[preferenciaId] = solicitud;
        return Task.FromResult(new PagoCreado(preferenciaId, RutaPagina + preferenciaId));
    }

    public async Task<PagoConsultado?> ObtenerPago(string pagoId, CancellationToken cancellationToken = default)
    {
        await Demorar(cancellationToken);
        FallarSiCorresponde();

        return _pagos.TryGetValue(pagoId, out var pago) ? pago : null;
    }

    public async Task<IReadOnlyList<PagoConsultado>> BuscarPagosDelCarrito(Guid carritoId, CancellationToken cancellationToken = default)
    {
        await Demorar(cancellationToken);
        FallarSiCorresponde();

        var referencia = carritoId.ToString();
        return _pagos.Values.Where(p => p.ReferenciaExterna == referencia).ToList();
    }

    public Task Devolver(string pagoId, Guid claveIdempotencia, CancellationToken cancellationToken = default)
    {
        FallarSiCorresponde();

        // Misma clave, misma devolución: un reintento no devuelve dos veces, igual que con X-Idempotency-Key.
        if (_devoluciones.TryAdd(claveIdempotencia, pagoId) && _pagos.TryGetValue(pagoId, out var pago))
            _pagos[pagoId] = pago with { Estado = EstadoPagoExterno.Devuelto };

        return Task.CompletedTask;
    }

    private async Task Demorar(CancellationToken cancellationToken)
    {
        if (DemoraDeConsulta > TimeSpan.Zero)
            await Task.Delay(DemoraDeConsulta, cancellationToken);
    }

    private void FallarSiCorresponde()
    {
        while (true)
        {
            var pendientes = Volatile.Read(ref _fallasPendientes);
            if (pendientes <= 0)
                return;

            if (Interlocked.CompareExchange(ref _fallasPendientes, pendientes - 1, pendientes) == pendientes)
                throw new ProcesadorDePagosNoDisponibleException("Pasarela simulada no disponible.");
        }
    }
}
