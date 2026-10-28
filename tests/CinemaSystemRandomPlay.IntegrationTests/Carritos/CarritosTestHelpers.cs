using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>Carrito recién creado: su id y su token, que solo entrega la respuesta de creación.</summary>
public sealed record CarritoCreado(Guid Id, string Token);

internal static class CarritosTestHelpers
{
    /// <summary>Convierte "C5" en el asiento fila C, número 5.</summary>
    public static object Asiento(string asiento) => new { fila = asiento[..1], numero = int.Parse(asiento[1..]) };

    public static Task<HttpResponseMessage> CrearCarrito(this HttpClient cliente, Guid funcionId, params string[] asientos) =>
        cliente.PostAsJsonAsync($"/api/funciones/{funcionId}/carritos", new { asientos = asientos.Select(Asiento).ToArray() });

    public static async Task<CarritoCreado> CrearCarritoOk(this CarritosApiFactory factory, Guid funcionId, params string[] asientos)
    {
        var response = await factory.CrearCliente().CrearCarrito(funcionId, asientos);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cuerpo = await response.LeerJson();
        return new CarritoCreado(cuerpo.GetProperty("carrito").GetProperty("id").GetGuid(), cuerpo.GetProperty("token").GetString()!);
    }

    public static Task<HttpResponseMessage> ObtenerCarrito(this HttpClient cliente, Guid carritoId) =>
        cliente.GetAsync($"/api/carritos/{carritoId}");

    public static Task<HttpResponseMessage> ReemplazarAsientos(this HttpClient cliente, Guid carritoId, params string[] asientos) =>
        cliente.PutAsJsonAsync($"/api/carritos/{carritoId}/asientos", new { asientos = asientos.Select(Asiento).ToArray() });

    public static Task<HttpResponseMessage> Abandonar(this HttpClient cliente, Guid carritoId) =>
        cliente.PostAsync($"/api/carritos/{carritoId}/abandono", null);

    public static Task<HttpResponseMessage> IndicarEmailEntrega(this HttpClient cliente, Guid carritoId, string? email) =>
        cliente.PutAsJsonAsync($"/api/carritos/{carritoId}/email-entrega", new { email });

    public static HttpClient ClienteDe(this CarritosApiFactory factory, CarritoCreado carrito) => factory.CrearCliente(carrito.Token);

    public static async Task<JsonElement> LeerJson(this HttpResponseMessage response)
    {
        var texto = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    /// <summary>Verifica que la respuesta sea un problem+json con el status y <c>codigo</c> esperados y devuelve su cuerpo.</summary>
    public static async Task<JsonElement> AssertProblema(this HttpResponseMessage response, int status, string codigo)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var cuerpo = await response.LeerJson();
        Assert.Equal(codigo, cuerpo.GetProperty("codigo").GetString());
        return cuerpo;
    }

    public static IEnumerable<string> Asientos(this JsonElement elemento, string propiedad = "asientos") =>
        elemento.GetProperty(propiedad).EnumerateArray().Select(a => $"{a.GetProperty("fila").GetString()}{a.GetProperty("numero").GetInt32()}");

    public static async Task<List<string>> TiposDeOutbox(this CarritosApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        return await dbContext.OutboxMensajes.OrderBy(m => m.OcurridoEn).Select(m => m.Tipo).ToListAsync();
    }

    /// <summary>Carritos guardados, leídos en un contexto nuevo (sin el seguimiento de la API).</summary>
    public static async Task<List<Carrito>> CarritosGuardados(this CarritosApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        return await dbContext.Carritos.AsNoTracking().ToListAsync();
    }

    /// <summary>Asientos retenidos ahora (carritos activos y no vencidos) en la función, según la base.</summary>
    public static async Task<List<Asiento>> AsientosRetenidos(this CarritosApiFactory factory, Guid funcionId)
    {
        var ahora = factory.Reloj.Ahora;
        return (await factory.CarritosGuardados())
            .Where(c => c.FuncionId == funcionId && c.RetieneAsientosEn(ahora))
            .SelectMany(c => c.Asientos)
            .ToList();
    }

    /// <summary>Asientos disponibles del mapa público de la función, como "C5".</summary>
    public static async Task<HashSet<string>> Disponibles(this CarritosApiFactory factory, Guid funcionId)
    {
        var mapa = await (await factory.CrearCliente().GetAsync($"/api/funciones/{funcionId}/asientos")).LeerJson();
        return mapa.GetProperty("filas").EnumerateArray()
            .SelectMany(f => f.GetProperty("asientosDisponibles").EnumerateArray().Select(n => $"{f.GetProperty("fila").GetString()}{n.GetInt32()}"))
            .ToHashSet();
    }
}
