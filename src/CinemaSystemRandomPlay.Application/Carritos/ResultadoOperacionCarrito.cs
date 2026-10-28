using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;

namespace CinemaSystemRandomPlay.Application.Carritos;

public enum EstadoOperacionCarrito
{
    Ok,
    PedidoInvalido,
    DesafioRechazado,
    FuncionNoEncontrada,
    FuncionNoDisponible,
    AsientosInexistentes,
    AsientosNoDisponibles,
    FuncionSinCupo,
    CarritoNoEncontrado,
    CarritoNoActivo
}

/// <summary>Resultado común de las operaciones sobre un carrito. <see cref="Token"/> solo viene al crearlo.</summary>
public record ResultadoOperacionCarrito(
    EstadoOperacionCarrito Estado,
    CarritoDto? Carrito = null,
    TokenDeCarrito? Token = null,
    IReadOnlyList<AsientoPedidoDto>? Asientos = null,
    EstadoCarrito? EstadoActual = null,
    IReadOnlyDictionary<string, string[]>? Errores = null)
{
    public static ResultadoOperacionCarrito Ok(CarritoDto carrito, TokenDeCarrito? token = null) =>
        new(EstadoOperacionCarrito.Ok, carrito, token);

    public static ResultadoOperacionCarrito PedidoInvalido(IReadOnlyDictionary<string, string[]> errores) =>
        new(EstadoOperacionCarrito.PedidoInvalido, Errores: errores);

    public static ResultadoOperacionCarrito DesafioRechazado() => new(EstadoOperacionCarrito.DesafioRechazado);

    public static ResultadoOperacionCarrito FuncionNoEncontrada() => new(EstadoOperacionCarrito.FuncionNoEncontrada);

    public static ResultadoOperacionCarrito CarritoNoEncontrado() => new(EstadoOperacionCarrito.CarritoNoEncontrado);

    public static ResultadoOperacionCarrito CarritoNoActivo(EstadoCarrito estadoActual) =>
        new(EstadoOperacionCarrito.CarritoNoActivo, EstadoActual: estadoActual);

    /// <summary>Traduce un rechazo de <see cref="AsignadorDeAsientos"/>. No debe invocarse con un resultado Ok.</summary>
    public static ResultadoOperacionCarrito DesdeRechazo(ResultadoSeleccion rechazo) => rechazo.Tipo switch
    {
        TipoResultadoSeleccion.CarritoNoActivo => CarritoNoActivo(rechazo.EstadoActual!.Value),
        TipoResultadoSeleccion.FuncionNoDisponible => new(EstadoOperacionCarrito.FuncionNoDisponible),
        TipoResultadoSeleccion.AsientosInexistentes =>
            new(EstadoOperacionCarrito.AsientosInexistentes, Asientos: ValidacionDeAsientos.ADtos(rechazo.Asientos!)),
        TipoResultadoSeleccion.AsientosNoDisponibles =>
            new(EstadoOperacionCarrito.AsientosNoDisponibles, Asientos: ValidacionDeAsientos.ADtos(rechazo.Asientos!)),
        TipoResultadoSeleccion.FuncionSinCupo => new(EstadoOperacionCarrito.FuncionSinCupo),
        _ => throw new InvalidOperationException($"{rechazo.Tipo} no es un rechazo.")
    };
}
