using CinemaSystemRandomPlay.Domain.Funciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

public class SucursalEntityConfiguration : IEntityTypeConfiguration<Sucursal>
{
    public static readonly Guid SucursalCentroId = new("11111111-0000-0000-0000-000000000001");
    public static readonly Guid SucursalNorteId = new("11111111-0000-0000-0000-000000000002");
    public static readonly Guid SucursalSurId = new("11111111-0000-0000-0000-000000000003");

    public void Configure(EntityTypeBuilder<Sucursal> builder)
    {
        builder.ToTable("Sucursales");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Nombre)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasData(
            new { Id = SucursalCentroId, Nombre = "Sucursal Centro" },
            new { Id = SucursalNorteId, Nombre = "Sucursal Norte" },
            new { Id = SucursalSurId, Nombre = "Sucursal Sur" });
    }
}
