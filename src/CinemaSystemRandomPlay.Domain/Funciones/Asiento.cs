using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Funciones;

/// <summary>
/// Posición física dentro de una <see cref="Sala"/>, identificada por fila y número. Dos asientos
/// con la misma fila y número son el mismo asiento (igualdad por valor).
/// </summary>
public sealed record Asiento
{
    public char Fila { get; }
    public int Numero { get; }

    public Asiento(char fila, int numero)
    {
        if (fila is < 'A' or > 'Z')
            throw new DomainException("La fila del asiento debe ser una letra mayúscula entre A y Z.");

        if (numero < 1)
            throw new DomainException("El número de asiento debe ser mayor o igual a 1.");

        Fila = fila;
        Numero = numero;
    }
}
