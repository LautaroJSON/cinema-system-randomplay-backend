using System.Net;
using System.Net.Http.Json;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>US1: crear un carrito sin credencial y consultarlo con su token (contracts/carritos-api.md).</summary>
public class CarritosControllerTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public CarritosControllerTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Crear_SinCredencial_Devuelve201ConCarritoTokenYLocation()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var antes = _factory.Reloj.Ahora;

        var response = await _factory.CrearCliente().CrearCarrito(funcionId, "C6", "C5");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cuerpo = await response.LeerJson();
        var carrito = cuerpo.GetProperty("carrito");
        var id = carrito.GetProperty("id").GetGuid();
        Assert.Equal($"/api/carritos/{id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Activo", carrito.GetProperty("estado").GetString());
        Assert.Equal(["C5", "C6"], carrito.Asientos());
        Assert.Equal("11000.00", carrito.GetProperty("total").GetProperty("monto").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, carrito.GetProperty("emailEntrega").ValueKind);
        var creadoEn = carrito.GetProperty("creadoEn").GetDateTimeOffset();
        Assert.InRange(creadoEn, antes.AddSeconds(-1), _factory.Reloj.Ahora.AddSeconds(1));
        Assert.Equal(creadoEn.AddMinutes(5), carrito.GetProperty("venceEn").GetDateTimeOffset());
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("token").GetString()));
        Assert.True(cuerpo.GetProperty("tokenVenceEn").GetDateTimeOffset() > carrito.GetProperty("venceEn").GetDateTimeOffset());
        Assert.Equal(["CarritoCreado"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Crear_FuncionQueEmpiezaEnDosMinutos_VenceAlInicio()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromMinutes(2));

        var carrito = (await (await _factory.CrearCliente().CrearCarrito(funcionId, "A1")).LeerJson()).GetProperty("carrito");

        var horaInicio = carrito.GetProperty("funcion").GetProperty("horaInicio").GetDateTimeOffset();
        Assert.Equal(horaInicio, carrito.GetProperty("venceEn").GetDateTimeOffset());
    }

    [Fact]
    public async Task Obtener_ConSuToken_DevuelveElCarrito()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var creado = await _factory.CrearCarritoOk(funcionId, "C5");

        var response = await _factory.ClienteDe(creado).ObtenerCarrito(creado.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var carrito = await response.LeerJson();
        Assert.Equal(creado.Id, carrito.GetProperty("id").GetGuid());
        Assert.Equal(["C5"], carrito.Asientos());
        Assert.Equal("Interstellar", carrito.GetProperty("funcion").GetProperty("peliculaTitulo").GetString());
    }

    [Fact]
    public async Task Crear_PedidoInvalido_Devuelve400SinEscribirNada()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var cliente = _factory.CrearCliente();

        await (await cliente.CrearCarrito(funcionId)).AssertProblema(400, "pedido-invalido");
        await (await cliente.CrearCarrito(funcionId, "A1", "A1")).AssertProblema(400, "pedido-invalido");
        await (await cliente.CrearCarrito(funcionId, Enumerable.Range(1, 11).Select(n => $"A{n}").ToArray())).AssertProblema(400, "pedido-invalido");
        await (await cliente.PostAsJsonAsync($"/api/funciones/{funcionId}/carritos", new { })).AssertProblema(400, "pedido-invalido");

        Assert.Empty(await _factory.CarritosGuardados());
        Assert.Empty(await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Crear_FuncionInexistenteOPeliculaInactiva_Devuelve404()
    {
        var inactiva = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3), peliculaActiva: false);
        var cliente = _factory.CrearCliente();

        await (await cliente.CrearCarrito(Guid.NewGuid(), "A1")).AssertProblema(404, "funcion-no-encontrada");
        await (await cliente.CrearCarrito(inactiva, "A1")).AssertProblema(404, "funcion-no-encontrada");
    }

    [Fact]
    public async Task Crear_FuncionComenzada_Devuelve410()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromMinutes(-1));

        await (await _factory.CrearCliente().CrearCarrito(funcionId, "A1")).AssertProblema(410, "funcion-no-disponible");
    }

    [Fact]
    public async Task Crear_AsientosInexistentes_Devuelve422ConLaLista()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));

        var problema = await (await _factory.CrearCliente().CrearCarrito(funcionId, "A1", "Z1", "A13")).AssertProblema(422, "asientos-inexistentes");

        Assert.Equal(["A13", "Z1"], problema.Asientos());
    }

    [Fact]
    public async Task Crear_AsientosDeOtroCarrito_Devuelve409ConLaListaYUnSoloEvento()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        await _factory.CrearCarritoOk(funcionId, "C5", "C6");

        var problema = await (await _factory.CrearCliente().CrearCarrito(funcionId, "C6", "C7")).AssertProblema(409, "asientos-no-disponibles");

        Assert.Equal(["C6"], problema.Asientos());
        Assert.Single(await _factory.CarritosGuardados());
        Assert.Equal(["CarritoCreado"], await _factory.TiposDeOutbox());
    }

    [Fact]
    public async Task Crear_LosDeUnCarritoVencido_EstanDisponibles()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        await _factory.CrearCarritoOk(funcionId, "C5");
        _factory.Reloj.Avanzar(TimeSpan.FromMinutes(5));

        var response = await _factory.CrearCliente().CrearCarrito(funcionId, "C5");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
