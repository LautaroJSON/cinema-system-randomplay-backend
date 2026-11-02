using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence;

/// <summary>
/// Sembrado de datos de desarrollo, invocado solo en Development (ver Program.cs). No es una
/// feature ni un endpoint: Sucursal/Sala/Funcion no tienen alta vía API (ver
/// specs/002-horarios-sucursal-hoy), esto es tooling local para no tener que insertar datos a
/// mano en cada arranque. Es idempotente: no duplica las Peliculas demo, y solo agrega Funciones
/// nuevas a una demo si no tiene ninguna disponible hoy (para no quedar "vencido" al día siguiente).
/// Los pósters son URLs de Wikipedia (obtenidas de su API REST pública), solo para mostrar en desarrollo.
/// </summary>
public static class DevDataSeeder
{
    public static readonly Guid PeliculaDemoId = new("88888888-0000-0000-0000-000000000001");
    public static readonly Guid PeliculaDemoDuneId = new("88888888-0000-0000-0000-000000000002");
    public static readonly Guid PeliculaDemoRelatosSalvajesId = new("88888888-0000-0000-0000-000000000003");
    public static readonly Guid PeliculaDemoSecretoDeSusOjosId = new("88888888-0000-0000-0000-000000000004");

    private static Pelicula[] PeliculasDemo() =>
    [
        new(
            PeliculaDemoId,
            "Interstellar (demo)",
            169,
            Clasificacion.ATP,
            "En un futuro en el que la Tierra se vuelve inhabitable, un grupo de astronautas viaja a través de un agujero de gusano en busca de un nuevo hogar para la humanidad.",
            imagenUrl: "https://upload.wikimedia.org/wikipedia/en/b/bc/Interstellar_film_poster.jpg"),
        new(
            PeliculaDemoDuneId,
            "Dune (demo)",
            155,
            Clasificacion.Mas13,
            "Paul Atreides, heredero de una familia noble, llega con los suyos a Arrakis, el planeta desértico del que depende la sustancia más valiosa del universo.",
            imagenUrl: "https://upload.wikimedia.org/wikipedia/en/8/8e/Dune_%282021_film%29.jpg"),
        new(
            PeliculaDemoRelatosSalvajesId,
            "Relatos salvajes (demo)",
            122,
            Clasificacion.Mas16,
            "Seis historias independientes de personas comunes que, empujadas al límite, pierden el control y se dejan llevar por el placer de la venganza.",
            imagenUrl: "https://upload.wikimedia.org/wikipedia/en/5/5e/Relatos_salvajes.jpg"),
        new(
            PeliculaDemoSecretoDeSusOjosId,
            "El secreto de sus ojos (demo)",
            129,
            Clasificacion.Mas16,
            "Un empleado judicial retirado decide escribir una novela sobre un crimen que investigó veinticinco años atrás y que todavía no puede olvidar.",
            imagenUrl: "https://upload.wikimedia.org/wikipedia/en/9/91/Cartel-nuevo-de-el-secreto-de-sus-ojos.jpg"),
    ];

    public static async Task SeedAsync(CinemaDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var peliculasDemo = PeliculasDemo();
        var idsDemo = peliculasDemo.Select(p => p.Id).ToList();
        var idsExistentes = await dbContext.Peliculas
            .Where(p => idsDemo.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        dbContext.Peliculas.AddRange(peliculasDemo.Where(p => !idsExistentes.Contains(p.Id)));
        await dbContext.SaveChangesAsync(cancellationToken);

        var ahora = DateTimeOffset.Now;
        var finDeHoy = new DateTimeOffset(ahora.Date, ahora.Offset).AddDays(1);

        var idsConFuncionesHoy = await dbContext.Funciones
            .Where(f => idsDemo.Contains(f.PeliculaId) && f.FechaHoraInicio >= ahora && f.FechaHoraInicio < finDeHoy)
            .Select(f => f.PeliculaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var (peliculaId, funciones) in FuncionesDeHoy(ahora))
        {
            if (!idsConFuncionesHoy.Contains(peliculaId))
                dbContext.Funciones.AddRange(funciones);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<(Guid PeliculaId, Funcion[] Funciones)> FuncionesDeHoy(DateTimeOffset ahora)
    {
        yield return (PeliculaDemoId,
        [
            new Funcion(Guid.NewGuid(), PeliculaDemoId, SalaEntityConfiguration.SalaCentro1Id, ahora.AddHours(2), Dinero.Ars(5500.00m)),
            new Funcion(Guid.NewGuid(), PeliculaDemoId, SalaEntityConfiguration.SalaCentro2Id, ahora.AddHours(4), Dinero.Ars(7250.50m)),
            new Funcion(Guid.NewGuid(), PeliculaDemoId, SalaEntityConfiguration.SalaNorte1Id, ahora.AddHours(1), Dinero.Ars(6000.00m)),
        ]);
        yield return (PeliculaDemoDuneId,
        [
            new Funcion(Guid.NewGuid(), PeliculaDemoDuneId, SalaEntityConfiguration.SalaCentro1Id, ahora.AddHours(5), Dinero.Ars(6500.00m)),
            new Funcion(Guid.NewGuid(), PeliculaDemoDuneId, SalaEntityConfiguration.SalaNorte1Id, ahora.AddHours(4), Dinero.Ars(6000.00m)),
        ]);
        yield return (PeliculaDemoRelatosSalvajesId,
        [
            new Funcion(Guid.NewGuid(), PeliculaDemoRelatosSalvajesId, SalaEntityConfiguration.SalaCentro2Id, ahora.AddHours(1), Dinero.Ars(5000.00m)),
        ]);
        // El secreto de sus ojos queda sin funciones a propósito: muestra la marca "sin funciones disponibles".
        yield return (PeliculaDemoSecretoDeSusOjosId, []);
    }
}
