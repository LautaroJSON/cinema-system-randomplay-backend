using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Pagos;

namespace CinemaSystemRandomPlay.Domain.Carritos;

/// <summary>
/// Retención temporal de asientos de una función por un comprador anónimo. El vencimiento se fija al crearse y cambia una
/// sola vez: al iniciar el pago gana un minuto, sin pasar del inicio de la función. Ningún reemplazo de la selección lo
/// modifica. Quien opera el carrito lo prueba con su token, que no forma parte del agregado.
/// </summary>
public class Carrito
{
    /// <summary>Tiempo extra de retención que gana el carrito al iniciar el pago (spec 008, FR-003).</summary>
    public static readonly TimeSpan ExtensionAlIniciarPago = TimeSpan.FromMinutes(1);

    private readonly List<Asiento> _asientos = new();
    private readonly List<Entrada> _entradas = new();

    public Guid Id { get; private set; }
    public Guid FuncionId { get; private set; }
    public EstadoCarrito Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public DateTimeOffset? AbandonadoEn { get; private set; }
    public EmailComprador? EmailEntrega { get; private set; }
    public Dinero PrecioUnitario { get; private set; } = null!;
    public Dinero Total => PrecioUnitario.Multiplicar(_asientos.Count);

    /// <summary>Preferencia de pago creada en la pasarela al pasar a <see cref="EstadoCarrito.Pagando"/>.</summary>
    public string? PreferenciaPagoId { get; private set; }

    /// <summary>Página de pago de la pasarela; se conserva para volver a ella mientras el carrito esté en pago.</summary>
    public string? UrlPago { get; private set; }

    public DateTimeOffset? PagoIniciadoEn { get; private set; }

    /// <summary>Pago que pagó el carrito.</summary>
    public string? PagoId { get; private set; }

    /// <summary>Hora de aprobación informada por la pasarela, no la de llegada del aviso.</summary>
    public DateTimeOffset? PagoAprobadoEn { get; private set; }

    public DateTimeOffset? PagadoEn { get; private set; }

    /// <summary>Momento en que el servicio de email aceptó el email de entrega.</summary>
    public DateTimeOffset? EntregaEnviadaEn { get; private set; }

    public IReadOnlyList<Asiento> Asientos => _asientos.OrderBy(a => a.Fila).ThenBy(a => a.Numero).ToList();

    public IReadOnlyList<Entrada> Entradas => _entradas.OrderBy(e => e.Asiento.Fila).ThenBy(e => e.Asiento.Numero).ToList();

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

    /// <summary>
    /// Estado efectivo: <c>Abandonado</c> y <c>Pagado</c> son finales; <c>Activo</c> y <c>Pagando</c> pasan a <c>Expirado</c>
    /// en el vencimiento, sin que nadie intervenga.
    /// </summary>
    public static EstadoCarrito EstadoEn(EstadoCarrito guardado, DateTimeOffset venceEn, DateTimeOffset ahora)
    {
        if (guardado is EstadoCarrito.Abandonado or EstadoCarrito.Pagado)
            return guardado;

        return venceEn <= ahora ? EstadoCarrito.Expirado : guardado;
    }

    /// <summary>Un carrito activo o en pago retiene sus asientos hasta vencer; uno pagado, para siempre.</summary>
    public bool RetieneAsientosEn(DateTimeOffset ahora) =>
        EstadoEn(ahora) is EstadoCarrito.Activo or EstadoCarrito.Pagando or EstadoCarrito.Pagado;

    /// <summary>
    /// Deja exactamente <paramref name="nuevos"/> como selección. Aplica la diferencia: los asientos comunes no se quitan
    /// ni se vuelven a agregar, así que nunca quedan libres. No toca el vencimiento. La disponibilidad frente a otros
    /// carritos la valida <see cref="AsignadorDeAsientos"/> antes de invocarlo.
    /// </summary>
    internal (IReadOnlyList<Asiento> Liberados, IReadOnlyList<Asiento> Retenidos) ReemplazarAsientos(
        IReadOnlyList<Asiento> nuevos, DateTimeOffset ahora, int maximoAsientos)
    {
        if (EstadoEn(ahora) != EstadoCarrito.Activo)
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

        if (estadoActual == EstadoCarrito.Pagando)
            return ResultadoAbandono.EnPago();

        if (estadoActual != EstadoCarrito.Activo)
            return ResultadoAbandono.NoActivo(estadoActual);

        Estado = EstadoCarrito.Abandonado;
        AbandonadoEn = ahora;
        return ResultadoAbandono.Ok();
    }

