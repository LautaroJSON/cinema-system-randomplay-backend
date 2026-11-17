using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CinemaSystemRandomPlay.Infrastructure.Outbox;

/// <summary>
/// Publica los mensajes del Outbox fuera de la transacción que los registró (Principio VI; specs/008-pago-mercado-pago/research.md,
/// Decisión 10). Toma un lote con <c>FOR UPDATE SKIP LOCKED</c> y le pone un lease, así que varias instancias no procesan el
/// mismo mensaje. Cada mensaje se ejecuta en su propio scope con el manejador de su tipo; si falla, se reintenta con espera
/// exponencial hasta <see cref="OpcionesOutbox.MaximoIntentos"/> y después queda registrado como fallido. Los tipos sin manejador
/// (eventos que todavía nadie consume) se marcan procesados sin acción.
/// </summary>
public class ProcesadorDeOutbox : BackgroundService
{
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OpcionesOutbox _opciones;
    private readonly ILogger<ProcesadorDeOutbox> _logger;

    public ProcesadorDeOutbox(IServiceScopeFactory scopeFactory, OpcionesOutbox opciones, ILogger<ProcesadorDeOutbox> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.Habilitado)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _opciones.IntervaloSegundos)));

        do
        {
            try
            {
                while (await ProcesarLote(stoppingToken) > 0 && !stoppingToken.IsCancellationRequested)
                {
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falló la toma de mensajes del Outbox; se reintenta en el próximo ciclo.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Toma y procesa un lote de mensajes listos.</summary>
    /// <returns>Cantidad de mensajes tomados.</returns>
    public async Task<int> ProcesarLote(CancellationToken cancellationToken = default)
    {
        var tomados = await TomarLote(cancellationToken);

        foreach (var mensaje in tomados)
            await Procesar(mensaje, cancellationToken);

        return tomados.Count;
    }

    private async Task<IReadOnlyList<OutboxMensaje>> TomarLote(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var ahora = scope.ServiceProvider.GetRequiredService<IReloj>().Ahora.ToUniversalTime();

        await using var transaccion = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var mensajes = await dbContext.OutboxMensajes
            .FromSqlInterpolated($"""
                SELECT * FROM "OutboxMensajes"
                WHERE "ProcesadoEn" IS NULL AND "FallidoEn" IS NULL
                  AND ("ProximoIntentoEn" IS NULL OR "ProximoIntentoEn" <= {ahora})
                ORDER BY "OcurridoEn"
                LIMIT {_opciones.TamanoLote}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var mensaje in mensajes)
            mensaje.ProximoIntentoEn = ahora.AddMinutes(_opciones.MinutosLease);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return mensajes;
    }

    private async Task Procesar(OutboxMensaje mensaje, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var manejador = scope.ServiceProvider.GetServices<IManejadorDeMensaje>().FirstOrDefault(m => m.Tipo == mensaje.Tipo);

        string? error = null;
        try
        {
            if (manejador is not null)
                await manejador.Manejar(mensaje.Payload, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            _logger.LogWarning(ex, "Falló el mensaje {MensajeId} del Outbox ({Tipo}), intento {Intento}.", mensaje.Id, mensaje.Tipo,
                mensaje.Intentos + 1);
        }

        await using var scopeDeRegistro = _scopeFactory.CreateAsyncScope();
        var dbContext = scopeDeRegistro.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var ahora = scopeDeRegistro.ServiceProvider.GetRequiredService<IReloj>().Ahora.ToUniversalTime();

        if (error is null)
        {
            await dbContext.OutboxMensajes.Where(m => m.Id == mensaje.Id).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ProcesadoEn, ahora)
                .SetProperty(m => m.ProximoIntentoEn, (DateTimeOffset?)null), cancellationToken);
            return;
        }

        var intentos = mensaje.Intentos + 1;
        var errorRecortado = error.Length <= OutboxMensajeEntityConfiguration.LargoMaximoError
            ? error
            : error[..OutboxMensajeEntityConfiguration.LargoMaximoError];

        if (intentos >= _opciones.MaximoIntentos)
        {
            await dbContext.OutboxMensajes.Where(m => m.Id == mensaje.Id).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Intentos, intentos)
                .SetProperty(m => m.UltimoError, errorRecortado)
                .SetProperty(m => m.FallidoEn, ahora)
                .SetProperty(m => m.ProximoIntentoEn, (DateTimeOffset?)null), cancellationToken);

            _logger.LogError("El mensaje {MensajeId} del Outbox ({Tipo}) agotó sus {Intentos} intentos y quedó como fallido.",
                mensaje.Id, mensaje.Tipo, intentos);

            try
            {
                await manejador!.AlAgotarIntentos(mensaje.Payload, errorRecortado, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falló el cierre del mensaje fallido {MensajeId} del Outbox ({Tipo}).", mensaje.Id, mensaje.Tipo);
            }

            return;
        }

        await dbContext.OutboxMensajes.Where(m => m.Id == mensaje.Id).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Intentos, intentos)
            .SetProperty(m => m.UltimoError, errorRecortado)
            .SetProperty(m => m.ProximoIntentoEn, ahora.Add(Espera(intentos))), cancellationToken);
    }

    /// <summary>10 s, 20 s, 40 s, ... hasta 1 hora.</summary>
    public static TimeSpan Espera(int intentos)
    {
        var segundos = 10 * Math.Pow(2, Math.Min(intentos - 1, 20));
        return TimeSpan.FromSeconds(Math.Min(segundos, EsperaMaxima.TotalSeconds));
    }
}
