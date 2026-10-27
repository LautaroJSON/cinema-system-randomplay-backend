using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using static CinemaSystemRandomPlay.IntegrationTests.Reservas.ReservasTestHelpers;

namespace CinemaSystemRandomPlay.IntegrationTests.Reservas;

public class ReservasControllerTests : IClassFixture<ReservasApiFactory>
{
    private static readonly Guid Sala = SalaEntityConfiguration.SalaCentro1Id; // A-H, 12 asientos por fila

    private readonly ReservasApiFactory _factory;

    public ReservasControllerTests(ReservasApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Ana() => _factory.CrearCliente(TokensDePrueba.Customer("ana"));

    private HttpClient Beto() => _factory.CrearCliente(TokensDePrueba.Customer("beto"));

    private async Task<int> ContarReservas()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CinemaDbContext>().Reservas.CountAsync();
    }

    // ---------- POST /api/funciones/{funcionId}/reservas (US1) ----------

    [Fact]
    public async Task Post_AsientosLibres_Devuelve201ConLaReservaPendiente()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await Ana().Reservar(funcionId, "C6", "C5");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reserva = await response.LeerJson();
        var id = reserva.GetProperty("id").GetGuid();
        Assert.Equal($"/api/reservas/{id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("PendienteDePago", reserva.GetProperty("estado").GetString());
        Assert.Equal(["C5", "C6"], reserva.Asientos());
        Assert.Equal(TimeSpan.FromMinutes(10), reserva.GetProperty("venceEn").GetDateTimeOffset() - reserva.GetProperty("creadaEn").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, reserva.GetProperty("canceladaEn").ValueKind);
        Assert.Equal(funcionId, reserva.GetProperty("funcion").GetProperty("funcionId").GetGuid());
        Assert.Equal("Interstellar", reserva.GetProperty("funcion").GetProperty("peliculaTitulo").GetString());
        Assert.Equal("Sala 1", reserva.GetProperty("funcion").GetProperty("salaNombre").GetString());
        Assert.Equal(["ReservaCreada"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Post_FuncionQueEmpiezaEnMenosDeDiezMinutos_VenceAlInicioDeLaFuncion()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromMinutes(4));

        var response = await Ana().Reservar(funcionId, "A1");

        var reserva = await response.LeerJson();
        Assert.Equal(reserva.GetProperty("funcion").GetProperty("horaInicio").GetDateTimeOffset(), reserva.GetProperty("venceEn").GetDateTimeOffset());
    }

    [Fact]
    public async Task Post_AsientoTomadoPorOtroCliente_Devuelve409NoDisponiblesYNoReservaNinguno()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Ana().ReservarOk(funcionId, "C5");

        var response = await Beto().Reservar(funcionId, "C4", "C5", "C6");

        var problema = await response.AssertProblema(409, "asientos-no-disponibles");
        Assert.Equal(["C5"], problema.Asientos());
        Assert.Equal(1, await ContarReservas());
        Assert.Equal(["ReservaCreada"], await _factory.TiposDeOutbox());
        var libre = await Beto().Reservar(funcionId, "C4", "C6");
        Assert.Equal(HttpStatusCode.Created, libre.StatusCode);
    }

    [Fact]
    public async Task Post_AsientoDeReservaVencida_SePuedeReservarDeNuevo()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Ana().ReservarOk(funcionId, "C5");
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        var response = await Beto().Reservar(funcionId, "C5");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_AsientosInexistentes_Devuelve422ConLosInexistentes()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await Ana().Reservar(funcionId, "A1", "Z1", "A13");