    /// <summary>
    /// Email al que se enviará la entrada. No se verifica ni es credencial; se puede reemplazar mientras esté activo. Desde
    /// que se inicia el pago queda fijo.
    /// </summary>
    public ResultadoEmailEntrega IndicarEmailEntrega(EmailComprador email, DateTimeOffset ahora)
    {
        if (email is null)
            throw new DomainException("El email de entrega es obligatorio.");

        var estadoActual = EstadoEn(ahora);

        if (estadoActual == EstadoCarrito.Pagando)
            return ResultadoEmailEntrega.EnPago();

        if (estadoActual != EstadoCarrito.Activo)
            return ResultadoEmailEntrega.NoActivo(estadoActual);

        EmailEntrega = email;
        return ResultadoEmailEntrega.Ok();
    }

    /// <summary>Vencimiento que tendrá el carrito al iniciar el pago: un minuto más, sin pasar del inicio de la función.</summary>
    public DateTimeOffset VencimientoAlIniciarPago(DateTimeOffset inicioFuncion) => VencimientoAlIniciarPago(VenceEn, inicioFuncion);

    public static DateTimeOffset VencimientoAlIniciarPago(DateTimeOffset venceEn, DateTimeOffset inicioFuncion)
    {
        var extendido = venceEn + ExtensionAlIniciarPago;
        return extendido <= inicioFuncion ? extendido : inicioFuncion;
    }

    /// <summary>
    /// Pasa el carrito a <see cref="EstadoCarrito.Pagando"/> una sola vez. La selección debe ser la misma que se envió a la
    /// pasarela (<paramref name="asientosEsperados"/>), para que lo que se cobra sea exactamente el carrito.
    /// </summary>
    public ResultadoInicioPago IniciarPago(string preferenciaId, string urlPago, IReadOnlyCollection<Asiento> asientosEsperados,
        DateTimeOffset ahora, DateTimeOffset inicioFuncion)
    {
        if (string.IsNullOrWhiteSpace(preferenciaId) || string.IsNullOrWhiteSpace(urlPago))
            throw new DomainException("La preferencia y la URL de pago son obligatorias.");

        var estadoActual = EstadoEn(ahora);

        if (estadoActual == EstadoCarrito.Pagando)
            return ResultadoInicioPago.YaEnPago(UrlPago!);

        if (estadoActual != EstadoCarrito.Activo)
            return ResultadoInicioPago.NoActivo(estadoActual);

        if (EmailEntrega is null)
            return ResultadoInicioPago.SinEmailEntrega();

        if (!_asientos.ToHashSet().SetEquals(asientosEsperados))
            return ResultadoInicioPago.SeleccionModificada();

        VenceEn = VencimientoAlIniciarPago(inicioFuncion);
        Estado = EstadoCarrito.Pagando;
        PreferenciaPagoId = preferenciaId;
        UrlPago = urlPago;
        PagoIniciadoEn = ahora;

        return ResultadoInicioPago.Iniciado(urlPago);
    }

    /// <summary>
    /// Marca el carrito como pagado y emite una entrada por asiento. Las reglas que deciden si el pago se acredita (vencimiento,
    /// monto, asientos libres) las aplica <see cref="ConfirmadorDePagos"/> antes de invocarlo.
    /// </summary>
    public void MarcarPagado(PagoAprobado pago, DateTimeOffset ahora, DatosDeLaFuncion funcion, IGeneradorDeCodigos generador)
    {
        ArgumentNullException.ThrowIfNull(pago);
        ArgumentNullException.ThrowIfNull(funcion);
        ArgumentNullException.ThrowIfNull(generador);

        if (Estado != EstadoCarrito.Pagando)
            throw new DomainException("Solo se puede pagar un Carrito en pago.");

        if (funcion.FuncionId != FuncionId)
            throw new DomainException("Los datos de la función no corresponden al Carrito.");

        Estado = EstadoCarrito.Pagado;
        PagoId = pago.PagoId;
        PagoAprobadoEn = pago.AprobadoEn;
        PagadoEn = ahora;

        foreach (var asiento in Asientos)
            _entradas.Add(new Entrada(Guid.NewGuid(), generador.Generar(), Id, asiento, funcion, PrecioUnitario, ahora));
    }

    /// <summary>Registra que el email de entrega se envió. Idempotente: conserva el primer momento.</summary>
    public void MarcarEntregaEnviada(DateTimeOffset ahora)
    {
        if (Estado != EstadoCarrito.Pagado)
            throw new DomainException("Solo se envía la entrega de un Carrito pagado.");

        EntregaEnviadaEn ??= ahora;
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
