using System.Net;
using System.Net.Http.Json;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using static CinemaSystemRandomPlay.IntegrationTests.Reservas.ReservasTestHelpers;

namespace CinemaSystemRandomPlay.IntegrationTests.Reservas;

/// <summary>User Story 3: cada invitado ve y cancela solo las reservas de su email.</summary>
public class ReservasAislamientoTests : IClassFixture<ReservasApiFactory>
{
    private static readonly Guid Sala = SalaEntityConfiguration.SalaCentro1Id;

    private readonly ReservasApiFactory _factory;

    public ReservasAislamientoTests(ReservasApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Ana() => _factory.CrearCliente(TokensDePrueba.Invitado("ana@example.com"));

    private HttpClient Beto() => _factory.CrearCliente(TokensDePrueba.Invitado("beto@example.com"));

    private static string EmailNuevo() => $"u{Guid.NewGuid():N}@example.com";

    private async Task<string> ObtenerCredencialPorElFlujoReal(string email)
    {
        var cliente = _factory.CrearCliente();
        await cliente.PostAsJsonAsync("/api/verificaciones-email", new { email });
        var response = await cliente.PostAsJsonAsync("/api/verificaciones-email/confirmacion",
            new { email, codigo = _factory.Email.UltimoCodigoDe(email) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.LeerJson()).GetProperty("credencial").GetString()!;
    }

    [Fact]
    public async Task Listar_DevuelveLasTresDeAna_ConElMismoOrdenQueLaFeature004_YNingunaDeBeto()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Ana().ReservarOk(funcionId, "A1");
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(11)); // la primera vence
        await Ana().ReservarOk(funcionId, "A2");
        var aCancelar = await Ana().ReservarOk(funcionId, "A3");
        await Ana().PostAsync($"/api/reservas/{aCancelar}/cancelacion", content: null);
        await Beto().ReservarOk(funcionId, "B1");

        var deAna = await (await Ana().GetAsync("/api/reservas")).LeerJson();
        var deBeto = await (await Beto().GetAsync("/api/reservas")).LeerJson();

        Assert.Equal(["Cancelada", "PendienteDePago", "Expirada"], deAna.EnumerateArray().Select(r => r.GetProperty("estado").GetString()));
        Assert.Equal(1, deBeto.GetArrayLength());
        Assert.Equal(["B1"], deBeto[0].Asientos());
    }

    [Fact]
    public async Task Consultar_ElIdExactoDeUnaReservaAjena_DaLoMismoQueUnIdInexistente()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var deBeto = await Beto().ReservarOk(funcionId, "B1");

        var ajena = await Ana().GetAsync($"/api/reservas/{deBeto}");
        var inexistente = await Ana().GetAsync($"/api/reservas/{Guid.NewGuid()}");

        var cuerpoAjena = await ajena.AssertProblema(404, "reserva-no-encontrada");
        var cuerpoInexistente = await inexistente.AssertProblema(404, "reserva-no-encontrada");
        Assert.Equal(cuerpoInexistente.GetProperty("title").GetString(), cuerpoAjena.GetProperty("title").GetString());
        Assert.Equal(cuerpoInexistente.GetProperty("status").GetInt32(), cuerpoAjena.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Cancelar_ElIdExactoDeUnaReservaAjena_DaLoMismoQueUnIdInexistente_YNoLaToca()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var deBeto = await Beto().ReservarOk(funcionId, "B1");

        var ajena = await Ana().PostAsync($"/api/reservas/{deBeto}/cancelacion", content: null);
        var inexistente = await Ana().PostAsync($"/api/reservas/{Guid.NewGuid()}/cancelacion", content: null);

        await ajena.AssertProblema(404, "reserva-no-encontrada");
        await inexistente.AssertProblema(404, "reserva-no-encontrada");
        var detalle = await (await Beto().GetAsync($"/api/reservas/{deBeto}")).LeerJson();
        Assert.Equal("PendienteDePago", detalle.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Cancelar_UnaPropiaVigente_LiberaLosAsientosDeInmediatoEnElMapaPublico()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var id = await Ana().ReservarOk(funcionId, "C5", "C6");
        var antes = await (await _factory.CrearCliente().GetAsync($"/api/funciones/{funcionId}/asientos")).LeerJson();

        var response = await Ana().PostAsync($"/api/reservas/{id}/cancelacion", content: null);
        var despues = await (await _factory.CrearCliente().GetAsync($"/api/funciones/{funcionId}/asientos")).LeerJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Cancelada", (await response.LeerJson()).GetProperty("estado").GetString());
        Assert.Equal(antes.GetProperty("cantidadDisponibles").GetInt32() + 2, despues.GetProperty("cantidadDisponibles").GetInt32());
    }

    [Fact]
    public async Task ConLaCredencialVencida_ConsultarYCancelarDan401_PeroLasReservasSiguenExistiendo()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var id = await Ana().ReservarOk(funcionId, "A1");
        var vencida = _factory.CrearCliente(TokensDePrueba.Invitado("ana@example.com", vencido: true));

        var lista = await vencida.GetAsync("/api/reservas");
        var detalle = await vencida.GetAsync($"/api/reservas/{id}");
        var cancelar = await vencida.PostAsync($"/api/reservas/{id}/cancelacion", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, lista.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, detalle.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, cancelar.StatusCode);
        var conCredencialNueva = await (await Ana().GetAsync($"/api/reservas/{id}")).LeerJson();
        Assert.Equal("PendienteDePago", conCredencialNueva.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task VariasCredencialesDelMismoEmailObtenidasPorElFlujoReal_VenLasMismasReservas()
    {
        var email = EmailNuevo();
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var delCelular = _factory.CrearCliente(await ObtenerCredencialPorElFlujoReal(email));
        var id = await delCelular.ReservarOk(funcionId, "D1", "D2");

        var delNotebook = _factory.CrearCliente(await ObtenerCredencialPorElFlujoReal(email));
        var desdeLaNueva = await (await delNotebook.GetAsync("/api/reservas")).LeerJson();
        var desdeLaVieja = await (await delCelular.GetAsync("/api/reservas")).LeerJson();

        Assert.Equal(1, desdeLaNueva.GetArrayLength());
        Assert.Equal(id, desdeLaNueva[0].GetProperty("id").GetGuid());
        Assert.Equal(desdeLaNueva.GetRawText(), desdeLaVieja.GetRawText());
    }
}
