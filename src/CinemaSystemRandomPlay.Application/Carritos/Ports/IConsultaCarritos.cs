using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Carritos.Ports;

/// <summary>
/// Datos guardados de un carrito más el contexto de su función. El estado es el guardado: quien consume el puerto calcula
/// el estado efectivo contra el reloj.
/// </summary>
public record CarritoLecturaDto(
    Guid Id,
    EstadoCarrito Estado,
    DateTimeOffset CreadoEn,
    DateTimeOffset VenceEn,
    DateTimeOffset? AbandonadoEn,
    string? EmailEntrega,
    IReadOnlyList<Asiento> Asientos,
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid PeliculaId,
    string PeliculaTitulo,
    Guid SucursalId,
    string SucursalNombre,
    Guid SalaId,
    string SalaNombre,
    Dinero PrecioUnitario);

public interface IConsultaCarritos
{
    Task<CarritoLecturaDto?> ObtenerPorId(Guid carritoId, CancellationToken cancellationToken = default);
}
