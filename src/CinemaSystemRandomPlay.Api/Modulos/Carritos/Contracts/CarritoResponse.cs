using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;

namespace CinemaSystemRandomPlay.Api.Modulos.Carritos.Contracts;

/// <summary>Carrito del comprador anónimo con los asientos que retiene.</summary>
/// <param name="Estado"><c>Activo</c>, <c>Abandonado</c> o <c>Expirado</c>. Solo un carrito activo retiene asientos y admite cambios.</param>
/// <param name="CreadoEn">Momento de creación.</param>
/// <param name="VenceEn">Hasta cuándo se retienen los asientos; pasado ese momento el carrito expira.</param>
/// <param name="AbandonadoEn">Momento del abandono; <c>null</c> si no fue abandonado.</param>
/// <param name="Asientos">Asientos retenidos.</param>
/// <param name="Funcion">Función a la que pertenecen los asientos.</param>
/// <param name="PrecioUnitario">Precio de una entrada.</param>
/// <param name="Total">Precio unitario por cantidad de asientos.</param>
/// <param name="EmailEntrega">Email al que se enviarán las entradas; <c>null</c> hasta que se indique.</param>
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
/// <param name="Carrito">El carrito creado.</param>
/// <param name="Token">JWT de acceso a este carrito. Enviarlo como <c>Authorization: Bearer {token}</c> en las demás operaciones.</param>
/// <param name="TokenVenceEn">Vencimiento del token.</param>
public record CarritoCreadoResponse(CarritoResponse Carrito, string Token, DateTimeOffset TokenVenceEn)
{
    public static CarritoCreadoResponse DesdeDto(CarritoDto carrito, TokenDeCarrito token) =>
        new(CarritoResponse.DesdeDto(carrito), token.Valor, token.VenceEn);
}

/// <param name="Fila">Letra de la fila.</param>
/// <param name="Numero">Número de asiento dentro de la fila.</param>
public record AsientoResponse(string Fila, int Numero)
{
    public static AsientoResponse DesdeDto(AsientoPedidoDto dto) => new(dto.Fila, dto.Numero);
}

/// <param name="HoraInicio">Inicio de la función.</param>
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
