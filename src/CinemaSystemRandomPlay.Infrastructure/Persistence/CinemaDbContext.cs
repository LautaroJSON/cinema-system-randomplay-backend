using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Domain.Verificaciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Outbox;
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

    public DbSet<Reserva> Reservas => Set<Reserva>();

    public DbSet<VerificacionEmail> VerificacionesEmail => Set<VerificacionEmail>();

    public DbSet<OutboxMensaje> OutboxMensajes => Set<OutboxMensaje>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CinemaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
