using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Reservas;

public class ReservaEntityConfiguration : IEntityTypeConfiguration<Reserva>
{
    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        builder.ToTable("Reservas");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.FuncionId)
            .IsRequired();

        builder.Property(r => r.ClienteId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.CreadaEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(r => r.VenceEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(r => r.CanceladaEn)
            .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTimeOffset?)null, v => v);

        builder.ComplexProperty(r => r.PrecioUnitario, precio =>
        {
            precio.Property(d => d.Monto).HasColumnName("PrecioUnitario_Monto").HasColumnType("numeric(12,2)").IsRequired();
            precio.Property(d => d.Moneda).HasColumnName("PrecioUnitario_Moneda").HasConversion<string>().HasMaxLength(3).IsRequired();
        });

        builder.Ignore(r => r.Total);

        builder.HasOne<Funcion>()
            .WithMany()
            .HasForeignKey(r => r.FuncionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.FuncionId, r.Estado, r.VenceEn });
        builder.HasIndex(r => new { r.ClienteId, r.CreadaEn });

        builder.OwnsMany(r => r.Asientos, asientos =>
        {
            asientos.ToTable("ReservaAsientos");

            asientos.WithOwner().HasForeignKey("ReservaId");

            asientos.HasKey("ReservaId", nameof(Asiento.Fila), nameof(Asiento.Numero));

            asientos.Property(a => a.Fila)
                .IsRequired()
                .HasColumnType("character(1)")
                .ValueGeneratedNever();

            asientos.Property(a => a.Numero)
                .IsRequired()
                .ValueGeneratedNever();
        });

        builder.Navigation(r => r.Asientos)
            .HasField("_asientos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
