using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.Reservas;

public class Reserva
{
    private readonly List<Asiento> _asientos = new();

    public Guid Id { get; private set; }
    public Guid FuncionId { get; private set; }
    public EmailComprador Comprador { get; private set; } = null!;
    public EstadoReserva Estado { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public DateTimeOffset? CanceladaEn { get; private set; }
    public Dinero PrecioUnitario { get; private set; } = null!;
    public Dinero Total => PrecioUnitario.Multiplicar(_asientos.Count);

    public IReadOnlyList<Asiento> Asientos => _asientos.OrderBy(a => a.Fila).ThenBy(a => a.Numero).ToList();

    private Reserva()
    {
    }

    internal static Reserva Crear(Guid id, Guid funcionId, EmailComprador comprador, IReadOnlyList<Asiento> asientos,
        Dinero precioUnitario, DateTimeOffset creadaEn, DateTimeOffset venceEn, int maximoAsientos)
    {
        if (funcionId == Guid.Empty)
            throw new DomainException("La Reserva debe tener una Funcion asociada.");

        if (comprador is null)
            throw new DomainException("La Reserva debe tener un comprador.");

        if (asientos is null || asientos.Count < 1 || asientos.Count > maximoAsientos)
            throw new DomainException($"La Reserva debe tener entre 1 y {maximoAsientos} asientos.");

        if (asientos.Distinct().Count() != asientos.Count)
            throw new DomainException("La Reserva no puede tener asientos repetidos.");

        if (precioUnitario is null)
            throw new DomainException("La Reserva debe tener un precio unitario.");

        var reserva = new Reserva
        {
            Id = id,
            FuncionId = funcionId,
            Comprador = comprador,
            Estado = EstadoReserva.PendienteDePago,
            CreadaEn = creadaEn,
            VenceEn = venceEn,
            PrecioUnitario = precioUnitario
        };
        reserva._asientos.AddRange(asientos);
        return reserva;
    }

    public EstadoReserva EstadoEn(DateTimeOffset ahora) => EstadoEn(Estado, VenceEn, ahora);

    public static EstadoReserva EstadoEn(EstadoReserva guardado, DateTimeOffset venceEn, DateTimeOffset ahora)
    {
        if (guardado == EstadoReserva.Cancelada)
            return EstadoReserva.Cancelada;

        return venceEn <= ahora ? EstadoReserva.Expirada : EstadoReserva.PendienteDePago;
    }

    public bool RetieneAsientosEn(DateTimeOffset ahora) => EstadoEn(ahora) == EstadoReserva.PendienteDePago;

    public ResultadoCancelacion Cancelar(DateTimeOffset ahora)
    {
        var estadoActual = EstadoEn(ahora);

        if (estadoActual != EstadoReserva.PendienteDePago)
            return ResultadoCancelacion.NoCancelable(estadoActual);

        Estado = EstadoReserva.Cancelada;
        CanceladaEn = ahora;
        return ResultadoCancelacion.Ok();
    }
}
