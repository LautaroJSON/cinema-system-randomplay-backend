using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Funciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

public class FuncionEntityConfiguration : IEntityTypeConfiguration<Funcion>
{
    public void Configure(EntityTypeBuilder<Funcion> builder)
    {
        builder.ToTable("Funciones");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.PeliculaId)
            .IsRequired();

        builder.Property(f => f.SalaId)
            .IsRequired();

        builder.Property(f => f.FechaHoraInicio)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.HasOne<Pelicula>()
            .WithMany()
            .HasForeignKey(f => f.PeliculaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Sala)
            .WithMany()
            .HasForeignKey(f => f.SalaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
