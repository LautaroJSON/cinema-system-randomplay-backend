using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Funciones;

public class SucursalTests
{
    [Fact]
    public void Constructor_ConDatosValidos_CreaLaSucursal()
    {
        var id = Guid.NewGuid();
        var sucursal = new Sucursal(id, "Sucursal Centro");

        Assert.Equal(id, sucursal.Id);
        Assert.Equal("Sucursal Centro", sucursal.Nombre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ConNombreVacio_LanzaDomainException(string nombreInvalido)
    {
        Assert.Throws<DomainException>(() => new Sucursal(Guid.NewGuid(), nombreInvalido));
    }
}
