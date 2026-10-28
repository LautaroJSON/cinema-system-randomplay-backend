using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Carritos;

/// <summary>
/// Retención temporal de asientos de una función por un comprador anónimo. El vencimiento se fija al crearse y ningún
/// reemplazo de la selección lo modifica. Quien opera el carrito lo prueba con su token, que no forma parte del agregado.
/// </summary>
public class Carrito
{
    private readonly List<Asiento> _asientos = new();

    public Guid Id { get; private set; }
    public Guid FuncionId { get; private set; }
    public EstadoCarrito Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public DateTimeOffset? AbandonadoEn { get; private set; }
    public EmailComprador? EmailEntrega { get; private set; }
    public Dinero PrecioUnitario { get; private set; } = null!;
    public Dinero Total => PrecioUnitario.Multiplicar(_asientos.Count);

    public IReadOnlyList<Asiento> Asientos => _asientos.OrderBy(a => a.Fila).ThenBy(a => a.Numero).ToList();

    private Carrito()
    {
    }

    internal static Carrito Crear(Guid id, Guid funcionId, IReadOnlyList<Asiento> asientos, Dinero precioUnitario,
        DateTimeOffset creadoEn, DateTimeOffset venceEn, int maximoAsientos)
    {
        if (funcionId == Guid.Empty)
            throw new DomainException("El Carrito debe tener una Funcion asociada.");

        ValidarSeleccion(asientos, maximoAsientos);

        if (precioUnitario is null)
            throw new DomainException("El Carrito debe tener un precio unitario.");

        var carrito = new Carrito
        {
            Id = id,
            FuncionId = funcionId,
            Estado = EstadoCarrito.Activo,
            CreadoEn = creadoEn,
            VenceEn = venceEn,
            PrecioUnitario = precioUnitario
        };
        carrito._asientos.AddRange(asientos);
        return carrito;
    }

    public EstadoCarrito EstadoEn(DateTimeOffset ahora) => EstadoEn(Estado, VenceEn, ahora);

    public static EstadoCarrito EstadoEn(EstadoCarrito guardado, DateTimeOffset venceEn, DateTimeOffset ahora)
    {
        if (guardado == EstadoCarrito.Abandonado)
            return EstadoCarrito.Abandonado;

        return venceEn <= ahora ? EstadoCarrito.Expirado : EstadoCarrito.Activo;
    }

    public bool RetieneAsientosEn(DateTimeOffset ahora) => EstadoEn(ahora) == EstadoCarrito.Activo;

    /// <summary>
    /// Deja exactamente <paramref name="nuevos"/> como selección. Aplica la diferencia: los asientos comunes no se quitan
    /// ni se vuelven a agregar, así que nunca quedan libres. No toca el vencimiento. La disponibilidad frente a otros
    /// carritos la valida <see cref="AsignadorDeAsientos"/> antes de invocarlo.
    /// </summary>
    internal (IReadOnlyList<Asiento> Liberados, IReadOnlyList<Asiento> Retenidos) ReemplazarAsientos(
        IReadOnlyList<Asiento> nuevos, DateTimeOffset ahora, int maximoAsientos)
    {
        if (!RetieneAsientosEn(ahora))
            throw new DomainException("Solo se puede cambiar la selección de un Carrito activo.");

        ValidarSeleccion(nuevos, maximoAsientos);

        var nuevosSet = nuevos.ToHashSet();
        var liberados = Ordenados(_asientos.Where(a => !nuevosSet.Contains(a)));
        var retenidos = Ordenados(nuevos.Where(a => !_asientos.Contains(a)));

        _asientos.RemoveAll(a => !nuevosSet.Contains(a));
        _asientos.AddRange(retenidos);

        return (liberados, retenidos);
    }

    public ResultadoAbandono Abandonar(DateTimeOffset ahora)
    {
        var estadoActual = EstadoEn(ahora);

        if (estadoActual != EstadoCarrito.Activo)
            return ResultadoAbandono.NoActivo(estadoActual);

        Estado = EstadoCarrito.Abandonado;
        AbandonadoEn = ahora;
        return ResultadoAbandono.Ok();
    }

    /// <summary>Email al que se enviará la entrada. No se verifica ni es credencial; se puede reemplazar mientras esté activo.</summary>
    public ResultadoEmailEntrega IndicarEmailEntrega(EmailComprador email, DateTimeOffset ahora)
    {
        if (email is null)
            throw new DomainException("El email de entrega es obligatorio.");

        var estadoActual = EstadoEn(ahora);

        if (estadoActual != EstadoCarrito.Activo)
            return ResultadoEmailEntrega.NoActivo(estadoActual);

        EmailEntrega = email;
        return ResultadoEmailEntrega.Ok();
    }

    private static void ValidarSeleccion(IReadOnlyList<Asiento> asientos, int maximoAsientos)
    {
        if (asientos is null || asientos.Count < 1 || asientos.Count > maximoAsientos)
            throw new DomainException($"El Carrito debe tener entre 1 y {maximoAsientos} asientos.");

        if (asientos.Distinct().Count() != asientos.Count)
            throw new DomainException("El Carrito no puede tener asientos repetidos.");
    }

    private static IReadOnlyList<Asiento> Ordenados(IEnumerable<Asiento> asientos) =>
        asientos.OrderBy(a => a.Fila).ThenBy(a => a.Numero).ToList();
}
