using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence;

public class CinemaDbContext : DbContext
{
    public CinemaDbContext(DbContextOptions<CinemaDbContext> options) : base(options)
    {
    }

    public DbSet<Pelicula> Peliculas => Set<Pelicula>();

    public DbSet<Sucursal> Sucursales => Set<Sucursal>();

    public DbSet<Sala> Salas => Set<Sala>();

    public DbSet<Funcion> Funciones => Set<Funcion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CinemaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
