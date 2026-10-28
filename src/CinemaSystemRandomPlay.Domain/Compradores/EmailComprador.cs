using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Compradores;

/// <summary>
/// Identidad del comprador invitado: un email normalizado (sin espacios en los bordes y en minúsculas).
/// Dos emails que difieren solo en eso son el mismo comprador. No normaliza alias de proveedor
/// (<c>ana+1@…</c>, puntos de Gmail): son emails distintos.
/// </summary>
public sealed record EmailComprador
{
    public const int LargoMaximo = 254;

    public string Valor { get; }

    private EmailComprador(string valor)
    {
        Valor = valor;
    }

    public static EmailComprador Crear(string? valor) =>
        TryCrear(valor, out var email) ? email! : throw new DomainException("El email del comprador es inválido.");

    public static bool TryCrear(string? valor, out EmailComprador? email)
    {
        email = null;

        if (string.IsNullOrWhiteSpace(valor))
            return false;

        var normalizado = valor.Trim().ToLowerInvariant();

        if (!TieneFormatoValido(normalizado))
            return false;

        email = new EmailComprador(normalizado);
        return true;
    }

    public override string ToString() => Valor;

    private static bool TieneFormatoValido(string email)
    {
        if (email.Length > LargoMaximo || email.Any(char.IsWhiteSpace))
            return false;

        var arroba = email.IndexOf('@');
        if (arroba <= 0 || arroba != email.LastIndexOf('@'))
            return false;

        var dominio = email[(arroba + 1)..];

        return dominio.Length > 0
            && dominio.Contains('.')
            && !dominio.StartsWith('.')
            && !dominio.EndsWith('.');
    }
}
