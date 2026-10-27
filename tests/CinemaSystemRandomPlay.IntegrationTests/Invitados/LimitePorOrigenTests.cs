using System.Net;
using System.Net.Http.Json;
using CinemaSystemRandomPlay.IntegrationTests.Reservas;
using static CinemaSystemRandomPlay.IntegrationTests.Reservas.ReservasTestHelpers;

namespace CinemaSystemRandomPlay.IntegrationTests.Invitados;

public class LimitePorOrigenApiFactory : ReservasApiFactory
{
    protected override IReadOnlyDictionary<string, string?> ConfiguracionAdicional => new Dictionary<string, string?>
    {
        ["Invitados:SolicitudesPorOrigen"] = "2"
    };
}

/// <summary>FR-006: además del límite por email, los pedidos de código se limitan por origen.</summary>
public class LimitePorOrigenTests : IClassFixture<LimitePorOrigenApiFactory>
{
    private readonly LimitePorOrigenApiFactory _factory;

    public LimitePorOrigenTests(LimitePorOrigenApiFactory factory)
    {
        _factory = factory;
    }

    private static string EmailNuevo() => $"u{Guid.NewGuid():N}@example.com";

    // Un solo test: el límite es por origen y todos los tests de la clase comparten el mismo origen y la misma ventana.
    [Fact]
    public async Task Pedir_ElTercerPedidoDesdeElMismoOrigenConEmailsDistintos_Devuelve429ConRetryAfterYNoEnviaNada_YConfirmarNoSeVeAfectado()
    {
        var emails = new[] { EmailNuevo(), EmailNuevo(), EmailNuevo() };
        var cliente = _factory.CrearCliente();

        var respuestas = new List<HttpResponseMessage>();
        foreach (var email in emails)
            respuestas.Add(await cliente.PostAsJsonAsync("/api/verificaciones-email", new { email }));

        Assert.Equal(HttpStatusCode.Accepted, respuestas[0].StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, respuestas[1].StatusCode);
        await respuestas[2].AssertProblema(429, "demasiados-pedidos");
        Assert.True(respuestas[2].Headers.Contains("Retry-After"));
        Assert.Equal(0, _factory.Email.CantidadDe(emails[2]));
        Assert.Equal(2, _factory.Email.Mensajes.Count);

        // Confirmar el código no cuenta contra el límite de pedidos.
        var confirmacion = await cliente.PostAsJsonAsync("/api/verificaciones-email/confirmacion",
            new { email = emails[0], codigo = _factory.Email.UltimoCodigoDe(emails[0]) });
        Assert.Equal(HttpStatusCode.OK, confirmacion.StatusCode);
    }
}