        var problema = await response.AssertProblema(422, "asientos-inexistentes");
        Assert.Equal(["A13", "Z1"], problema.Asientos());
        Assert.Equal(0, await ContarReservas());
    }

    [Fact]
    public async Task Post_SuperaElTopeDelCliente_Devuelve409ConLosRestantes()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        await Ana().ReservarOk(funcionId, "A1", "A2");

        var excede = await Ana().Reservar(funcionId, "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "B9");
        var justo = await Ana().Reservar(funcionId, "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8");

        var problema = await excede.AssertProblema(409, "limite-asientos-superado");
        Assert.Equal(8, problema.GetProperty("asientosRestantes").GetInt32());
        Assert.Equal(HttpStatusCode.Created, justo.StatusCode);
    }

    public static TheoryData<string> PedidosInvalidos => new()
    {
        { """{"asientos":[]}""" },
        { """{"asientos":null}""" },
        { """{}""" },
        { """{"asientos":[{"fila":"A","numero":1},{"fila":"A","numero":1}]}""" },
        { """{"asientos":[{"fila":"a","numero":1}]}""" },
        { """{"asientos":[{"fila":"AB","numero":1}]}""" },
        { """{"asientos":[{"fila":"A","numero":0}]}""" },
        { """{"asientos":[{"fila":"A","numero":"x"}]}""" },
        { """{"asientos":[{"fila":"A","numero":1},{"fila":"A","numero":2},{"fila":"A","numero":3},{"fila":"A","numero":4},{"fila":"A","numero":5},{"fila":"A","numero":6},{"fila":"A","numero":7},{"fila":"A","numero":8},{"fila":"A","numero":9},{"fila":"A","numero":10},{"fila":"A","numero":11}]}""" }
    };

    [Theory]
    [MemberData(nameof(PedidosInvalidos))]
    public async Task Post_PedidoInvalido_Devuelve400(string cuerpo)
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await Ana().PostAsync($"/api/funciones/{funcionId}/reservas",
            new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));

        await response.AssertProblema(400, "pedido-invalido");
        Assert.Equal(0, await ContarReservas());
    }

    [Fact]
    public async Task Post_SinCuerpo_Devuelve400()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await Ana().PostAsync($"/api/funciones/{funcionId}/reservas",
            new StringContent("", System.Text.Encoding.UTF8, "application/json"));

        await response.AssertProblema(400, "pedido-invalido");
    }

    [Fact]
    public async Task Post_FuncionInexistente_Devuelve404()
    {
        await _factory.LimpiarDatos();

        var response = await Ana().Reservar(Guid.NewGuid(), "A1");

        await response.AssertProblema(404, "funcion-no-encontrada");
    }

    [Fact]
    public async Task Post_FuncionDePeliculaInactiva_Devuelve404()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3), peliculaActiva: false);

        var response = await Ana().Reservar(funcionId, "A1");

        await response.AssertProblema(404, "funcion-no-encontrada");
    }

    [Fact]
    public async Task Post_FuncionYaComenzada_Devuelve410()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromMinutes(-5));

        var response = await Ana().Reservar(funcionId, "A1");

        await response.AssertProblema(410, "funcion-no-disponible");
    }

    [Fact]
    public async Task Post_IdDeFuncionQueNoEsGuid_Devuelve404()
    {
        var response = await Ana().PostAsJsonAsync("/api/funciones/no-es-guid/reservas", new { asientos = new[] { Asiento("A1") } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ConVariosProblemas_InformaPrimeroElPedidoInvalido()
    {
        await _factory.LimpiarDatos();

        var response = await Ana().PostAsJsonAsync($"/api/funciones/{Guid.NewGuid()}/reservas", new { asientos = Array.Empty<object>() });

        await response.AssertProblema(400, "pedido-invalido");
    }

    [Fact]
    public async Task Post_SinToken_Devuelve401YNoCreaNada()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await _factory.CrearCliente().Reservar(funcionId, "A1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.Equal(0, await ContarReservas());
    }

    public static TheoryData<string> TokensInvalidos => new()
    {
        { "basura" },
        { TokensDePrueba.Crear("ana", ["Customer"], vencido: true) },
        { TokensDePrueba.Crear("ana", ["Customer"], emisor: "https://otro-emisor") },
        { TokensDePrueba.Crear("ana", ["Customer"], clave: new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("otra-clave-de-pruebas-de-al-menos-32-bytes-999"))) }
    };

    [Theory]
    [MemberData(nameof(TokensInvalidos))]
    public async Task Post_ConTokenInvalido_Devuelve401(string token)
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await _factory.CrearCliente(token).Reservar(funcionId, "A1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await ContarReservas());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Admin")]
    public async Task Post_ConTokenSinRolCustomer_Devuelve403(string rol)
    {
        string[] roles = rol.Length == 0 ? [] : [rol];
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await _factory.CrearCliente(TokensDePrueba.Crear("ana", roles)).Reservar(funcionId, "A1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await ContarReservas());
    }

    [Fact]
    public async Task Post_LaIdentidadSaleDelToken_NoDelCuerpo()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var response = await Ana().PostAsJsonAsync($"/api/funciones/{funcionId}/reservas",
            new { clienteId = "beto", asientos = new[] { Asiento("A1") } });
        var reservaId = (await response.LeerJson()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Ana().GetAsync($"/api/reservas/{reservaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Beto().GetAsync($"/api/reservas/{reservaId}")).StatusCode);
    }

    // ---------- GET /api/reservas y GET /api/reservas/{id} (US3) ----------

    [Fact]
    public async Task GetReservas_SinReservas_DevuelveListaVacia()
    {
        await _factory.LimpiarDatos();

        var response = await Ana().GetAsync("/api/reservas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.LeerJson()).GetArrayLength());
    }

    [Fact]
    public async Task GetReservas_DevuelveSoloLasPropiasDeLaMasRecienteALaMasAntigua()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var primera = await Ana().ReservarOk(funcionId, "A1");
        await Beto().ReservarOk(funcionId, "A2");
        var segunda = await Ana().ReservarOk(funcionId, "A3");

        var response = await Ana().GetAsync("/api/reservas");

        var lista = (await response.LeerJson()).EnumerateArray().ToList();
        Assert.Equal([segunda, primera], lista.Select(r => r.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task GetReservas_InformaElEstadoEfectivoDeCadaReserva()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var vencida = await Ana().ReservarOk(funcionId, "A1");
        var cancelada = await Ana().ReservarOk(funcionId, "A2");
        await Ana().PostAsync($"/api/reservas/{cancelada}/cancelacion", content: null);
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        var vigente = await Ana().ReservarOk(funcionId, "A3");

        var lista = (await (await Ana().GetAsync("/api/reservas")).LeerJson()).EnumerateArray()
            .ToDictionary(r => r.GetProperty("id").GetGuid(), r => r.GetProperty("estado").GetString());

        Assert.Equal("PendienteDePago", lista[vigente]);
        Assert.Equal("Expirada", lista[vencida]);
        Assert.Equal("Cancelada", lista[cancelada]);
    }

    [Fact]
    public async Task GetReserva_Propia_DevuelveElDetalle()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var id = await Ana().ReservarOk(funcionId, "B2", "B1");

        var response = await Ana().GetAsync($"/api/reservas/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reserva = await response.LeerJson();
        Assert.Equal(id, reserva.GetProperty("id").GetGuid());
        Assert.Equal(["B1", "B2"], reserva.Asientos());
        Assert.Equal("Sucursal Centro", reserva.GetProperty("funcion").GetProperty("sucursalNombre").GetString());
    }

    [Fact]
    public async Task GetReserva_AjenaOInexistente_Devuelve404IndistinguibleDeLaOtra()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var deAna = await Ana().ReservarOk(funcionId, "B1");

        var ajena = await Beto().GetAsync($"/api/reservas/{deAna}");
        var inexistente = await Beto().GetAsync($"/api/reservas/{Guid.NewGuid()}");

        var cuerpoAjena = await ajena.AssertProblema(404, "reserva-no-encontrada");
        var cuerpoInexistente = await inexistente.AssertProblema(404, "reserva-no-encontrada");
        Assert.Equal(cuerpoInexistente.GetProperty("title").GetString(), cuerpoAjena.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetReservas_SinToken_Devuelve401_YSinRol_Devuelve403()
    {
        var lista = await _factory.CrearCliente().GetAsync("/api/reservas");
        var detalle = await _factory.CrearCliente().GetAsync($"/api/reservas/{Guid.NewGuid()}");
        var sinRol = await _factory.CrearCliente(TokensDePrueba.Crear("ana")).GetAsync("/api/reservas");

        Assert.Equal(HttpStatusCode.Unauthorized, lista.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, detalle.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sinRol.StatusCode);
    }

    // ---------- POST /api/reservas/{id}/cancelacion (US4) ----------

    [Fact]
    public async Task Cancelar_ReservaPropiaVigente_Devuelve200ConLaReservaCancelada()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var id = await Ana().ReservarOk(funcionId, "C5", "C6");

        var response = await Ana().PostAsync($"/api/reservas/{id}/cancelacion", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reserva = await response.LeerJson();
        Assert.Equal("Cancelada", reserva.GetProperty("estado").GetString());
        Assert.NotEqual(JsonValueKind.Null, reserva.GetProperty("canceladaEn").ValueKind);
        Assert.Equal(["ReservaCreada", "ReservaCancelada"], await _factory.TiposDeOutbox());
        Assert.Equal(HttpStatusCode.Created, (await Beto().Reservar(funcionId, "C5", "C6")).StatusCode);
    }

    [Fact]
    public async Task Cancelar_YaCancelada_Devuelve409ConElEstadoActualYSinNuevoOutbox()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var id = await Ana().ReservarOk(funcionId, "C5");
        await Ana().PostAsync($"/api/reservas/{id}/cancelacion", content: null);

        var response = await Ana().PostAsync($"/api/reservas/{id}/cancelacion", content: null);

        var problema = await response.AssertProblema(409, "reserva-no-cancelable");
        Assert.Equal("Cancelada", problema.GetProperty("estadoActual").GetString());
        Assert.Equal(["ReservaCreada", "ReservaCancelada"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Cancelar_Vencida_Devuelve409Expirada()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var id = await Ana().ReservarOk(funcionId, "C5");
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        var response = await Ana().PostAsync($"/api/reservas/{id}/cancelacion", content: null);

        var problema = await response.AssertProblema(409, "reserva-no-cancelable");
        Assert.Equal("Expirada", problema.GetProperty("estadoActual").GetString());
    }

    [Fact]
    public async Task Cancelar_ReservaAjenaOInexistente_Devuelve404YNoLaToca()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var deAna = await Ana().ReservarOk(funcionId, "C5");

        var ajena = await Beto().PostAsync($"/api/reservas/{deAna}/cancelacion", content: null);
        var inexistente = await Beto().PostAsync($"/api/reservas/{Guid.NewGuid()}/cancelacion", content: null);

        await ajena.AssertProblema(404, "reserva-no-encontrada");
        await inexistente.AssertProblema(404, "reserva-no-encontrada");
        var detalle = await (await Ana().GetAsync($"/api/reservas/{deAna}")).LeerJson();
        Assert.Equal("PendienteDePago", detalle.GetProperty("estado").GetString());
        Assert.Equal(["ReservaCreada"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Cancelar_SinToken_Devuelve401_YSinRol_Devuelve403()
    {
        var sinToken = await _factory.CrearCliente().PostAsync($"/api/reservas/{Guid.NewGuid()}/cancelacion", content: null);
        var sinRol = await _factory.CrearCliente(TokensDePrueba.Crear("ana")).PostAsync($"/api/reservas/{Guid.NewGuid()}/cancelacion", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sinRol.StatusCode);
    }

    // ---------- Precios (feature 005) ----------

    private static void AssertMonto(JsonElement reserva, string propiedad, string monto)
    {
        var dinero = reserva.GetProperty(propiedad);
        Assert.Equal(JsonValueKind.String, dinero.GetProperty("monto").ValueKind);
        Assert.Equal(monto, dinero.GetProperty("monto").GetString());
        Assert.Equal("ARS", dinero.GetProperty("moneda").GetString());
    }

    [Fact]
    public async Task Post_InformaElPrecioUnitarioYElTotal()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));

        var reserva = await (await Ana().Reservar(funcionId, "C5", "C6", "C7")).LeerJson();

        AssertMonto(reserva, "precioUnitario", "5500.00");
        AssertMonto(reserva, "total", "16500.00");
    }

    [Fact]
    public async Task Post_ConPrecioConCentavos_SePersisteYSeInformaExacto()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3), precio: 7250.50m);

        var id = await Ana().ReservarOk(funcionId, "A1", "A2", "A3", "A4");
        var reserva = await (await Ana().GetAsync($"/api/reservas/{id}")).LeerJson();

        AssertMonto(reserva, "precioUnitario", "7250.50");
        AssertMonto(reserva, "total", "29002.00");
    }

    [Fact]
    public async Task Reserva_ConservaElPrecioSiCambiaElDeLaFuncion()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var primera = await Ana().ReservarOk(funcionId, "C5", "C6");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "Funciones" SET "PrecioEntrada_Monto" = 6000.00 WHERE "Id" = {funcionId}""");
        }

        var original = await (await Ana().GetAsync($"/api/reservas/{primera}")).LeerJson();
        AssertMonto(original, "precioUnitario", "5500.00");
        AssertMonto(original, "total", "11000.00");

        var nueva = await (await Ana().Reservar(funcionId, "D1", "D2")).LeerJson();
        AssertMonto(nueva, "precioUnitario", "6000.00");
        AssertMonto(nueva, "total", "12000.00");
    }

    [Fact]
    public async Task GetReservas_InformaLosMontosDeCadaReservaTambienLasCanceladas()
    {
        var funcionA = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var funcionB = await _factory.AgregarFuncion(Sala, TimeSpan.FromHours(4), precio: 7250.50m);
        var dosAsientos = await Ana().ReservarOk(funcionA, "A1", "A2");
        await Ana().ReservarOk(funcionB, "A1", "A2", "A3", "A4");
        await Ana().PostAsync($"/api/reservas/{dosAsientos}/cancelacion", content: null);

        var lista = await (await Ana().GetAsync("/api/reservas")).LeerJson();

        var porId = lista.EnumerateArray().ToDictionary(r => r.GetProperty("id").GetGuid());
        Assert.Equal(2, porId.Count);
        var cancelada = porId[dosAsientos];
        Assert.Equal("Cancelada", cancelada.GetProperty("estado").GetString());
        AssertMonto(cancelada, "precioUnitario", "5500.00");
        AssertMonto(cancelada, "total", "11000.00");
        var otra = porId.Values.Single(r => r.GetProperty("id").GetGuid() != dosAsientos);
        AssertMonto(otra, "precioUnitario", "7250.50");
        AssertMonto(otra, "total", "29002.00");
    }

    [Fact]
    public async Task Cancelar_ConservaLosMontos()
    {
        var funcionId = await _factory.SembrarFuncion(Sala, TimeSpan.FromHours(3));
        var id = await Ana().ReservarOk(funcionId, "C5", "C6");

        var reserva = await (await Ana().PostAsync($"/api/reservas/{id}/cancelacion", content: null)).LeerJson();

        AssertMonto(reserva, "precioUnitario", "5500.00");
        AssertMonto(reserva, "total", "11000.00");
    }
}
