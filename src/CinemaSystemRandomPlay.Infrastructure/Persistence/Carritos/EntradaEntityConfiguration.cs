using CinemaSystemRandomPlay.Domain.Carritos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Carritos;

public class EntradaEntityConfiguration : IEntityTypeConfiguration<Entrada>
{
    public const int LargoCodigo = 11;

    public void Configure(EntityTypeBuilder<Entrada> builder)
    {
        builder.ToTable("Entradas");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Codigo)
            .IsRequired()
            .HasMaxLength(LargoCodigo);

        builder.Property(e => e.CarritoId).IsRequired();

        builder.Property(e => e.FuncionId).IsRequired();

        builder.Property(e => e.Fila)
            .IsRequired()
            .HasColumnType("character(1)");

        builder.Property(e => e.Numero).IsRequired();

        builder.Ignore(e => e.Asiento);

        builder.Property(e => e.PeliculaTitulo).IsRequired().HasMaxLength(200);

        builder.Property(e => e.SucursalNombre).IsRequired().HasMaxLength(200);

        builder.Property(e => e.SalaNombre).IsRequired().HasMaxLength(100);

        builder.Property(e => e.HoraInicio)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.Property(e => e.EmitidaEn)
            .IsRequired()
            .HasConversion(v => v.ToUniversalTime(), v => v);

        builder.ComplexProperty(e => e.PrecioUnitario, precio =>
        {
            precio.Property(d => d.Monto).HasColumnName("PrecioUnitario_Monto").HasColumnType("numeric(12,2)").IsRequired();
            precio.Property(d => d.Moneda).HasColumnName("PrecioUnitario_Moneda").HasConversion<string>().HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(e => e.Codigo).IsUnique();

        // Red de seguridad de la base contra una doble venta, además del bloqueo de la función.
        builder.HasIndex(e => new { e.FuncionId, e.Fila, e.Numero }).IsUnique();
    }
}
