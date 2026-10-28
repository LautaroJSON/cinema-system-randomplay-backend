using System.Net.Http.Headers;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;
using CinemaSystemRandomPlay.IntegrationTests.Invitados;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CinemaSystemRandomPlay.IntegrationTests.Carritos;

/// <summary>Reloj real con un desplazamiento que los tests pueden avanzar para vencer carritos.</summary>
public class RelojDePrueba : IReloj
{
    private long _desplazamientoTicks;

    public DateTimeOffset Ahora => DateTimeOffset.Now.AddTicks(Interlocked.Read(ref _desplazamientoTicks));

    public void Avanzar(TimeSpan tiempo) => Interlocked.Add(ref _desplazamientoTicks, tiempo.Ticks);
}

public class CarritosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresDePrueba _postgres = new();

    public RelojDePrueba Reloj { get; } = new();

    public EnviadorDeEmailEnMemoria Email { get; } = new();

    /// <summary>Configuración adicional de una factory derivada (por ejemplo, un límite por origen más bajo).</summary>
    protected virtual IReadOnlyDictionary<string, string?> ConfiguracionAdicional => new Dictionary<string, string?>();

    /// <summary>Reemplazos de servicios de una factory derivada (por ejemplo, un desafío anti-bots que rechaza).</summary>
    protected virtual void ConfigurarServiciosAdicionales(IServiceCollection services)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Todos los pedidos de TestServer comparten origen: alto por defecto para que los tests no se interfieran; el test
                // del límite por origen lo baja. El tope global por función queda en su valor real.
                ["Carritos:CreacionesPorOrigen"] = "1000",
                ["ConnectionStrings:CinemaDb"] = _postgres.ConnectionString,
                ["Invitados:ClaveFirma"] = TokensDePrueba.ClaveFirmaDePrueba
            });
            configBuilder.AddInMemoryCollection(ConfiguracionAdicional);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReloj>();
            services.AddSingleton<IReloj>(Reloj);

            services.RemoveAll<IEnviadorDeEmail>();
            services.AddSingleton<IEnviadorDeEmail>(Email);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = TokensDePrueba.Emisor,
                    ValidAudience = TokensDePrueba.Audiencia,
                    IssuerSigningKey = TokensDePrueba.Clave,
                    NameClaimType = "sub",
                    RoleClaimType = TokensDePrueba.ClaimRoles
                };
            });

            ConfigurarServiciosAdicionales(services);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.Iniciar();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CrearCliente(string? token = null)
    {
        var cliente = CreateClient();
        if (token is not null)
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    public async Task LimpiarDatos()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        dbContext.OutboxMensajes.RemoveRange(dbContext.OutboxMensajes);
        dbContext.Carritos.RemoveRange(dbContext.Carritos);
        dbContext.Funciones.RemoveRange(dbContext.Funciones);
        dbContext.Peliculas.RemoveRange(dbContext.Peliculas);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Limpia los datos y siembra una película con una función que empieza a <paramref name="desdeAhora"/> del reloj de test.</summary>
    public async Task<Guid> SembrarFuncion(Guid salaId, TimeSpan desdeAhora, bool peliculaActiva = true, decimal precio = 5500m)
    {
        await LimpiarDatos();
        return await AgregarFuncion(salaId, desdeAhora, peliculaActiva, precio);
    }

    /// <summary>Agrega otra función (con su propia película) sin limpiar lo existente.</summary>
    public async Task<Guid> AgregarFuncion(Guid salaId, TimeSpan desdeAhora, bool peliculaActiva = true, decimal precio = 5500m)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis", peliculaActiva);
        var funcion = new Funcion(Guid.NewGuid(), pelicula.Id, salaId, Reloj.Ahora.Add(desdeAhora), Dinero.Ars(precio));
        dbContext.Peliculas.Add(pelicula);
        dbContext.Funciones.Add(funcion);
        await dbContext.SaveChangesAsync();
        return funcion.Id;
    }
}
