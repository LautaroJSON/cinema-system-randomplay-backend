using CinemaSystemRandomPlay.Domain.Carritos;

namespace CinemaSystemRandomPlay.Domain.Pagos;

public sealed record DecisionDePago(bool Acreditar, MotivoDevolucion? Motivo)
{
    public static DecisionDePago Acreditarlo() => new(true, null);

    public static DecisionDePago Devolver(MotivoDevolucion motivo) => new(false, motivo);
}

/// <summary>
/// Decide si un pago aprobado paga el carrito o se devuelve (spec 008, FR-015 y FR-019). Cuenta la hora de aprobación que
/// informa la pasarela, no la de llegada del aviso: un pago aprobado a tiempo se acredita aunque el aviso llegue tarde, si
/// sus asientos siguen libres. Quien lo invoca debe haber serializado los cambios de ocupación de la función.
/// </summary>
public class ConfirmadorDePagos
{
    public DecisionDePago Decidir(Carrito carrito, IReadOnlyList<Carrito> carritosQueRetienenDeLaFuncion, PagoAprobado pago,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(carrito);
        ArgumentNullException.ThrowIfNull(pago);

        if (carrito.Estado == EstadoCarrito.Pagado)
            return DecisionDePago.Devolver(MotivoDevolucion.CarritoYaPagado);

        if (carrito.Estado != EstadoCarrito.Pagando)
            return DecisionDePago.Devolver(MotivoDevolucion.PagoTardio);

        if (pago.Monto != carrito.Total)
            return DecisionDePago.Devolver(MotivoDevolucion.MontoDistinto);

        if (pago.AprobadoEn > carrito.VenceEn)
            return DecisionDePago.Devolver(MotivoDevolucion.PagoTardio);

        if (carrito.VenceEn <= ahora && AlgunAsientoTomado(carrito, carritosQueRetienenDeLaFuncion, ahora))
            return DecisionDePago.Devolver(MotivoDevolucion.AsientosNoDisponibles);

        return DecisionDePago.Acreditarlo();
    }

    private static bool AlgunAsientoTomado(Carrito carrito, IReadOnlyList<Carrito> otros, DateTimeOffset ahora)
    {
        var tomados = otros
            .Where(c => c.Id != carrito.Id && c.FuncionId == carrito.FuncionId && c.RetieneAsientosEn(ahora))
            .SelectMany(c => c.Asientos)
            .ToHashSet();

        return carrito.Asientos.Any(tomados.Contains);
    }
}
