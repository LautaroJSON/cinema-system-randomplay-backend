using CinemaSystemRandomPlay.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Catalogo;

public class PeliculaEntityConfiguration : IEntityTypeConfiguration<Pelicula>
{
    public void Configure(EntityTypeBuilder<Pelicula> builder)
    {
        builder.ToTable("Peliculas");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Titulo)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.DuracionMinutos)
            .IsRequired();

        builder.Property(p => p.Clasificacion)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.Sinopsis)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.Activa)
            .IsRequired();
    }
}
