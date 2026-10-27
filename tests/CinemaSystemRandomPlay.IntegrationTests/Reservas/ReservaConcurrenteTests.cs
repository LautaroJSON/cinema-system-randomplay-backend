using System.Net;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CinemaSystemRandomPlay.IntegrationTests.Reservas.ReservasTestHelpers;

namespace CinemaSystemRandomPlay.IntegrationTests.Reservas;

/// <summary>
/// Constitución, Principio V: las condiciones de carrera se prueban contra Postgres real, vía HTTP, sin dobles.
/// </summary>
public class ReservaConcurrenteTests : IClassFixture<ReservasApiFactory>
{
    private readonly ReservasApiFactory _factory;

    public ReservaConcurrenteTests(ReservasApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task VeintePedidosSimultaneosSobreElMismoAsiento_UnoGanaYElRestoRecibe409()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        const int pedidos = 20;
        var puerta = new TaskCompletionSource();

        var tareas = Enumerable.Range(0, pedidos).Select(async i =>
        {
            var cliente = _factory.CrearCliente(TokensDePrueba.Invitado($"cliente-{i}@example.com"));
            await puerta.Task;
            return await cliente.Reservar(funcionId, "D8");
        }).ToList();

        puerta.SetResult();
        var respuestas = await Task.WhenAll(tareas);

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(pedidos - 1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var rechazada in respuestas.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            var problema = await rechazada.LeerJson();
            Assert.Equal("asientos-no-disponibles", problema.GetProperty("codigo").GetString());
            Assert.Equal(["D8"], problema.Asientos());
        }

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.Equal(1, await dbContext.Reservas.CountAsync());
        Assert.Equal(["ReservaCreada"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task PedidosSimultaneosQueSeSolapanParcialmente_NingunAsientoQuedaRetenidoDosVeces()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var puerta = new TaskCompletionSource();

        // Cada pedido toma 3 asientos consecutivos de la fila E; los vecinos se solapan.
        var tareas = Enumerable.Range(1, 10).Select(async i =>
        {
            var cliente = _factory.CrearCliente(TokensDePrueba.Invitado($"cliente-{i}@example.com"));
            await puerta.Task;
            return await cliente.Reservar(funcionId, $"E{i}", $"E{i + 1}", $"E{i + 2}");
        }).ToList();

        puerta.SetResult();
        await Task.WhenAll(tareas);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var asientos = (await dbContext.Reservas.ToListAsync()).SelectMany(r => r.Asientos).ToList();
        Assert.NotEmpty(asientos);
        Assert.Equal(asientos.Count, asientos.Distinct().Count());
    }

    [Fact]
    public async Task MismoClienteConDosPedidosDeSeisAsientosEnParalelo_SoloUnoProspera()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var token = TokensDePrueba.Invitado("ana@example.com");
        var puerta = new TaskCompletionSource();

        var primero = Task.Run(async () =>
        {
            await puerta.Task;
            return await _factory.CrearCliente(token).Reservar(funcionId, "A1", "A2", "A3", "A4", "A5", "A6");
        });
        var segundo = Task.Run(async () =>
        {
            await puerta.Task;
            return await _factory.CrearCliente(token).Reservar(funcionId, "B1", "B2", "B3", "B4", "B5", "B6");
        });

        puerta.SetResult();
        var respuestas = await Task.WhenAll(primero, segundo);

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        var rechazada = Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        var problema = await rechazada.LeerJson();
        Assert.Equal("limite-asientos-superado", problema.GetProperty("codigo").GetString());
        Assert.Equal(4, problema.GetProperty("asientosRestantes").GetInt32());
    }
}
