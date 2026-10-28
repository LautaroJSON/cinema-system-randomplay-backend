using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Carritos;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>Mapeo de la colección propia de asientos y de los campos del carrito contra Postgres real.</summary>
public class CarritoRepositoryTests : IClassFixture<CarritosApiFactory>
{
    private readonly CarritosApiFactory _factory;

    public CarritoRepositoryTests(CarritosApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> Guardar(Guid funcionId, DateTimeOffset creadoEn, params Asiento[] asientos)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var funcion = await dbContext.Funciones.Include(f => f.Sala).FirstAsync(f => f.Id == funcionId);
        var carrito = new AsignadorDeAsientos().Crear(funcion, [], asientos, creadoEn, new ReglasCarrito()).Carrito!;
        await new CarritoRepository(dbContext).Agregar(carrito);
        await dbContext.SaveChangesAsync();
        return carrito.Id;
    }

    private async Task<Carrito> Recargar(Guid carritoId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        return await dbContext.Carritos.AsNoTracking().SingleAsync(c => c.Id == carritoId);
    }

    [Fact]
    public async Task Reemplazo_ConAsientosComunes_SePersisteSinErroresDeSeguimiento()
    {
        var ahora = _factory.Reloj.Ahora;
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var carritoId = await Guardar(funcionId, ahora, new Asiento('C', 5), new Asiento('C', 6));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var repositorio = new CarritoRepository(dbContext);
            var funcion = await dbContext.Funciones.Include(f => f.Sala).FirstAsync(f => f.Id == funcionId);
            var carrito = (await repositorio.ObtenerPorId(carritoId))!;
            var activos = await repositorio.ListarActivosDeFuncion(funcionId, ahora);

            var resultado = new AsignadorDeAsientos().Reemplazar(funcion, carrito, activos, [new Asiento('C', 6), new Asiento('C', 7)], ahora, new ReglasCarrito());

            Assert.Equal(TipoResultadoSeleccion.Ok, resultado.Tipo);
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal([new Asiento('C', 6), new Asiento('C', 7)], (await Recargar(carritoId)).Asientos);
    }

    [Fact]
    public async Task EmailYAbandono_SePersistenYSeLeenNormalizados()
    {
        var ahora = _factory.Reloj.Ahora;
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var carritoId = await Guardar(funcionId, ahora, new Asiento('A', 1));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var carrito = (await new CarritoRepository(dbContext).ObtenerPorId(carritoId))!;
            carrito.IndicarEmailEntrega(EmailComprador.Crear(" Ana@Example.com "), ahora);
            carrito.Abandonar(ahora);
            await dbContext.SaveChangesAsync();
        }

        var recargado = await Recargar(carritoId);
        Assert.Equal("ana@example.com", recargado.EmailEntrega!.Valor);
        Assert.Equal(EstadoCarrito.Abandonado, recargado.Estado);
        Assert.NotNull(recargado.AbandonadoEn);
    }

    [Fact]
    public async Task ListarActivosDeFuncion_ExcluyeVencidosAbandonadosYDeOtraFuncion()
    {
        var ahora = _factory.Reloj.Ahora;
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var otraFuncionId = await _factory.AgregarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var activo = await Guardar(funcionId, ahora, new Asiento('A', 1));
        await Guardar(funcionId, ahora.AddMinutes(-6), new Asiento('A', 2));
        await Guardar(otraFuncionId, ahora, new Asiento('A', 3));
        var abandonado = await Guardar(funcionId, ahora, new Asiento('A', 4));
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            (await dbContext.Carritos.SingleAsync(c => c.Id == abandonado)).Abandonar(ahora);
            await dbContext.SaveChangesAsync();
        }

        using var lectura = _factory.Services.CreateScope();
        var activos = await new CarritoRepository(lectura.ServiceProvider.GetRequiredService<CinemaDbContext>()).ListarActivosDeFuncion(funcionId, ahora);

        Assert.Equal([activo], activos.Select(c => c.Id));
    }
}
