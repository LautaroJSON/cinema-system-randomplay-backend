using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Catalogo;

public class PeliculaTests
{
    [Fact]
    public void Constructor_ConDatosValidos_CreaLaPelicula()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.", activa: true);

        Assert.Equal("Matrix", pelicula.Titulo);
        Assert.Equal(136, pelicula.DuracionMinutos);
        Assert.Equal(Clasificacion.Mas13, pelicula.Clasificacion);
        Assert.True(pelicula.Activa);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ConTituloVacio_LanzaDomainException(string tituloInvalido)
    {
        Assert.Throws<DomainException>(() =>
            new Pelicula(Guid.NewGuid(), tituloInvalido, 120, Clasificacion.ATP, "Sinopsis."));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_ConDuracionNoPositiva_LanzaDomainException(int duracionInvalida)
    {
        Assert.Throws<DomainException>(() =>
            new Pelicula(Guid.NewGuid(), "Matrix", duracionInvalida, Clasificacion.ATP, "Sinopsis."));
    }
}
