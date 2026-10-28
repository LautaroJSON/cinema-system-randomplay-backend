using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Reservas;

public enum TipoResultadoReserva
{
    Ok,
    FuncionNoDisponible,
    AsientosInexistentes,
    AsientosNoDisponibles,
    LimiteSuperado
}

public sealed record ResultadoReserva(
    TipoResultadoReserva Tipo,
    Reserva? Reserva = null,
    IReadOnlyList<Asiento>? Asientos = null,
    int AsientosRestantes = 0)
{
    public static ResultadoReserva Ok(Reserva reserva) => new(TipoResultadoReserva.Ok, reserva);

    public static ResultadoReserva FuncionNoDisponible() => new(TipoResultadoReserva.FuncionNoDisponible);

    public static ResultadoReserva AsientosInexistentes(IReadOnlyList<Asiento> asientos) => new(TipoResultadoReserva.AsientosInexistentes, Asientos: asientos);

    public static ResultadoReserva AsientosNoDisponibles(IReadOnlyList<Asiento> asientos) => new(TipoResultadoReserva.AsientosNoDisponibles, Asientos: asientos);

    public static ResultadoReserva LimiteSuperado(int asientosRestantes) => new(TipoResultadoReserva.LimiteSuperado, AsientosRestantes: asientosRestantes);
}

/// <summary>
/// Reglas que cruzan reservas: un asiento no puede estar retenido dos veces y un comprador no puede retener
/// más del máximo en una misma función. Quien lo invoca debe haber serializado los pedidos de la función.
/// </summary>
public class ReservadorDeAsientos
{
    public ResultadoReserva Reservar(
        Funcion funcion,
        IReadOnlyList<Reserva> reservasDeLaFuncion,
        EmailComprador comprador,
        IReadOnlyList<Asiento> pedidos,
        DateTimeOffset ahora,
        ReglasReserva reglas)
    {
        if (funcion.YaComenzo(ahora))
            return ResultadoReserva.FuncionNoDisponible();

        var inexistentes = Ordenados(pedidos.Where(a => !funcion.Sala!.Contiene(a)));
        if (inexistentes.Count > 0)
            return ResultadoReserva.AsientosInexistentes(inexistentes);

        var vigentes = reservasDeLaFuncion.Where(r => r.FuncionId == funcion.Id && r.RetieneAsientosEn(ahora)).ToList();

        var retenidos = vigentes.SelectMany(r => r.Asientos).ToHashSet();
        var noDisponibles = Ordenados(pedidos.Where(retenidos.Contains));
        if (noDisponibles.Count > 0)
            return ResultadoReserva.AsientosNoDisponibles(noDisponibles);

        var retenidosDelComprador = vigentes.Where(r => r.Comprador == comprador).Sum(r => r.Asientos.Count);
        if (retenidosDelComprador + pedidos.Count > reglas.MaximoAsientosPorComprador)
            return ResultadoReserva.LimiteSuperado(Math.Max(0, reglas.MaximoAsientosPorComprador - retenidosDelComprador));

        var venceEn = Minimo(ahora.AddMinutes(reglas.MinutosRetencion), funcion.FechaHoraInicio);

        return ResultadoReserva.Ok(Reserva.Crear(Guid.NewGuid(), funcion.Id, comprador, pedidos, funcion.PrecioEntrada, ahora, venceEn, reglas.MaximoAsientosPorComprador));
    }

    private static IReadOnlyList<Asiento> Ordenados(IEnumerable<Asiento> asientos) =>
        asientos.Distinct().OrderBy(a => a.Fila).ThenBy(a => a.Numero).ToList();

    private static DateTimeOffset Minimo(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
