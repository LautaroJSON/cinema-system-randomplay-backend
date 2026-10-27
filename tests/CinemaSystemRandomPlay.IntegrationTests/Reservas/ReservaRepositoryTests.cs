using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Reservas;

public class ReservaRepositoryTests : IClassFixture<ReservasApiFactory>
{
    private readonly ReservasApiFactory _factory;

    public ReservaRepositoryTests(ReservasApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Reserva> Guardar(Guid funcionId, string cliente, DateTimeOffset creadaEn, bool cancelar = false, params Asiento[] asientos)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var funcion = await dbContext.Funciones.Include(f => f.Sala).FirstAsync(f => f.Id == funcionId);
        var reserva = new ReservadorDeAsientos().Reservar(funcion, [], cliente, asientos, creadaEn, new ReglasReserva()).Reserva!;
        if (cancelar)
            reserva.Cancelar(creadaEn);
        await new ReservaRepository(dbContext).Agregar(reserva);
        await dbContext.SaveChangesAsync();
        return reserva;
    }

    [Fact]
    public async Task ListarVigentesDeFuncion_FiltraPorEstadoVencimientoYFuncion()
    {
        var ahora = _factory.Reloj.Ahora;
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var otraFuncionId = await _factory.AgregarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(4));
        var vigente = await Guardar(funcionId, "ana", ahora, asientos: new Asiento('A', 1));
        await Guardar(funcionId, "beto", ahora.AddMinutes(-20), asientos: new Asiento('A', 2));
        await Guardar(funcionId, "caro", ahora, cancelar: true, asientos: new Asiento('A', 3));
        await Guardar(otraFuncionId, "ana", ahora, asientos: new Asiento('A', 1));

        using var scope = _factory.Services.CreateScope();
        var repositorio = new ReservaRepository(scope.ServiceProvider.GetRequiredService<CinemaDbContext>());
        var resultado = await repositorio.ListarVigentesDeFuncion(funcionId, ahora);

        var unica = Assert.Single(resultado);
        Assert.Equal(vigente.Id, unica.Id);
        Assert.Equal([new Asiento('A', 1)], unica.Asientos);
    }

    [Fact]
    public async Task ListarVigentesDeFuncion_ReservaQueVenceExactamenteAhora_YaNoEstaVigente()
    {
        // Segundos enteros: Postgres guarda microsegundos y .NET ticks de 100 ns.
        var ahora = _factory.Reloj.Ahora;
        ahora = new DateTimeOffset(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerSecond, ahora.Offset);
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var reserva = await Guardar(funcionId, "ana", ahora, asientos: new Asiento('A', 1));

        using var scope = _factory.Services.CreateScope();
        var repositorio = new ReservaRepository(scope.ServiceProvider.GetRequiredService<CinemaDbContext>());

        Assert.Single(await repositorio.ListarVigentesDeFuncion(funcionId, reserva.VenceEn.AddSeconds(-1)));
        Assert.Empty(await repositorio.ListarVigentesDeFuncion(funcionId, reserva.VenceEn));
    }

    [Fact]
    public async Task ConsultaReservas_AislaPorClienteOrdenaYTraeElContextoDeLaFuncion()
    {
        var ahora = _factory.Reloj.Ahora;
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromHours(3));
        var vieja = await Guardar(funcionId, "ana", ahora.AddMinutes(-30), asientos: [new Asiento('B', 2), new Asiento('B', 1)]);
        var nueva = await Guardar(funcionId, "ana", ahora, asientos: new Asiento('C', 1));
        await Guardar(funcionId, "beto", ahora, asientos: new Asiento('D', 1));

        using var scope = _factory.Services.CreateScope();
        var consulta = new ConsultaReservas(scope.ServiceProvider.GetRequiredService<CinemaDbContext>());

        var deAna = await consulta.ListarDeCliente("ana");
        var propia = await consulta.ObtenerDeCliente(vieja.Id, "ana");
        var ajena = await consulta.ObtenerDeCliente(vieja.Id, "beto");

        Assert.Equal([nueva.Id, vieja.Id], deAna.Select(r => r.Id));
        Assert.Null(ajena);
        Assert.Equal(EstadoReserva.PendienteDePago, propia!.Estado);
        Assert.Equal(vieja.VenceEn, propia.VenceEn, TimeSpan.FromMilliseconds(1));
        Assert.Equal([new Asiento('B', 1), new Asiento('B', 2)], propia.Asientos.OrderBy(a => a.Numero));
        Assert.Equal(funcionId, propia.FuncionId);
        Assert.Equal("Interstellar", propia.PeliculaTitulo);
        Assert.Equal("Sucursal Centro", propia.SucursalNombre);
        Assert.Equal("Sala 1", propia.SalaNombre);
    }
}
