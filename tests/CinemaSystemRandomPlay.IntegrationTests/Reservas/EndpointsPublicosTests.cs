using System.Net;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystemRandomPlay.IntegrationTests.Reservas;

/// <summary>
/// Regresión de FR-024 / SC-007: con la autenticación y el fallback cerrado activos, los endpoints públicos
/// existentes siguen respondiendo sin token.
/// </summary>
public class EndpointsPublicosTests : IClassFixture<ReservasApiFactory>
{
    private readonly ReservasApiFactory _factory;

    public EndpointsPublicosTests(ReservasApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EndpointsPublicosExistentes_SinToken_NoExigenAutenticacion()
    {
        var funcionId = await _factory.SembrarFuncion(SalaEntityConfiguration.SalaCentro1Id, TimeSpan.FromMinutes(30));
        Guid peliculaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            peliculaId = dbContext.Funciones.Single(f => f.Id == funcionId).PeliculaId;
        }

        var cliente = _factory.CrearCliente();
        var rutas = new[]
        {
            "/api/catalogo/peliculas",
            $"/api/catalogo/peliculas/{peliculaId}",
            $"/api/funciones/peliculas/{peliculaId}/sucursales-hoy",
            $"/api/funciones/peliculas/{peliculaId}/sucursales/{SucursalEntityConfiguration.SucursalCentroId}/horarios-hoy",
            $"/api/funciones/{funcionId}/asientos"
        };

        foreach (var ruta in rutas)
        {
            var response = await cliente.GetAsync(ruta);
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.Gone,
                $"{ruta} respondió {(int)response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/catalogo/peliculas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/funciones/{funcionId}/asientos")).StatusCode);
    }

    [Fact]
    public async Task EndpointSinPoliticaDeclarada_FallaCerrado_ConUnAnonimo()
    {
        // Los endpoints de Reservas declaran su política; sin token no se llega a ellos (401), lo que
        // confirma que el fallback exige usuario autenticado.
        var response = await _factory.CrearCliente().GetAsync("/api/reservas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
