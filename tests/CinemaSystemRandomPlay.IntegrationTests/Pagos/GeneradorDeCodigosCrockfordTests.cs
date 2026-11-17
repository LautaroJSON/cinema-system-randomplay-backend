using System.Text.RegularExpressions;
using CinemaSystemRandomPlay.Infrastructure.Pagos;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

public class GeneradorDeCodigosCrockfordTests
{
    private readonly GeneradorDeCodigosCrockford _generador = new();

    [Fact]
    public void Generar_TieneElFormatoYElAlfabetoCrockford()
    {
        var codigo = _generador.Generar();

        Assert.Matches(new Regex("^[0-9A-HJKMNP-TV-Z]{5}-[0-9A-HJKMNP-TV-Z]{5}$"), codigo);
        Assert.DoesNotContain(codigo, c => c is 'I' or 'L' or 'O' or 'U');
    }

    [Fact]
    public void Generar_MilCodigosSinRepetir()
    {
        var codigos = Enumerable.Range(0, 1000).Select(_ => _generador.Generar()).ToList();

        Assert.Equal(codigos.Count, codigos.Distinct().Count());
    }
}
