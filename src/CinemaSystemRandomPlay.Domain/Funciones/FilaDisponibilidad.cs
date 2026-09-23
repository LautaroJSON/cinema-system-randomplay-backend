namespace CinemaSystemRandomPlay.Domain.Funciones;

/// <summary>
/// Una fila de una <see cref="Sala"/> con los números de asiento disponibles para una función.
/// Los números de 1..<see cref="CantidadAsientos"/> que no figuran en <see cref="NumerosDisponibles"/>
/// están ocupados.
/// </summary>
public sealed record FilaDisponibilidad(char Letra, int CantidadAsientos, IReadOnlyList<int> NumerosDisponibles);
