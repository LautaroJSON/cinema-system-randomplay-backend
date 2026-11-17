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

/// <summary>Pago del carrito; existe desde que se inició.</summary>
public record PagoDto(string UrlPago, DateTimeOffset IniciadoEn, DateTimeOffset? PagadoEn);

public record EntradaDto(
    string Codigo,
    AsientoPedidoDto Asiento,
    string PeliculaTitulo,
    string SucursalNombre,
    string SalaNombre,
    DateTimeOffset HoraInicio,
    DineroDto PrecioUnitario);

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
    string? EmailEntrega,
    PagoDto? Pago,
    IReadOnlyList<EntradaDto> Entradas)
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
            lectura.EmailEntrega,
            lectura is { UrlPago: not null, PagoIniciadoEn: not null }
                ? new PagoDto(lectura.UrlPago, lectura.PagoIniciadoEn.Value, lectura.PagadoEn)
                : null,
            (lectura.Entradas ?? [])
                .OrderBy(e => e.Asiento.Fila).ThenBy(e => e.Asiento.Numero)
                .Select(e => new EntradaDto(e.Codigo, new AsientoPedidoDto(e.Asiento.Fila.ToString(), e.Asiento.Numero),
                    e.PeliculaTitulo, e.SucursalNombre, e.SalaNombre, e.HoraInicio, DineroDto.DesdeDominio(e.PrecioUnitario)))
                .ToList());
}
