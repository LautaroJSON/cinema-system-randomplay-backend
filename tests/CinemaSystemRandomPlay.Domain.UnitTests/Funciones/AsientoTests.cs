using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Funciones;

public class AsientoTests
{
    [Fact]
    public void Constructor_ConDatosValidos_CreaElAsiento()
    {
        var asiento = new Asiento('F', 7);

        Assert.Equal('F', asiento.Fila);
        Assert.Equal(7, asiento.Numero);
    }

    [Theory]
    [InlineData('a')]
    [InlineData('1')]
    [InlineData(' ')]
    public void Constructor_ConFilaInvalida_LanzaDomainException(char filaInvalida)
    {
        Assert.Throws<DomainException>(() => new Asiento(filaInvalida, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ConNumeroMenorAUno_LanzaDomainException(int numeroInvalido)
    {
        Assert.Throws<DomainException>(() => new Asiento('A', numeroInvalido));
    }

    [Fact]
    public void Igualdad_MismaFilaYNumero_SonIguales()
    {
        Assert.Equal(new Asiento('C', 5), new Asiento('C', 5));
        Assert.NotEqual(new Asiento('C', 5), new Asiento('C', 6));
    }

    [Fact]
    public void HashSet_NoAdmiteAsientosRepetidos()
    {
        var asientos = new HashSet<Asiento> { new('C', 5), new('C', 5), new('D', 5) };

        Assert.Equal(2, asientos.Count);
        Assert.Contains(new Asiento('C', 5), asientos);
    }
}
