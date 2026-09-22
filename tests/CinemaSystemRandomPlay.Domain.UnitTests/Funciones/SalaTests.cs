using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Funciones;

public class SalaTests
{
    [Fact]
    public void Constructor_ConDatosValidos_CreaLaSala()
    {
        var id = Guid.NewGuid();
        var sucursalId = Guid.NewGuid();
        var sala = new Sala(id, sucursalId, "Sala 1");

        Assert.Equal(id, sala.Id);
        Assert.Equal(sucursalId, sala.SucursalId);
        Assert.Equal("Sala 1", sala.Nombre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ConNombreVacio_LanzaDomainException(string nombreInvalido)
    {
        Assert.Throws<DomainException>(() => new Sala(Guid.NewGuid(), Guid.NewGuid(), nombreInvalido));
    }

    [Fact]
    public void Constructor_ConSucursalIdVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => new Sala(Guid.NewGuid(), Guid.Empty, "Sala 1"));
    }
}
