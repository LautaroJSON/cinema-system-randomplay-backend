using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
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
    string SalaNombre,
    Dinero PrecioUnitario);

public interface IConsultaReservas
{
    Task<IReadOnlyList<ReservaLecturaDto>> ListarDeComprador(EmailComprador comprador, CancellationToken cancellationToken = default);

    Task<ReservaLecturaDto?> ObtenerDeComprador(Guid reservaId, EmailComprador comprador, CancellationToken cancellationToken = default);
}
