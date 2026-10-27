using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Verificaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Verificaciones;

public class VerificacionEmailEntityConfiguration : IEntityTypeConfiguration<VerificacionEmail>
{
    public void Configure(EntityTypeBuilder<VerificacionEmail> builder)
    {
        builder.ToTable("VerificacionesEmail");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .ValueGeneratedNever();

        builder.Property(v => v.Email)
            .HasConversion(e => e.Valor, v => EmailComprador.Crear(v))
            .IsRequired()
            .HasMaxLength(EmailComprador.LargoMaximo);

        builder.Property(v => v.CodigoHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(v => v.CreadaEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(v => v.VenceEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(v => v.IntentosFallidos)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(v => v.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // A lo sumo una verificación vigente por email: cierra la carrera de dos pedidos simultáneos.
        builder.HasIndex(v => v.Email)
            .IsUnique()
            .HasFilter("\"Estado\" = 'Vigente'")
            .HasDatabaseName("UX_VerificacionesEmail_Email_Vigente");

        builder.HasIndex(v => new { v.Email, v.CreadaEn });
    }
}
