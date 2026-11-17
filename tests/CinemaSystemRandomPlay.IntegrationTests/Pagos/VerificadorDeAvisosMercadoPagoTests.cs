using System.Security.Cryptography;
using System.Text;
using CinemaSystemRandomPlay.Infrastructure.Pagos;
using CinemaSystemRandomPlay.IntegrationTests.Compartido;

namespace CinemaSystemRandomPlay.IntegrationTests.Pagos;

/// <summary>Firma de los webhooks de Mercado Pago (research.md, Decisión 7; spec 008, US5).</summary>
public class VerificadorDeAvisosMercadoPagoTests
{
    private const string Clave = TokensDePrueba.ClaveAvisosDePrueba;
    private const string Ts = "1742505638683";
    private const string RequestId = "bb56a2f1-6aae-46ac-982e-9dcd3581d08e";

    private readonly VerificadorDeAvisosMercadoPago _verificador = new(new OpcionesMercadoPago { ClaveSecretaAvisos = Clave });

    [Fact]
    public void FirmaValida_EsValida()
    {
        var firma = FirmaDeAvisos.Firmar("1234567890", RequestId, Ts, Clave);

        Assert.True(_verificador.EsValido(firma, RequestId, "1234567890"));
    }

    [Fact]
    public void ManifestEsElDocumentadoPorMercadoPago()
    {
        Assert.Equal($"id:1234567890;request-id:{RequestId};ts:{Ts};", VerificadorDeAvisosMercadoPago.Manifest("1234567890", RequestId, Ts));
    }

    [Fact]
    public void V1Alterado_EsInvalida()
    {
        var firma = FirmaDeAvisos.Firmar("1234567890", RequestId, Ts, Clave);
        var alterada = firma[..^1] + (firma[^1] == '0' ? '1' : '0');

        Assert.False(_verificador.EsValido(alterada, RequestId, "1234567890"));
    }

    [Fact]
    public void OtraClave_EsInvalida()
    {
        var firma = FirmaDeAvisos.Firmar("1234567890", RequestId, Ts, "otra-clave");

        Assert.False(_verificador.EsValido(firma, RequestId, "1234567890"));
    }

    [Fact]
    public void OtroDataIdOtroRequestId_EsInvalida()
    {
        var firma = FirmaDeAvisos.Firmar("1234567890", RequestId, Ts, Clave);

        Assert.False(_verificador.EsValido(firma, RequestId, "999"));
        Assert.False(_verificador.EsValido(firma, "otro-request", "1234567890"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1=abc")]
    [InlineData("ts=1742505638683")]
    [InlineData("ts=1742505638683,v1=no-es-hex")]
    [InlineData("basura")]
    public void FirmaAusenteOMalFormada_EsInvalidaSinLanzar(string? firma)
    {
        Assert.False(_verificador.EsValido(firma, RequestId, "1234567890"));
    }

    [Fact]
    public void EspaciosAlrededorDeLasPartes_SeToleran()
    {
        var firma = FirmaDeAvisos.Firmar("1234567890", RequestId, Ts, Clave).Replace(",", " , ");

        Assert.True(_verificador.EsValido(" " + firma + " ", RequestId, "1234567890"));
    }

    [Fact]
    public void DataIdAlfanumericoEnMayusculas_SeFirmaEnMinusculas()
    {
        var firma = FirmaDeAvisos.Firmar("ord01abc", RequestId, Ts, Clave);

        Assert.True(_verificador.EsValido(firma, RequestId, "ORD01ABC"));
    }

    [Fact]
    public void SinRequestId_ElManifestOmiteEsaParte()
    {
        var firma = FirmaDeAvisos.Firmar("1234567890", null, Ts, Clave);

        Assert.Equal($"id:1234567890;ts:{Ts};", VerificadorDeAvisosMercadoPago.Manifest("1234567890", null, Ts));
        Assert.True(_verificador.EsValido(firma, null, "1234567890"));
    }

    [Fact]
    public void SinClaveConfigurada_NadaEsValido()
    {
        var sinClave = new VerificadorDeAvisosMercadoPago(new OpcionesMercadoPago { ClaveSecretaAvisos = string.Empty });

        Assert.False(sinClave.EsValido(FirmaDeAvisos.Firmar("1", RequestId, Ts, string.Empty), RequestId, "1"));
    }
}

/// <summary>Firma un aviso como lo haría Mercado Pago, calculada de forma independiente al código de producción.</summary>
public static class FirmaDeAvisos
{
    public static string Firmar(string dataId, string? requestId, string ts, string clave)
    {
        var manifest = $"id:{dataId.ToLowerInvariant()};" + (requestId is null ? "" : $"request-id:{requestId};") + $"ts:{ts};";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(clave), Encoding.UTF8.GetBytes(manifest));
        return $"ts={ts},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
