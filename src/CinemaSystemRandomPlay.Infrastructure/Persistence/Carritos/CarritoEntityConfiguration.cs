using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Carritos;

public class CarritoEntityConfiguration : IEntityTypeConfiguration<Carrito>
{
    public void Configure(EntityTypeBuilder<Carrito> builder)
    {
        builder.ToTable("Carritos");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.FuncionId)
            .IsRequired();

        builder.Property(c => c.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.CreadoEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(c => c.VenceEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(c => c.AbandonadoEn)
            .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTimeOffset?)null, v => v);

        builder.Property(c => c.EmailEntrega)
            .HasConversion(e => e!.Valor, v => EmailComprador.Crear(v))
            .HasMaxLength(EmailComprador.LargoMaximo);

        builder.ComplexProperty(c => c.PrecioUnitario, precio =>
        {
            precio.Property(d => d.Monto).HasColumnName("PrecioUnitario_Monto").HasColumnType("numeric(12,2)").IsRequired();
            precio.Property(d => d.Moneda).HasColumnName("PrecioUnitario_Moneda").HasConversion<string>().HasMaxLength(3).IsRequired();
        });

        builder.Ignore(c => c.Total);

        builder.HasOne<Funcion>()
            .WithMany()
            .HasForeignKey(c => c.FuncionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.FuncionId, c.Estado, c.VenceEn });

        builder.OwnsMany(c => c.Asientos, asientos =>
        {
            asientos.ToTable("CarritoAsientos");

            asientos.WithOwner().HasForeignKey("CarritoId");

            asientos.HasKey("CarritoId", nameof(Asiento.Fila), nameof(Asiento.Numero));

            asientos.Property(a => a.Fila)
                .IsRequired()
                .HasColumnType("character(1)")
                .ValueGeneratedNever();

            asientos.Property(a => a.Numero)
                .IsRequired()
                .ValueGeneratedNever();
        });

        builder.Navigation(c => c.Asientos)
            .HasField("_asientos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
