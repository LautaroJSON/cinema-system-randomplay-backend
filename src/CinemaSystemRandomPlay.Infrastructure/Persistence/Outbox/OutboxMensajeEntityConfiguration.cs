using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Outbox;

public class OutboxMensajeEntityConfiguration : IEntityTypeConfiguration<OutboxMensaje>
{
    public const int LargoMaximoError = 1000;

    public void Configure(EntityTypeBuilder<OutboxMensaje> builder)
    {
        builder.ToTable("OutboxMensajes");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.Tipo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(m => m.OcurridoEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(m => m.ProcesadoEn)
            .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTimeOffset?)null, v => v);

        builder.Property(m => m.Intentos)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.ProximoIntentoEn)
            .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTimeOffset?)null, v => v);

        builder.Property(m => m.UltimoError)
            .HasMaxLength(LargoMaximoError);

        builder.Property(m => m.FallidoEn)
            .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTimeOffset?)null, v => v);

        builder.HasIndex(m => new { m.ProcesadoEn, m.OcurridoEn });

        builder.HasIndex(m => new { m.ProcesadoEn, m.FallidoEn, m.ProximoIntentoEn });
    }
}
