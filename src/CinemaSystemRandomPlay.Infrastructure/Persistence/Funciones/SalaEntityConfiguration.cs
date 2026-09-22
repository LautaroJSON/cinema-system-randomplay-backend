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

        builder.HasOne<Sucursal>()
            .WithMany()
            .HasForeignKey(s => s.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { Id = SalaCentro1Id, SucursalId = SucursalEntityConfiguration.SucursalCentroId, Nombre = "Sala 1" },
            new { Id = SalaCentro2Id, SucursalId = SucursalEntityConfiguration.SucursalCentroId, Nombre = "Sala 2" },
            new { Id = SalaNorte1Id, SucursalId = SucursalEntityConfiguration.SucursalNorteId, Nombre = "Sala 1" },
            new { Id = SalaSur1Id, SucursalId = SucursalEntityConfiguration.SucursalSurId, Nombre = "Sala 1" });
    }
}
