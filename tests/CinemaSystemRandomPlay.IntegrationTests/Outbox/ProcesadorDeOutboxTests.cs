using System.Collections.Concurrent;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Infrastructure.Outbox;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Outbox;
using CinemaSystemRandomPlay.IntegrationTests.Carritos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Outbox;

/// <summary>Manejador de prueba: cuenta ejecuciones por mensaje y falla mientras se le pida.</summary>
public sealed class ManejadorDePrueba : IManejadorDeMensaje
{
    public const string TipoDePrueba = "EventoDePrueba";

    public ConcurrentDictionary<string, int> Ejecuciones { get; } = new();
    public ConcurrentBag<string> Agotados { get; } = new();
    public bool Fallar { get; set; }

    public string Tipo => TipoDePrueba;

    public async Task Manejar(string payload, CancellationToken cancellationToken)
    {
        Ejecuciones.AddOrUpdate(payload, 1, (_, n) => n + 1);
        await Task.Delay(5, cancellationToken);
        if (Fallar)
            throw new InvalidOperationException("falla de prueba");
    }

    public Task AlAgotarIntentos(string payload, string error, CancellationToken cancellationToken)
    {
        Agotados.Add(payload);
        return Task.CompletedTask;
    }
}

public class OutboxApiFactory : CarritosApiFactory
{
    public ManejadorDePrueba Manejador { get; } = new();

    protected override IReadOnlyDictionary<string, string?> ConfiguracionAdicional => new Dictionary<string, string?>
    {
        ["Outbox:MaximoIntentos"] = "3"
    };

    protected override void ConfigurarServiciosAdicionales(IServiceCollection services) =>
        services.AddSingleton<IManejadorDeMensaje>(Manejador);
}

/// <summary>Procesador del Outbox contra Postgres real (research.md, Decisión 10; Principio VI).</summary>
public class ProcesadorDeOutboxTests : IClassFixture<OutboxApiFactory>, IAsyncLifetime
{
    private readonly OutboxApiFactory _factory;

    public ProcesadorDeOutboxTests(OutboxApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.LimpiarDatos();
        _factory.Manejador.Ejecuciones.Clear();
        _factory.Manejador.Agotados.Clear();
        _factory.Manejador.Fallar = false;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ProcesadorDeOutbox Procesador => _factory.Services.GetRequiredService<ProcesadorDeOutbox>();

    private async Task<List<Guid>> Registrar(string tipo, int cantidad)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var ids = new List<Guid>();
        for (var i = 0; i < cantidad; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            dbContext.OutboxMensajes.Add(new OutboxMensaje
            {
                Id = id, Tipo = tipo, Payload = $"\"{id}\"", OcurridoEn = _factory.Reloj.Ahora.AddMilliseconds(i)
            });
        }

        await dbContext.SaveChangesAsync();
        return ids;
    }

    private async Task<OutboxMensaje> Mensaje(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CinemaDbContext>().OutboxMensajes.AsNoTracking().SingleAsync(m => m.Id == id);
    }

    [Fact]
    public async Task MensajeConManejador_QuedaProcesado()
    {
        var id = (await Registrar(ManejadorDePrueba.TipoDePrueba, 1)).Single();

        Assert.Equal(1, await Procesador.ProcesarLote());

        var mensaje = await Mensaje(id);
        Assert.NotNull(mensaje.ProcesadoEn);
        Assert.Null(mensaje.ProximoIntentoEn);
        Assert.Equal(1, _factory.Manejador.Ejecuciones[$"\"{id}\""]);
        Assert.Equal(0, await Procesador.ProcesarLote());
    }

    [Fact]
    public async Task TipoSinManejador_QuedaProcesadoSinAccion()
    {
        var id = (await Registrar("CarritoCreado", 1)).Single();

        await Procesador.ProcesarLote();

        Assert.NotNull((await Mensaje(id)).ProcesadoEn);
        Assert.Empty(_factory.Manejador.Ejecuciones);
    }

    [Fact]
    public async Task Falla_SeReintentaRecienDespuesDeLaEspera()
    {
        var id = (await Registrar(ManejadorDePrueba.TipoDePrueba, 1)).Single();
        _factory.Manejador.Fallar = true;

        await Procesador.ProcesarLote();

        var fallido = await Mensaje(id);
        Assert.Null(fallido.ProcesadoEn);
        Assert.Equal(1, fallido.Intentos);
        Assert.Contains("falla de prueba", fallido.UltimoError);
        Assert.True(fallido.ProximoIntentoEn > _factory.Reloj.Ahora);
        Assert.Equal(0, await Procesador.ProcesarLote());

        _factory.Manejador.Fallar = false;
        _factory.Reloj.Avanzar(ProcesadorDeOutbox.Espera(1) + TimeSpan.FromSeconds(1));

        Assert.Equal(1, await Procesador.ProcesarLote());
        Assert.NotNull((await Mensaje(id)).ProcesadoEn);
        Assert.Equal(2, _factory.Manejador.Ejecuciones[$"\"{id}\""]);
    }

    [Fact]
    public async Task AlAgotarLosIntentos_QuedaFallidoYAvisaAlManejador()
    {
        var id = (await Registrar(ManejadorDePrueba.TipoDePrueba, 1)).Single();
        _factory.Manejador.Fallar = true;

        for (var intento = 1; intento <= 3; intento++)
        {
            Assert.Equal(1, await Procesador.ProcesarLote());
            _factory.Reloj.Avanzar(TimeSpan.FromHours(2));
        }

        var mensaje = await Mensaje(id);
        Assert.NotNull(mensaje.FallidoEn);
        Assert.Equal(3, mensaje.Intentos);
        Assert.Equal([$"\"{id}\""], _factory.Manejador.Agotados);
        Assert.Equal(0, await Procesador.ProcesarLote());
    }

    [Fact]
    public async Task LotesConcurrentes_NuncaEjecutanDosVecesElMismoMensaje()
    {
        var ids = await Registrar(ManejadorDePrueba.TipoDePrueba, 60);

        var puerta = new TaskCompletionSource();
        var tareas = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await puerta.Task;
            while (await Procesador.ProcesarLote() > 0)
            {
            }
        })).ToList();
        puerta.SetResult();
        await Task.WhenAll(tareas);

        Assert.Equal(ids.Count, _factory.Manejador.Ejecuciones.Count);
        Assert.All(_factory.Manejador.Ejecuciones.Values, n => Assert.Equal(1, n));
    }
}
