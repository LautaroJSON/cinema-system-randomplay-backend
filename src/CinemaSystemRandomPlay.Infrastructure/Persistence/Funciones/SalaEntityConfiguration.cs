using CinemaSystemRandomPlay.Domain.Funciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

public class SalaEntityConfiguration : IEntityTypeConfiguration<Sala>
{
    public static readonly Guid SalaCentro1Id = new("22222222-0000-0000-0000-000000000001");
    public static readonly Guid SalaCentro2Id = new("22222222-0000-0000-0000-000000000002");
    public static readonly Guid SalaNorte1Id = new("22222222-0000-0000-0000-000000000003");
    public static readonly Guid SalaSur1Id = new("22222222-0000-0000-0000-000000000004");

    public void Configure(EntityTypeBuilder<Sala> builder)
    {
        builder.ToTable("Salas");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SucursalId)
            .IsRequired();

        builder.Property(s => s.Nombre)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(s => s.Sucursal)
            .WithMany()
            .HasForeignKey(s => s.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { Id = SalaCentro1Id, SucursalId = SucursalEntityConfiguration.SucursalCentroId, Nombre = "Sala 1" },
            new { Id = SalaCentro2Id, SucursalId = SucursalEntityConfiguration.SucursalCentroId, Nombre = "Sala 2" },
            new { Id = SalaNorte1Id, SucursalId = SucursalEntityConfiguration.SucursalNorteId, Nombre = "Sala 1" },
            new { Id = SalaSur1Id, SucursalId = SucursalEntityConfiguration.SucursalSurId, Nombre = "Sala 1" });

        builder.OwnsMany(s => s.Filas, filas =>
        {
            filas.ToTable("SalaFilas");

            filas.WithOwner().HasForeignKey("SalaId");

            filas.HasKey("SalaId", nameof(FilaSala.Letra));

            filas.Property(f => f.Letra)
                .IsRequired()
                .HasColumnType("character(1)")
                .ValueGeneratedNever();

            filas.Property(f => f.CantidadAsientos)
                .IsRequired();

            filas.HasData(
                Filas(SalaCentro1Id, ('A', 12), ('B', 12), ('C', 12), ('D', 12), ('E', 12), ('F', 12), ('G', 12), ('H', 12))
                    .Concat(Filas(SalaCentro2Id, ('A', 8), ('B', 10), ('C', 12), ('D', 12), ('E', 14), ('F', 14)))
                    .Concat(Filas(SalaNorte1Id, ('A', 10), ('B', 10), ('C', 10), ('D', 10), ('E', 10), ('F', 10), ('G', 10), ('H', 10), ('I', 10), ('J', 10)))
                    .Concat(Filas(SalaSur1Id, ('A', 8), ('B', 8), ('C', 8), ('D', 8), ('E', 8)))
                    .ToArray());
        });

        builder.Navigation(s => s.Filas)
            .HasField("_filas")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static IEnumerable<object> Filas(Guid salaId, params (char Letra, int CantidadAsientos)[] filas)
    {
        return filas.Select(f => new { SalaId = salaId, f.Letra, f.CantidadAsientos });
    }
}
