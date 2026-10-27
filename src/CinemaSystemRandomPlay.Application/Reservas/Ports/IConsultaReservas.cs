using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;

namespace CinemaSystemRandomPlay.Application.Reservas.Ports;

/// <summary>
/// Datos guardados de una reserva más el contexto de su función. El estado es el guardado: quien consume
/// el puerto calcula el estado efectivo contra el reloj.
/// </summary>
public record ReservaLecturaDto(
    Guid Id,
    EstadoReserva Estado,
    DateTimeOffset CreadaEn,
    DateTimeOffset VenceEn,
    DateTimeOffset? CanceladaEn,
    IReadOnlyList<Asiento> Asientos,
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid PeliculaId,
    string PeliculaTitulo,
    Guid SucursalId,
    string SucursalNombre,
    Guid SalaId,
    string SalaNombre);

public interface IConsultaReservas
{
    Task<IReadOnlyList<ReservaLecturaDto>> ListarDeCliente(string clienteId, CancellationToken cancellationToken = default);

    Task<ReservaLecturaDto?> ObtenerDeCliente(Guid reservaId, string clienteId, CancellationToken cancellationToken = default);
}
