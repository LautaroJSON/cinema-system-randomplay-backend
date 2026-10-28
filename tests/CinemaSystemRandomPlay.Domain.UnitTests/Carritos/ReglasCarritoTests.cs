using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Carritos;

public class ReglasCarritoTests
{
    [Fact]
    public void ValoresPorDefecto_SonLosDeLaSpec()
    {
        var reglas = new ReglasCarrito();

        Assert.Equal(5, reglas.MinutosRetencion);
        Assert.Equal(10, reglas.MaximoAsientosPorCarrito);
        Assert.Equal(50, reglas.PorcentajeMaximoRetenidoPorFuncion);
    }

    [Theory]
    [InlineData(0, 10, 50)]
    [InlineData(5, 0, 50)]
    [InlineData(5, 10, 0)]
    [InlineData(5, 10, 101)]
    public void Construir_ConValoresInvalidos_Lanza(int minutos, int maximo, int porcentaje)
    {
        Assert.Throws<DomainException>(() => new ReglasCarrito(minutos, maximo, porcentaje));
    }

    [Theory]
    [InlineData(50, 40, 20)]
    [InlineData(50, 41, 20)]
    [InlineData(50, 1, 1)]
    [InlineData(1, 40, 1)]
    [InlineData(100, 96, 96)]
    public void TopeRetenidoPara_RedondeaHaciaAbajoConMinimoUno(int porcentaje, int totalAsientos, int tope)
    {
        Assert.Equal(tope, new ReglasCarrito(5, 10, porcentaje).TopeRetenidoPara(totalAsientos));
    }
}
