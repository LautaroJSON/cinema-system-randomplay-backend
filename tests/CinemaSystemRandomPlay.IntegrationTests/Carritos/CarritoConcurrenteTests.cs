using System.Net;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>
/// Constitución, Principio V: las condiciones de carrera se prueban contra Postgres real, vía HTTP, sin dobles. Cubre la
/// creación (FR-012), el reemplazo atómico (FR-008) y el tope global por función (FR-022) con pedidos simultáneos.
/// </summary>
public class CarritoConcurrenteTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public CarritoConcurrenteTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Lanza todas las operaciones a la vez, detrás de una misma puerta.</summary>
    private static async Task<HttpResponseMessage[]> EnParalelo(IEnumerable<Func<Task<HttpResponseMessage>>> operaciones)
    {
        var puerta = new TaskCompletionSource();
        var tareas = operaciones.Select(operacion => Task.Run(async () =>
        {
            await puerta.Task;
            return await operacion();
        })).ToList();

        puerta.SetResult();
        return await Task.WhenAll(tareas);
    }

    private async Task AssertNingunAsientoEnDosCarritos(Guid funcionId)
    {
        var retenidos = await _factory.AsientosRetenidos(funcionId);
        Assert.Equal(retenidos.Count, retenidos.Distinct().Count());
    }

    [Fact]
    public async Task VeinteCreacionesSimultaneasDelMismoAsiento_UnaGanaYElRestoRecibe409()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        const int pedidos = 20;

        var respuestas = await EnParalelo(Enumerable.Range(0, pedidos)
            .Select(_ => (Func<Task<HttpResponseMessage>>)(() => _factory.CrearCliente().CrearCarrito(funcionId, "D8"))));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(pedidos - 1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var rechazada in respuestas.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Equal(["D8"], (await rechazada.AssertProblema(409, "asientos-no-disponibles")).Asientos());

        Assert.Single(await _factory.CarritosGuardados());
        Assert.Equal(["CarritoCreado"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task CreacionesSimultaneasQueSeSolapan_NingunAsientoQuedaEnDosCarritos()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));

        // Cada pedido toma 3 asientos consecutivos de la fila E; los vecinos se solapan.
        await EnParalelo(Enumerable.Range(1, 10)
            .Select(i => (Func<Task<HttpResponseMessage>>)(() => _factory.CrearCliente().CrearCarrito(funcionId, $"E{i}", $"E{i + 1}", $"E{i + 2}"))));

        Assert.NotEmpty(await _factory.AsientosRetenidos(funcionId));
        await AssertNingunAsientoEnDosCarritos(funcionId);
    }

    [Fact]
    public async Task DosCarritosReemplazanEnParaleloHaciaElMismoAsiento_UnoGanaYElOtroConservaSuSeleccion()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var a = await _factory.CrearCarritoOk(funcionId, "H1");
        var b = await _factory.CrearCarritoOk(funcionId, "H2");

        var respuestas = await EnParalelo(
        [
            () => _factory.ClienteDe(a).ReemplazarAsientos(a.Id, "H5"),
            () => _factory.ClienteDe(b).ReemplazarAsientos(b.Id, "H5")
        ]);

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        var rechazada = Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(["H5"], (await rechazada.AssertProblema(409, "asientos-no-disponibles")).Asientos());

        var carritos = (await _factory.CarritosGuardados()).ToDictionary(c => c.Id);
        var ganoA = respuestas[0].StatusCode == HttpStatusCode.OK;
        Assert.Equal([new Asiento('H', 5)], carritos[ganoA ? a.Id : b.Id].Asientos);
        Assert.Equal([new Asiento('H', ganoA ? 2 : 1)], carritos[ganoA ? b.Id : a.Id].Asientos);
        await AssertNingunAsientoEnDosCarritos(funcionId);
    }

    [Fact]
    public async Task ReemplazoConcurrenteConCreacionesQuePidenUnAsientoViejo_NuncaQuedaUnAsientoEnDosCarritos()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var a = await _factory.CrearCarritoOk(funcionId, "C5", "C6");

        var operaciones = new List<Func<Task<HttpResponseMessage>>> { () => _factory.ClienteDe(a).ReemplazarAsientos(a.Id, "D1", "D2") };
        operaciones.AddRange(Enumerable.Range(0, 10).Select(_ => (Func<Task<HttpResponseMessage>>)(() => _factory.CrearCliente().CrearCarrito(funcionId, "C5"))));

        var respuestas = await EnParalelo(operaciones);

        Assert.Equal(HttpStatusCode.OK, respuestas[0].StatusCode);
        var creados = respuestas.Skip(1).Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.InRange(creados, 0, 1);
        Assert.All(respuestas.Skip(1).Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var carritoA = (await _factory.CarritosGuardados()).Single(c => c.Id == a.Id);
        Assert.Equal([new Asiento('D', 1), new Asiento('D', 2)], carritoA.Asientos);
        await AssertNingunAsientoEnDosCarritos(funcionId);
    }

    [Fact]
    public async Task DosReemplazosParalelosDelMismoCarrito_LaSeleccionFinalEsExactamenteUnaDeLasDos()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var a = await _factory.CrearCarritoOk(funcionId, "A1");

        var respuestas = await EnParalelo(
        [
            () => _factory.ClienteDe(a).ReemplazarAsientos(a.Id, "B1", "B2"),
            () => _factory.ClienteDe(a).ReemplazarAsientos(a.Id, "G1", "G2", "G3")
        ]);

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var final = (await _factory.CarritosGuardados()).Single().Asientos;
        Assert.True(
            final.SequenceEqual([new Asiento('B', 1), new Asiento('B', 2)])
            || final.SequenceEqual([new Asiento('G', 1), new Asiento('G', 2), new Asiento('G', 3)]),
            $"Selección final inesperada: {string.Join(", ", final)}");
        Assert.Equal(["CarritoCreado", "SeleccionReemplazada", "SeleccionReemplazada"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task CreacionesSimultaneasCercaDelTope_NuncaLoSuperan()
    {
        // Sala Sur 1: 40 asientos al 50%, tope 20. Con 15 retenidos quedan 5 lugares.
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaSur1Id, TimeSpan.FromHours(3));
        await _factory.CrearCarritoOk(funcionId, "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "B1", "B2");
        await _factory.CrearCarritoOk(funcionId, "B3", "B4", "B5", "B6", "B7");

        var libres = Enumerable.Range(1, 8).Select(n => $"C{n}")
            .Concat(Enumerable.Range(1, 8).Select(n => $"D{n}"))
            .Concat(Enumerable.Range(1, 4).Select(n => $"E{n}"))
            .ToList();

        var respuestas = await EnParalelo(libres
            .Select(asiento => (Func<Task<HttpResponseMessage>>)(() => _factory.CrearCliente().CrearCarrito(funcionId, asiento))));

        Assert.Equal(5, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var rechazada in respuestas.Where(r => r.StatusCode != HttpStatusCode.Created))
            await rechazada.AssertProblema(409, "funcion-sin-cupo");
        Assert.Equal(20, (await _factory.AsientosRetenidos(funcionId)).Count);
    }
}
