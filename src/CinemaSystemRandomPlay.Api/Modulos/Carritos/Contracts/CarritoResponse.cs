using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;

namespace CinemaSystemRandomPlay.Api.Modulos.Carritos.Contracts;

public record CarritoResponse(
    Guid Id,
    string Estado,
    DateTimeOffset CreadoEn,
    DateTimeOffset VenceEn,
    DateTimeOffset? AbandonadoEn,
    IReadOnlyList<AsientoResponse> Asientos,
    FuncionCarritoResponse Funcion,
    DineroResponse PrecioUnitario,
    DineroResponse Total,
    string? EmailEntrega)
{
    public static CarritoResponse DesdeDto(CarritoDto dto) =>
        new(dto.Id, dto.Estado.ToString(), dto.CreadoEn, dto.VenceEn, dto.AbandonadoEn,
            dto.Asientos.Select(AsientoResponse.DesdeDto).ToList(), FuncionCarritoResponse.DesdeDto(dto.Funcion),
            DineroResponse.DesdeDto(dto.PrecioUnitario), DineroResponse.DesdeDto(dto.Total), dto.EmailEntrega);
}

/// <summary>Respuesta de la creación: la única vez que se entrega el token del carrito.</summary>
public record CarritoCreadoResponse(CarritoResponse Carrito, string Token, DateTimeOffset TokenVenceEn)
{
    public static CarritoCreadoResponse DesdeDto(CarritoDto carrito, TokenDeCarrito token) =>
        new(CarritoResponse.DesdeDto(carrito), token.Valor, token.VenceEn);
}

public record AsientoResponse(string Fila, int Numero)
{
    public static AsientoResponse DesdeDto(AsientoPedidoDto dto) => new(dto.Fila, dto.Numero);
}

public record FuncionCarritoResponse(
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid PeliculaId,
    string PeliculaTitulo,
    Guid SucursalId,
    string SucursalNombre,
    Guid SalaId,
    string SalaNombre)
{
    public static FuncionCarritoResponse DesdeDto(FuncionCarritoDto dto) =>
        new(dto.FuncionId, dto.HoraInicio, dto.PeliculaId, dto.PeliculaTitulo, dto.SucursalId, dto.SucursalNombre, dto.SalaId, dto.SalaNombre);
}
