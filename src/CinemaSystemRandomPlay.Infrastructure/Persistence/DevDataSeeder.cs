using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence;

/// <summary>
/// Sembrado de datos de desarrollo, invocado solo en Development (ver Program.cs). No es una
/// feature ni un endpoint: Sucursal/Sala/Funcion no tienen alta vía API (ver
/// specs/002-horarios-sucursal-hoy), esto es tooling local para no tener que insertar datos a
/// mano en cada arranque. Es idempotente: no duplica la Pelicula demo, y solo agrega Funciones
/// nuevas si la demo no tiene ninguna disponible hoy (para no quedar "vencido" al día siguiente).
/// </summary>
public static class DevDataSeeder
{
    public static readonly Guid PeliculaDemoId = new("88888888-0000-0000-0000-000000000001");

    public static async Task SeedAsync(CinemaDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var pelicula = await dbContext.Peliculas.FindAsync([PeliculaDemoId], cancellationToken);
        if (pelicula is null)
        {
            pelicula = new Pelicula(
                PeliculaDemoId,
                "Interstellar (demo)",
                169,
                Clasificacion.ATP,
                "Película de datos de desarrollo, sembrada automáticamente al arrancar en Development.");
            dbContext.Peliculas.Add(pelicula);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var ahora = DateTimeOffset.Now;
        var finDeHoy = new DateTimeOffset(ahora.Date, ahora.Offset).AddDays(1);

        var tieneFuncionesHoy = await dbContext.Funciones
            .AnyAsync(f => f.PeliculaId == pelicula.Id && f.FechaHoraInicio >= ahora && f.FechaHoraInicio < finDeHoy, cancellationToken);

        if (tieneFuncionesHoy)
            return;

        dbContext.Funciones.AddRange(
            new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro1Id, ahora.AddHours(2)),
            new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaCentro2Id, ahora.AddHours(4)),
            new Funcion(Guid.NewGuid(), pelicula.Id, SalaEntityConfiguration.SalaNorte1Id, ahora.AddHours(1)));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
