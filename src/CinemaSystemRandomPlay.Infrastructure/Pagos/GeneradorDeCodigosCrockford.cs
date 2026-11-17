using System.Security.Cryptography;
using CinemaSystemRandomPlay.Domain.Pagos;

namespace CinemaSystemRandomPlay.Infrastructure.Pagos;

/// <summary>
/// Códigos de entrada <c>XXXXX-XXXXX</c> con el alfabeto Crockford Base32 (sin I, L, O ni U, que se confunden al leer):
/// 50 bits de azar criptográfico, no adivinables. La unicidad la garantiza además un índice único en la base.
/// </summary>
public sealed class GeneradorDeCodigosCrockford : IGeneradorDeCodigos
{
    public const string Alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Caracteres = 10;

    public string Generar()
    {
        Span<char> codigo = stackalloc char[Caracteres + 1];
        var posicion = 0;

        for (var i = 0; i < Caracteres; i++)
        {
            if (i == Caracteres / 2)
                codigo[posicion++] = '-';

            codigo[posicion++] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }

        return new string(codigo);
    }
}
