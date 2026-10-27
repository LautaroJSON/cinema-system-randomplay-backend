using System.Net;
using System.Net.Http.Json;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CinemaSystemRandomPlay.IntegrationTests.Reservas.ReservasTestHelpers;

namespace CinemaSystemRandomPlay.IntegrationTests.Reservas;

/// <summary>User Story 2: reservar exige una credencial de invitado; el dueño y el tope son por email normalizado.</summary>
public class ReservasInvitadoTests : IClassFixture<ReservasApiFactory>
{
    private static readonly Guid Sala = SalaEntityConfiguration.SalaCentro1Id; // A-H, 12 asientos por fila

    private readonly ReservasApiFactory _factory;

    public ReservasInvitadoTests(ReservasApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Cliente(string email) => _factory.CrearCliente(TokensDePrueba.Invitado(email));

    private async Task<List<string>> CompradoresGuardados()
    {
        using var scope = _factory.Services.CreateScope();
        var reservas = await scope.ServiceProvider.GetRequiredService<CinemaDbContext>().Reservas.ToListAsync();
        return reservas.Select(r => r.Comprador.Valor).ToList();
    }

    [Fact]
    public async Task Post_LaReservaQuedaANombreDelEmailNormalizadoDeLaCredencial()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await Cliente("  ANA@Example.com ").Reservar(funcionId, "C5", "C6");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["ana@example.com"], await CompradoresGuardados());
    }

    [Fact]
    public async Task Post_UnEmailDistintoEnElCuerpo_SeIgnora_ElDuenioEsElDeLaCredencial()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await Cliente("ana@example.com").PostAsJsonAsync($"/api/funciones/{funcionId}/reservas",
            new { email = "beto@example.com", comprador = "beto@example.com", asientos = new[] { Asiento("A1") } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["ana@example.com"], await CompradoresGuardados());
        var deBeto = await Cliente("beto@example.com").GetAsync("/api/reservas");
        Assert.Equal(0, (await deBeto.LeerJson()).GetArrayLength());
    }

    [Fact]
    public async Task Post_ElTopeSeCuentaPorEmailNormalizado_AunqueLaCredencialSeEscribaDistinto()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Cliente("ana@example.com").ReservarOk(funcionId, "A1", "A2", "A3", "A4", "A5", "A6", "A7");

        var response = await Cliente("ANA@example.com ").Reservar(funcionId, "B1", "B2", "B3", "B4");

        var problema = await response.AssertProblema(409, "limite-asientos-superado");
        Assert.Equal(3, problema.GetProperty("asientosRestantes").GetInt32());
    }

    [Fact]
    public async Task Post_OtroEmailReservaEnLaMismaFuncionAunqueElPrimeroTengaElCupoLleno()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Cliente("ana@example.com").ReservarOk(funcionId, "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "A10");

        var deBeto = await Cliente("beto@example.com").Reservar(funcionId, "B1", "B2");
        var masDeAna = await Cliente("ana@example.com").Reservar(funcionId, "C1");

        Assert.Equal(HttpStatusCode.Created, deBeto.StatusCode);
        await masDeAna.AssertProblema(409, "limite-asientos-superado");
    }

    [Fact]
    public async Task Post_AliasDeProveedor_SonCompradoresDistintos()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Cliente("ana@example.com").ReservarOk(funcionId, "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "A10");

        var conAlias = await Cliente("ana+1@example.com").Reservar(funcionId, "B1");

        Assert.Equal(HttpStatusCode.Created, conAlias.StatusCode);
    }

    [Fact]
    public async Task Post_ConCredencialVencida_Devuelve401YNoCreaNada()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await _factory.CrearCliente(TokensDePrueba.Invitado("ana@example.com", vencido: true)).Reservar(funcionId, "A1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await CompradoresGuardados());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Admin")]
    [InlineData("CinemaManager")]
    public async Task Post_ConUnTokenDelPersonal_NoSirveComoCredencialDeInvitado(string rol)
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        string[] roles = rol.Length == 0 ? [] : [rol];

        var response = await _factory.CrearCliente(TokensDePrueba.Crear("ana@example.com", roles)).Reservar(funcionId, "A1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await CompradoresGuardados());
    }
}
