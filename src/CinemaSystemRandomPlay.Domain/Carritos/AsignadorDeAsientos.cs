using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Carritos;

public enum TipoResultadoSeleccion
{
    Ok,
    CarritoNoActivo,
    CarritoEnPago,
    FuncionNoDisponible,
    AsientosInexistentes,
    AsientosNoDisponibles,
    FuncionSinCupo
}

public sealed record ResultadoSeleccion(
    TipoResultadoSeleccion Tipo,
    Carrito? Carrito = null,
    IReadOnlyList<Asiento>? Asientos = null,
    EstadoCarrito? EstadoActual = null,
    IReadOnlyList<Asiento>? Liberados = null,
    IReadOnlyList<Asiento>? Retenidos = null)
{
    public static ResultadoSeleccion Creado(Carrito carrito) => new(TipoResultadoSeleccion.Ok, carrito);

    public static ResultadoSeleccion Reemplazado(Carrito carrito, IReadOnlyList<Asiento> liberados, IReadOnlyList<Asiento> retenidos) =>
        new(TipoResultadoSeleccion.Ok, carrito, Liberados: liberados, Retenidos: retenidos);

    public static ResultadoSeleccion CarritoNoActivo(EstadoCarrito estadoActual) => new(TipoResultadoSeleccion.CarritoNoActivo, EstadoActual: estadoActual);

    public static ResultadoSeleccion CarritoEnPago() => new(TipoResultadoSeleccion.CarritoEnPago, EstadoActual: EstadoCarrito.Pagando);

    public static ResultadoSeleccion FuncionNoDisponible() => new(TipoResultadoSeleccion.FuncionNoDisponible);

    public static ResultadoSeleccion AsientosInexistentes(IReadOnlyList<Asiento> asientos) => new(TipoResultadoSeleccion.AsientosInexistentes, Asientos: asientos);

    public static ResultadoSeleccion AsientosNoDisponibles(IReadOnlyList<Asiento> asientos) => new(TipoResultadoSeleccion.AsientosNoDisponibles, Asientos: asientos);

    public static ResultadoSeleccion FuncionSinCupo() => new(TipoResultadoSeleccion.FuncionSinCupo);
}

/// <summary>
/// Reglas que cruzan carritos: un asiento no puede estar retenido por dos carritos (activos, en pago o pagados) y los asientos retenidos de una
/// función no pueden superar el tope global. Quien lo invoca debe haber serializado los pedidos de la función.
/// </summary>
public class AsignadorDeAsientos
{
    public ResultadoSeleccion Crear(
        Funcion funcion,
        IReadOnlyList<Carrito> carritosDeLaFuncion,
        IReadOnlyList<Asiento> pedidos,
        DateTimeOffset ahora,
        ReglasCarrito reglas)
    {
        var rechazo = Validar(funcion, carritosDeLaFuncion, propio: null, pedidos, ahora, reglas);
        if (rechazo is not null)
            return rechazo;

        var venceEn = Minimo(ahora.AddMinutes(reglas.MinutosRetencion), funcion.FechaHoraInicio);

        return ResultadoSeleccion.Creado(Carrito.Crear(Guid.NewGuid(), funcion.Id, pedidos, funcion.PrecioEntrada, ahora, venceEn,
            reglas.MaximoAsientosPorCarrito));
    }

    /// <summary>Todo o nada: si el pedido se rechaza, el carrito queda sin cambios.</summary>
    public ResultadoSeleccion Reemplazar(
        Funcion funcion,
        Carrito carrito,
        IReadOnlyList<Carrito> carritosDeLaFuncion,
        IReadOnlyList<Asiento> pedidos,
        DateTimeOffset ahora,
        ReglasCarrito reglas)
    {
        var estado = carrito.EstadoEn(ahora);
        if (estado == EstadoCarrito.Pagando)
            return ResultadoSeleccion.CarritoEnPago();

        if (estado != EstadoCarrito.Activo)
            return ResultadoSeleccion.CarritoNoActivo(estado);

        var rechazo = Validar(funcion, carritosDeLaFuncion, carrito, pedidos, ahora, reglas);
        if (rechazo is not null)
            return rechazo;

        var (liberados, retenidos) = carrito.ReemplazarAsientos(pedidos, ahora, reglas.MaximoAsientosPorCarrito);

        return ResultadoSeleccion.Reemplazado(carrito, liberados, retenidos);
    }

    private static ResultadoSeleccion? Validar(
        Funcion funcion,
        IReadOnlyList<Carrito> carritosDeLaFuncion,
        Carrito? propio,
        IReadOnlyList<Asiento> pedidos,
        DateTimeOffset ahora,
        ReglasCarrito reglas)
    {
        if (funcion.YaComenzo(ahora))
            return ResultadoSeleccion.FuncionNoDisponible();

        var inexistentes = Ordenados(pedidos.Where(a => !funcion.Sala!.Contiene(a)));
        if (inexistentes.Count > 0)
            return ResultadoSeleccion.AsientosInexistentes(inexistentes);

        // Los asientos del propio carrito cuentan como disponibles para él (FR-010).
        var otros = carritosDeLaFuncion
            .Where(c => c.FuncionId == funcion.Id && c.RetieneAsientosEn(ahora) && (propio is null || c.Id != propio.Id))
            .ToList();

        var ocupadosPorOtros = otros.SelectMany(c => c.Asientos).ToHashSet();
        var noDisponibles = Ordenados(pedidos.Where(ocupadosPorOtros.Contains));
        if (noDisponibles.Count > 0)
            return ResultadoSeleccion.AsientosNoDisponibles(noDisponibles);

        // El tope global limita la retención temporal (carritos activos o en pago), no las ventas: los asientos de carritos
        // pagados no están disponibles, pero no cuentan para el tope (spec 007 FR-022; spec 008).
        var retenidosPorOtros = otros.Where(c => c.Estado != EstadoCarrito.Pagado).Sum(c => c.Asientos.Count);
        if (retenidosPorOtros + pedidos.Count > reglas.TopeRetenidoPara(funcion.Sala!.TotalAsientos))
            return ResultadoSeleccion.FuncionSinCupo();

        return null;
    }

    private static IReadOnlyList<Asiento> Ordenados(IEnumerable<Asiento> asientos) =>
        asientos.Distinct().OrderBy(a => a.Fila).ThenBy(a => a.Numero).ToList();

    private static DateTimeOffset Minimo(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
