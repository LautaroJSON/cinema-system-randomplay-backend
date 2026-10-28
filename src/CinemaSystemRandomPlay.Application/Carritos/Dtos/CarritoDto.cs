using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Dtos;
using CinemaSystemRandomPlay.Domain.Carritos;

namespace CinemaSystemRandomPlay.Application.Carritos.Dtos;

public record FuncionCarritoDto(
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid PeliculaId,
    string PeliculaTitulo,
    Guid SucursalId,
    string SucursalNombre,
    Guid SalaId,
    string SalaNombre);

public record CarritoDto(
    Guid Id,
    EstadoCarrito Estado,
    DateTimeOffset CreadoEn,
    DateTimeOffset VenceEn,
    DateTimeOffset? AbandonadoEn,
    IReadOnlyList<AsientoPedidoDto> Asientos,
    FuncionCarritoDto Funcion,
    DineroDto PrecioUnitario,
    DineroDto Total,
    string? EmailEntrega)
{
    public static CarritoDto DesdeLectura(CarritoLecturaDto lectura, DateTimeOffset ahora) =>
        new(lectura.Id,
            Carrito.EstadoEn(lectura.Estado, lectura.VenceEn, ahora),
            lectura.CreadoEn,
            lectura.VenceEn,
            lectura.AbandonadoEn,
            lectura.Asientos.OrderBy(a => a.Fila).ThenBy(a => a.Numero).Select(a => new AsientoPedidoDto(a.Fila.ToString(), a.Numero)).ToList(),
            new FuncionCarritoDto(lectura.FuncionId, lectura.HoraInicio, lectura.PeliculaId, lectura.PeliculaTitulo,
                lectura.SucursalId, lectura.SucursalNombre, lectura.SalaId, lectura.SalaNombre),
            DineroDto.DesdeDominio(lectura.PrecioUnitario),
            DineroDto.DesdeDominio(lectura.PrecioUnitario.Multiplicar(lectura.Asientos.Count)),
            lectura.EmailEntrega);
}
