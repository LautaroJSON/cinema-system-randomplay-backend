using CinemaSystemRandomPlay.Domain.Pagos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Pagos;

public class DevolucionEntityConfiguration : IEntityTypeConfiguration<Devolucion>
{
    public void Configure(EntityTypeBuilder<Devolucion> builder)
    {
        builder.ToTable("Devoluciones");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.PagoId).IsRequired().HasMaxLength(50);

        builder.Property(d => d.CarritoId).IsRequired();

        builder.ComplexProperty(d => d.Monto, monto =>
        {
            monto.Property(x => x.Monto).HasColumnName("Monto").HasColumnType("numeric(12,2)").IsRequired();
            monto.Property(x => x.Moneda).HasColumnName("Moneda").HasConversion<string>().HasMaxLength(3).IsRequired();
        });

        builder.Property(d => d.Motivo).IsRequired().HasConversion<string>().HasMaxLength(30);

        builder.Property(d => d.Estado).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(d => d.SolicitadaEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(d => d.ResueltaEn)
            .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTimeOffset?)null, v => v);

        builder.Property(d => d.UltimoError).HasMaxLength(Devolucion.LargoMaximoError);

        // Un pago se devuelve a lo sumo una vez (FR-020).
        builder.HasIndex(d => d.PagoId).IsUnique();

        builder.HasIndex(d => d.CarritoId);
    }
}
