using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Compradores;

public class EmailCompradorTests
{
    [Theory]
    [InlineData("ana@example.com", "ana@example.com")]
    [InlineData("Ana@Example.com", "ana@example.com")]
    [InlineData("  ANA@example.COM ", "ana@example.com")]
    [InlineData("ana+1@example.com", "ana+1@example.com")]
    public void TryCrear_EmailValido_NormalizaRecortandoYPasandoAMinusculas(string entrada, string esperado)
    {
        var ok = EmailComprador.TryCrear(entrada, out var email);

        Assert.True(ok);
        Assert.Equal(esperado, email!.Valor);
        Assert.Equal(esperado, email.ToString());
    }

    [Fact]
    public void Igualdad_EmailsQueDifierenSoloEnMayusculasYEspacios_SonElMismoComprador()
    {
        var a = EmailComprador.Crear("Ana@Example.com");
        var b = EmailComprador.Crear(" ana@example.com ");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Igualdad_AliasDeProveedor_NoSeNormalizan()
    {
        Assert.NotEqual(EmailComprador.Crear("ana@example.com"), EmailComprador.Crear("ana+1@example.com"));
        Assert.NotEqual(EmailComprador.Crear("a.na@gmail.com"), EmailComprador.Crear("ana@gmail.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("ana.example.com")]
    [InlineData("ana@@example.com")]
    [InlineData("ana@exa@mple.com")]
    [InlineData("@example.com")]
    [InlineData("ana@")]
    [InlineData("ana@example")]
    [InlineData("ana@.example.com")]
    [InlineData("ana@example.com.")]
    [InlineData("ana@exam ple.com")]
    [InlineData("a na@example.com")]
    public void TryCrear_EmailInvalido_DevuelveFalse(string? entrada)
    {
        var ok = EmailComprador.TryCrear(entrada, out var email);

        Assert.False(ok);
        Assert.Null(email);
    }

    [Fact]
    public void TryCrear_MasDe254Caracteres_DevuelveFalse()
    {
        var largo = new string('a', 250) + "@example.com";

        Assert.False(EmailComprador.TryCrear(largo, out _));
    }

    [Fact]
    public void TryCrear_Exactamente254Caracteres_EsValido()
    {
        var dominio = "@example.com";
        var email = new string('a', 254 - dominio.Length) + dominio;

        Assert.True(EmailComprador.TryCrear(email, out var creado));
        Assert.Equal(254, creado!.Valor.Length);
    }

    [Fact]
    public void Crear_EmailInvalido_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => EmailComprador.Crear("no-es-un-email"));
    }
}
