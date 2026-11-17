using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;

namespace CinemaSystemRandomPlay.Api.Modulos.Carritos.Contracts;

/// <summary>Carrito del comprador anónimo con los asientos que retiene.</summary>
/// <param name="Estado">
/// <c>Activo</c>, <c>Pagando</c>, <c>Pagado</c>, <c>Expirado</c> o <c>Abandonado</c>. Activo y Pagando retienen asientos hasta
/// <c>venceEn</c>; Pagado, para siempre. Solo un carrito activo admite cambios.
/// </param>
/// <param name="CreadoEn">Momento de creación.</param>
/// <param name="VenceEn">Hasta cuándo se retienen los asientos si no se pagan; pasado ese momento el carrito expira. Gana un minuto al iniciar el pago.</param>
/// <param name="AbandonadoEn">Momento del abandono; <c>null</c> si no fue abandonado.</param>
/// <param name="Asientos">Asientos retenidos.</param>
/// <param name="Funcion">Función a la que pertenecen los asientos.</param>
/// <param name="PrecioUnitario">Precio de una entrada.</param>
/// <param name="Total">Precio unitario por cantidad de asientos.</param>
/// <param name="EmailEntrega">Email al que se enviarán las entradas; <c>null</c> hasta que se indique.</param>
/// <param name="Pago">Pago en Mercado Pago; <c>null</c> mientras no se inició.</param>
/// <param name="Entradas">Una por asiento; vacío salvo en <c>Pagado</c>.</param>
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
    string? EmailEntrega,
    PagoResponse? Pago,
    IReadOnlyList<EntradaResponse> Entradas)
{
    public static CarritoResponse DesdeDto(CarritoDto dto) =>
        new(dto.Id, dto.Estado.ToString(), dto.CreadoEn, dto.VenceEn, dto.AbandonadoEn,
            dto.Asientos.Select(AsientoResponse.DesdeDto).ToList(), FuncionCarritoResponse.DesdeDto(dto.Funcion),
            DineroResponse.DesdeDto(dto.PrecioUnitario), DineroResponse.DesdeDto(dto.Total), dto.EmailEntrega,
            dto.Pago is null ? null : PagoResponse.DesdeDto(dto.Pago),
            dto.Entradas.Select(EntradaResponse.DesdeDto).ToList());
}

/// <param name="UrlPago">Página de pago de Mercado Pago; sirve para volver a ella mientras el carrito esté en <c>Pagando</c>.</param>
/// <param name="IniciadoEn">Momento en que se inició el pago.</param>
/// <param name="PagadoEn">Momento en que se confirmó el pago; <c>null</c> hasta entonces.</param>
public record PagoResponse(string UrlPago, DateTimeOffset IniciadoEn, DateTimeOffset? PagadoEn)
{
    public static PagoResponse DesdeDto(PagoDto dto) => new(dto.UrlPago, dto.IniciadoEn, dto.PagadoEn);
}

/// <summary>Entrada emitida al pagar. Los datos de la función quedan copiados tal como estaban al pagar.</summary>
/// <param name="Codigo">Código único de la entrada, con formato <c>XXXXX-XXXXX</c>.</param>
public record EntradaResponse(
    string Codigo,
    string Pelicula,
    string Sucursal,
    string Sala,
    DateTimeOffset HoraInicio,
    AsientoResponse Asiento,
    DineroResponse PrecioUnitario)
{
    public static EntradaResponse DesdeDto(EntradaDto dto) =>
        new(dto.Codigo, dto.PeliculaTitulo, dto.SucursalNombre, dto.SalaNombre, dto.HoraInicio, AsientoResponse.DesdeDto(dto.Asiento),
            DineroResponse.DesdeDto(dto.PrecioUnitario));
}

/// <summary>Respuesta de iniciar el pago.</summary>
/// <param name="UrlPago">Página de pago de Mercado Pago a la que se redirige al comprador.</param>
/// <param name="Carrito">El carrito, en estado <c>Pagando</c>.</param>
public record PagoIniciadoResponse(string UrlPago, CarritoResponse Carrito)
{
    public static PagoIniciadoResponse DesdeDto(string urlPago, CarritoDto carrito) => new(urlPago, CarritoResponse.DesdeDto(carrito));
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
