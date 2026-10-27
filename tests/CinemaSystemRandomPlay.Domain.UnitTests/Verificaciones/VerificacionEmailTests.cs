using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Verificaciones;
using static CinemaSystemRandomPlay.Domain.UnitTests.CompradoresDePrueba;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Verificaciones;

public class VerificacionEmailTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 27, 18, 0, 0, TimeSpan.FromHours(-3));
    private const string Hash = "hash-correcto";
    private const int Maximo = 5;

    private static VerificacionEmail Crear() => VerificacionEmail.Crear(Ana, Hash, Ahora, 10);

    [Fact]
    public void Crear_DejaLaVerificacionVigenteConSuVencimiento()
    {
        var verificacion = Crear();

        Assert.Equal(Ana, verificacion.Email);
        Assert.Equal(EstadoVerificacion.Vigente, verificacion.EstadoEn(Ahora));
        Assert.Equal(Ahora, verificacion.CreadaEn);
        Assert.Equal(Ahora.AddMinutes(10), verificacion.VenceEn);
        Assert.Equal(0, verificacion.IntentosFallidos);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Crear_SinHash_LanzaDomainException(string hash)
    {
        Assert.Throws<DomainException>(() => VerificacionEmail.Crear(Ana, hash, Ahora, 10));
    }

    [Fact]
    public void Crear_SinEmailOConVigenciaInvalida_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => VerificacionEmail.Crear(null!, Hash, Ahora, 10));
        Assert.Throws<DomainException>(() => VerificacionEmail.Crear(Ana, Hash, Ahora, 0));
    }

    [Fact]
    public void Verificar_HashCorrecto_DevuelveCorrectoYConsumeElCodigo()
    {
        var verificacion = Crear();

        var resultado = verificacion.Verificar(Hash, Ahora.AddMinutes(1), Maximo);

        Assert.Equal(ResultadoVerificacion.Correcto, resultado);
        Assert.Equal(EstadoVerificacion.Usada, verificacion.EstadoEn(Ahora.AddMinutes(1)));
    }

    [Fact]
    public void Verificar_UnCodigoYaUsado_DevuelveYaUsado()
    {
        var verificacion = Crear();
        verificacion.Verificar(Hash, Ahora, Maximo);

        var resultado = verificacion.Verificar(Hash, Ahora, Maximo);

        Assert.Equal(ResultadoVerificacion.YaUsado, resultado);
    }

    [Fact]
    public void Verificar_HashIncorrecto_DevuelveIncorrectoYSumaUnIntento()
    {
        var verificacion = Crear();

        var resultado = verificacion.Verificar("otro-hash", Ahora, Maximo);

        Assert.Equal(ResultadoVerificacion.Incorrecto, resultado);
        Assert.Equal(1, verificacion.IntentosFallidos);
        Assert.Equal(EstadoVerificacion.Vigente, verificacion.EstadoEn(Ahora));
    }

    [Fact]
    public void Verificar_UnIncorrectoNoGastaElCodigo_ElCorrectoTodaviaFunciona()
    {
        var verificacion = Crear();
        verificacion.Verificar("otro-hash", Ahora, Maximo);

        Assert.Equal(ResultadoVerificacion.Correcto, verificacion.Verificar(Hash, Ahora, Maximo));
    }

    [Fact]
    public void Verificar_AlAgotarLosIntentosFallidos_InvalidaElCodigo()
    {
        var verificacion = Crear();

        for (var i = 1; i < Maximo; i++)
            Assert.Equal(ResultadoVerificacion.Incorrecto, verificacion.Verificar("otro-hash", Ahora, Maximo));

        Assert.Equal(ResultadoVerificacion.Invalidado, verificacion.Verificar("otro-hash", Ahora, Maximo));
        Assert.Equal(EstadoVerificacion.Invalidada, verificacion.EstadoEn(Ahora));
    }

    [Fact]
    public void Verificar_UnCodigoInvalidadoPorIntentos_YaNoAceptaNiElCodigoCorrecto()
    {
        var verificacion = Crear();
        for (var i = 0; i < Maximo; i++)
            verificacion.Verificar("otro-hash", Ahora, Maximo);

        var resultado = verificacion.Verificar(Hash, Ahora, Maximo);

        Assert.Equal(ResultadoVerificacion.Invalidado, resultado);
    }

    [Fact]
    public void Verificar_EnElInstanteExactoDelVencimiento_DevuelveVencido()
    {
        var verificacion = Crear();

        Assert.Equal(ResultadoVerificacion.Correcto, Crear().Verificar(Hash, verificacion.VenceEn.AddTicks(-1), Maximo));
        Assert.Equal(ResultadoVerificacion.Vencido, verificacion.Verificar(Hash, verificacion.VenceEn, Maximo));
    }

    [Fact]
    public void EstadoEn_UnaVigentePasadoSuVencimiento_EsVencida()
    {
        var verificacion = Crear();

        Assert.Equal(EstadoVerificacion.Vencida, verificacion.EstadoEn(Ahora.AddMinutes(11)));
        Assert.Equal(EstadoVerificacion.Vigente, verificacion.Estado);
    }

    [Fact]
    public void Invalidar_UnaVigente_PasaAInvalidadaYYaNoAceptaElCodigo()
    {
        var verificacion = Crear();

        verificacion.Invalidar();

        Assert.Equal(EstadoVerificacion.Invalidada, verificacion.EstadoEn(Ahora));
        Assert.Equal(ResultadoVerificacion.Invalidado, verificacion.Verificar(Hash, Ahora, Maximo));
    }

    [Fact]
    public void Invalidar_UnaYaUsada_NoCambiaSuEstado()
    {
        var verificacion = Crear();
        verificacion.Verificar(Hash, Ahora, Maximo);

        verificacion.Invalidar();

        Assert.Equal(EstadoVerificacion.Usada, verificacion.EstadoEn(Ahora));
    }
}
