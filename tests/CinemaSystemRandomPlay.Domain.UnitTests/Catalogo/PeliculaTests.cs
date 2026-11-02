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
        Assert.Null(pelicula.ImagenUrl);
    }

    [Theory]
    [InlineData("https://cdn.ejemplo.com/posters/matrix.jpg")]
    [InlineData("http://cdn.ejemplo.com/posters/matrix.jpg")]
    public void Constructor_ConImagenUrlValida_LaGuarda(string imagenUrl)
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.", imagenUrl: imagenUrl);

        Assert.Equal(imagenUrl, pelicula.ImagenUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ConImagenUrlVacia_LaNormalizaANull(string imagenUrl)
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.", imagenUrl: imagenUrl);

        Assert.Null(pelicula.ImagenUrl);
    }

    [Theory]
    [InlineData("posters/matrix.jpg")]
    [InlineData("ftp://cdn.ejemplo.com/matrix.jpg")]
    [InlineData("no es una url")]
    public void Constructor_ConImagenUrlInvalida_LanzaDomainException(string imagenUrlInvalida)
    {
        Assert.Throws<DomainException>(() =>
            new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.", imagenUrl: imagenUrlInvalida));
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
