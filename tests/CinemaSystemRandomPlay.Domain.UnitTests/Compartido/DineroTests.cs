using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Compartido;

public class DineroTests
{
    [Fact]
    public void Constructor_ConMontoValido_CreaElDinero()
    {
        var dinero = Dinero.Ars(7250.50m);

        Assert.Equal(7250.50m, dinero.Monto);
        Assert.Equal(Moneda.ARS, dinero.Moneda);
    }

    [Fact]
    public void Constructor_ConMontoNegativo_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Dinero.Ars(-1m));
    }

    [Fact]
    public void Constructor_ConMasDeDosDecimales_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Dinero.Ars(10.005m));
    }

    [Fact]
    public void Constructor_ConCero_EsValido()
    {
        Assert.Equal(0m, Dinero.Ars(0m).Monto);
    }

    [Fact]
    public void Constructor_NormalizaLaEscalaADosDecimales()
    {
        Assert.Equal("5500.00", Dinero.Ars(5500m).Monto.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("5500.00", Dinero.Ars(5500.000m).Monto.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Igualdad_EsPorValor()
    {
        Assert.Equal(Dinero.Ars(5500m), Dinero.Ars(5500.00m));
        Assert.NotEqual(Dinero.Ars(5500m), Dinero.Ars(5500.01m));
    }

    [Fact]
    public void Multiplicar_EsExacto()
    {
        Assert.Equal(Dinero.Ars(29002.00m), Dinero.Ars(7250.50m).Multiplicar(4));
        Assert.Equal(Dinero.Ars(0m), Dinero.Ars(5500m).Multiplicar(0));
    }

    [Fact]
    public void Multiplicar_ConCantidadNegativa_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Dinero.Ars(5500m).Multiplicar(-1));
    }
}
