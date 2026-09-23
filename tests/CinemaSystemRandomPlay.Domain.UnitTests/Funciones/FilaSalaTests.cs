using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Funciones;

public class FilaSalaTests
{
    [Fact]
    public void Constructor_ConDatosValidos_CreaLaFila()
    {
        var fila = new FilaSala('C', 12);

        Assert.Equal('C', fila.Letra);
        Assert.Equal(12, fila.CantidadAsientos);
    }

    [Theory]
    [InlineData('a')]
    [InlineData('1')]
    [InlineData('Ñ')]
    [InlineData(' ')]
    public void Constructor_ConLetraInvalida_LanzaDomainException(char letraInvalida)
    {
        Assert.Throws<DomainException>(() => new FilaSala(letraInvalida, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_ConCantidadDeAsientosMenorAUno_LanzaDomainException(int cantidadInvalida)
    {
        Assert.Throws<DomainException>(() => new FilaSala('A', cantidadInvalida));
    }
}
