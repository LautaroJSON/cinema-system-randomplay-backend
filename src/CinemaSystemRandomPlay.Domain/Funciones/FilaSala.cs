using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Domain.Funciones;

/// <summary>
/// Una fila de la disposición física de una <see cref="Sala"/>. Sus asientos son los números
/// 1..<see cref="CantidadAsientos"/> (ver specs/003-mapa-asientos-funcion/research.md, Decisión 1).
/// </summary>
public sealed record FilaSala
{
    public char Letra { get; }
    public int CantidadAsientos { get; }

    public FilaSala(char letra, int cantidadAsientos)
    {
        if (letra is < 'A' or > 'Z')
            throw new DomainException("La letra de la fila debe ser una letra mayúscula entre A y Z.");

        if (cantidadAsientos < 1)
            throw new DomainException("La fila debe tener al menos un asiento.");

        Letra = letra;
        CantidadAsientos = cantidadAsientos;
    }
}
