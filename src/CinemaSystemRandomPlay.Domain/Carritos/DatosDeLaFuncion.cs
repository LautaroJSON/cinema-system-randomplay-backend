namespace CinemaSystemRandomPlay.Domain.Carritos;

/// <summary>Datos de la función que se copian en cada entrada al pagar, para que no cambien si después se edita el catálogo.</summary>
public sealed record DatosDeLaFuncion(
    Guid FuncionId,
    string PeliculaTitulo,
    string SucursalNombre,
    string SalaNombre,
    DateTimeOffset HoraInicio);
