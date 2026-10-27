using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Reservas;

internal static class ReservasTestHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Convierte "C5" en el asiento fila C, número 5.</summary>
    public static object Asiento(string asiento) => new { fila = asiento[..1], numero = int.Parse(asiento[1..]) };

    public static Task<HttpResponseMessage> Reservar(this HttpClient cliente, Guid funcionId, params string[] asientos) =>
        cliente.PostAsJsonAsync($"/api/funciones/{funcionId}/reservas", new { asientos = asientos.Select(Asiento).ToArray() });

    public static async Task<Guid> ReservarOk(this HttpClient cliente, Guid funcionId, params string[] asientos)
    {
        var response = await cliente.Reservar(funcionId, asientos);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return (await response.LeerJson()).GetProperty("id").GetGuid();
    }

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

    public static async Task<List<string>> TiposDeOutbox(this ReservasApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        return await dbContext.OutboxMensajes.OrderBy(m => m.OcurridoEn).Select(m => m.Tipo).ToListAsync();
    }
}
