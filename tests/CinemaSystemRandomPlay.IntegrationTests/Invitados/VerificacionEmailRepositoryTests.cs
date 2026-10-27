using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Verificaciones;
using CinemaSystemRandomPlay.Infrastructure.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Verificaciones;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CinemaSystemRandomPlay.IntegrationTests.Invitados;

/// <summary>Persistencia de las verificaciones contra Postgres real: unicidad de la vigente, ventana y bloqueo por email.</summary>
public class VerificacionEmailRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 27, 18, 0, 0, TimeSpan.FromHours(-3));

    private readonly PostgresDePrueba _postgres = new();

    public async Task InitializeAsync()
    {
        await _postgres.Iniciar();
        await using var dbContext = CrearContexto();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private CinemaDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<CinemaDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    private static EmailComprador Email(string valor) => EmailComprador.Crear(valor);

    private static VerificacionEmail Nueva(EmailComprador email, DateTimeOffset? creadaEn = null) =>
        VerificacionEmail.Crear(email, "hash", creadaEn ?? Ahora, 10);

    [Fact]
    public async Task IndiceUnico_NoPermiteDosVerificacionesVigentesDelMismoEmail()
    {
        var email = Email("ana@example.com");
        await using var dbContext = CrearContexto();
        dbContext.VerificacionesEmail.AddRange(Nueva(email), Nueva(email));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task IndiceUnico_PermiteUnaVigenteJuntoAOtrasUsadasOInvalidadas()
    {
        var email = Email("ana@example.com");
        var usada = Nueva(email);
        usada.Verificar("hash", Ahora, 5);
        var invalidada = Nueva(email);
        invalidada.Invalidar();
        var vigente = Nueva(email);

        await using (var dbContext = CrearContexto())
        {
            dbContext.VerificacionesEmail.AddRange(usada, invalidada, vigente);
            await dbContext.SaveChangesAsync();
        }

        await using var lectura = CrearContexto();
        var obtenida = await new VerificacionEmailRepository(lectura).ObtenerVigente(email);
        Assert.Equal(vigente.Id, obtenida!.Id);
    }

    [Fact]
    public async Task ObtenerVigente_OtroEmailNoVeLaVerificacion()
    {
        await using (var dbContext = CrearContexto())
        {
            dbContext.VerificacionesEmail.Add(Nueva(Email("ana@example.com")));
            await dbContext.SaveChangesAsync();
        }

        await using var lectura = CrearContexto();
        Assert.Null(await new VerificacionEmailRepository(lectura).ObtenerVigente(Email("beto@example.com")));
    }

    [Fact]
    public async Task ContarDesde_CuentaSoloLasDeLaVentanaDelEmail_SeaCualSeaSuEstado()
    {
        var ana = Email("ana@example.com");
        var vieja = Nueva(ana, Ahora.AddMinutes(-90));
        vieja.Invalidar(); // a lo sumo una vigente por email: la más reciente es la que queda vigente
        var usada = Nueva(ana, Ahora.AddMinutes(-30));
        usada.Verificar("hash", Ahora.AddMinutes(-29), 5);
        var reciente = Nueva(ana, Ahora.AddMinutes(-1));

        await using (var dbContext = CrearContexto())
        {
            dbContext.VerificacionesEmail.AddRange(vieja, usada, reciente, Nueva(Email("beto@example.com"), Ahora));
            await dbContext.SaveChangesAsync();
        }

        await using var lectura = CrearContexto();
        var cantidad = await new VerificacionEmailRepository(lectura).ContarDesde(ana, Ahora.AddMinutes(-60));

        Assert.Equal(2, cantidad);
    }

    [Fact]
    public async Task Agregar_ConLaVigenteAnteriorInvalidada_NoViolaElIndiceUnico()
    {
        var email = Email("ana@example.com");
        await using (var dbContext = CrearContexto())
        {
            dbContext.VerificacionesEmail.Add(Nueva(email));
            await dbContext.SaveChangesAsync();
        }

        await using var contexto = CrearContexto();
        var repository = new VerificacionEmailRepository(contexto);
        await new UnidadDeTrabajoEf(contexto).EjecutarEnTransaccion(async ct =>
        {
            var anterior = await repository.ObtenerVigente(email, ct);
            anterior!.Invalidar();
            await repository.Agregar(Nueva(email, Ahora.AddMinutes(1)), ct);
            return 0;
        });

        await using var lectura = CrearContexto();
        Assert.Equal(2, await lectura.VerificacionesEmail.CountAsync());
        Assert.Equal(1, await lectura.VerificacionesEmail.CountAsync(v => v.Estado == EstadoVerificacion.Vigente));
    }

    [Fact]
    public async Task BloquearPorEmail_DosSolicitudesConcurrentesDelMismoEmail_NoSuperanElLimite()
    {
        var email = Email("ana@example.com");
        const int limite = 3;
        const int solicitudes = 10;
        var puerta = new TaskCompletionSource();

        async Task<bool> Solicitar()
        {
            await puerta.Task;
            await using var contexto = CrearContexto();
            var repository = new VerificacionEmailRepository(contexto);
            return await new UnidadDeTrabajoEf(contexto).EjecutarEnTransaccion(async ct =>
            {
                await repository.BloquearPorEmail(email, ct);
                if (await repository.ContarDesde(email, Ahora.AddMinutes(-60), ct) >= limite)
                    return false;

                (await repository.ObtenerVigente(email, ct))?.Invalidar();
                await repository.Agregar(Nueva(email), ct);
                return true;
            });
        }

        var tareas = Enumerable.Range(0, solicitudes).Select(_ => Task.Run(Solicitar)).ToList();
        puerta.SetResult();
        var resultados = await Task.WhenAll(tareas);

        Assert.Equal(limite, resultados.Count(aceptada => aceptada));
        await using var lectura = CrearContexto();
        Assert.Equal(limite, await lectura.VerificacionesEmail.CountAsync());
        Assert.Equal(1, await lectura.VerificacionesEmail.CountAsync(v => v.Estado == EstadoVerificacion.Vigente));
    }
}
