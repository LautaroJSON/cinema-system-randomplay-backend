using CinemaSystemRandomPlay.Application.Compartido.Dtos;
using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Domain.Reservas;

namespace CinemaSystemRandomPlay.Application.Reservas.Dtos;

public record FuncionReservaDto(
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid PeliculaId,
    string PeliculaTitulo,
    Guid SucursalId,
    string SucursalNombre,
    Guid SalaId,
    string SalaNombre);

public record ReservaDto(
    Guid Id,
    EstadoReserva Estado,
    DateTimeOffset CreadaEn,
    DateTimeOffset VenceEn,
    DateTimeOffset? CanceladaEn,
    IReadOnlyList<AsientoPedidoDto> Asientos,
    FuncionReservaDto Funcion,
    DineroDto PrecioUnitario,
    DineroDto Total)
{
    public static ReservaDto DesdeLectura(ReservaLecturaDto lectura, DateTimeOffset ahora) =>
        new(lectura.Id,
            Reserva.EstadoEn(lectura.Estado, lectura.VenceEn, ahora),
            lectura.CreadaEn,
            lectura.VenceEn,
            lectura.CanceladaEn,
            lectura.Asientos.OrderBy(a => a.Fila).ThenBy(a => a.Numero).Select(a => new AsientoPedidoDto(a.Fila.ToString(), a.Numero)).ToList(),
            new FuncionReservaDto(lectura.FuncionId, lectura.HoraInicio, lectura.PeliculaId, lectura.PeliculaTitulo,
                lectura.SucursalId, lectura.SucursalNombre, lectura.SalaId, lectura.SalaNombre),
            DineroDto.DesdeDominio(lectura.PrecioUnitario),
            DineroDto.DesdeDominio(lectura.PrecioUnitario.Multiplicar(lectura.Asientos.Count)));
}
