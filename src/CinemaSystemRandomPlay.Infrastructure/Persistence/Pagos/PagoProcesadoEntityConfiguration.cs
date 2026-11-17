using CinemaSystemRandomPlay.Domain.Pagos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Pagos;

public class PagoProcesadoEntityConfiguration : IEntityTypeConfiguration<PagoProcesado>
{
    public void Configure(EntityTypeBuilder<PagoProcesado> builder)
    {
        builder.ToTable("PagosProcesados");

        // La PK es el id del pago: un segundo procesamiento del mismo pago no puede insertarse (idempotencia, FR-017).
        builder.HasKey(p => p.PagoId);

        builder.Property(p => p.PagoId).HasMaxLength(50).ValueGeneratedNever();

        builder.Property(p => p.CarritoId).IsRequired();

        builder.ComplexProperty(p => p.Monto, monto =>
        {
            monto.Property(d => d.Monto).HasColumnName("Monto").HasColumnType("numeric(12,2)").IsRequired();
            monto.Property(d => d.Moneda).HasColumnName("Moneda").HasConversion<string>().HasMaxLength(3).IsRequired();
        });

        builder.Property(p => p.AprobadoEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(p => p.Resultado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.ProcesadoEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.HasIndex(p => p.CarritoId);
    }
}
