using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Reservas.Dtos;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Domain.Reservas.Eventos;

namespace CinemaSystemRandomPlay.Application.Reservas.Commands;

public record ReservarAsientosCommand(Guid FuncionId, string ClienteId, IReadOnlyList<AsientoPedidoDto>? Asientos);

public enum EstadoReservarAsientos
{
    Ok,
    PedidoInvalido,
    FuncionNoEncontrada,
    FuncionNoDisponible,
    AsientosInexistentes,
    AsientosNoDisponibles,
    LimiteSuperado
}

public record ReservarAsientosResultado(
    EstadoReservarAsientos Estado,
    ReservaDto? Reserva = null,
    IReadOnlyList<AsientoPedidoDto>? Asientos = null,
    int AsientosRestantes = 0,
    IReadOnlyDictionary<string, string[]>? Errores = null)
{
    public static ReservarAsientosResultado Ok(ReservaDto reserva) => new(EstadoReservarAsientos.Ok, Reserva: reserva);

    public static ReservarAsientosResultado PedidoInvalido(IReadOnlyDictionary<string, string[]> errores) =>
        new(EstadoReservarAsientos.PedidoInvalido, Errores: errores);

    public static ReservarAsientosResultado FuncionNoEncontrada() => new(EstadoReservarAsientos.FuncionNoEncontrada);

    public static ReservarAsientosResultado FuncionNoDisponible() => new(EstadoReservarAsientos.FuncionNoDisponible);

    public static ReservarAsientosResultado AsientosInexistentes(IReadOnlyList<AsientoPedidoDto> asientos) =>
        new(EstadoReservarAsientos.AsientosInexistentes, Asientos: asientos);

    public static ReservarAsientosResultado AsientosNoDisponibles(IReadOnlyList<AsientoPedidoDto> asientos) =>
        new(EstadoReservarAsientos.AsientosNoDisponibles, Asientos: asientos);

    public static ReservarAsientosResultado LimiteSuperado(int asientosRestantes) =>
        new(EstadoReservarAsientos.LimiteSuperado, AsientosRestantes: asientosRestantes);
}

public class ReservarAsientosCommandHandler
{
    private const string CampoAsientos = "asientos";

    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IFuncionRepository _funcionRepository;
    private readonly IPeliculaRepository _peliculaRepository;
    private readonly IReservaRepository _reservaRepository;
    private readonly IOutbox _outbox;
    private readonly IReloj _reloj;
    private readonly ReservadorDeAsientos _reservador;
    private readonly ReglasReserva _reglas;

    public ReservarAsientosCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IFuncionRepository funcionRepository,
        IPeliculaRepository peliculaRepository,
        IReservaRepository reservaRepository,
        IOutbox outbox,
        IReloj reloj,
        ReservadorDeAsientos reservador,
        ReglasReserva reglas)
    {
        _unidadDeTrabajo = unidadDeTrabajo;
        _funcionRepository = funcionRepository;
        _peliculaRepository = peliculaRepository;
        _reservaRepository = reservaRepository;
        _outbox = outbox;
        _reloj = reloj;
        _reservador = reservador;
        _reglas = reglas;
    }

    public async Task<ReservarAsientosResultado> Handle(ReservarAsientosCommand command, CancellationToken cancellationToken = default)
    {
        var validacion = Validar(command.Asientos);

        if (validacion.Errores is not null)
            return ReservarAsientosResultado.PedidoInvalido(validacion.Errores);

        var pedidos = validacion.Asientos!;

        return await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            var funcion = await _funcionRepository.ObtenerConSalaBloqueando(command.FuncionId, ct);

            if (funcion?.Sala is null)
                return ReservarAsientosResultado.FuncionNoEncontrada();

            var pelicula = await _peliculaRepository.ObtenerPorId(funcion.PeliculaId, ct);

            if (pelicula is null || !pelicula.Activa)
                return ReservarAsientosResultado.FuncionNoEncontrada();

            var ahora = _reloj.Ahora;
            var vigentes = await _reservaRepository.ListarVigentesDeFuncion(funcion.Id, ahora, ct);

            var resultado = _reservador.Reservar(funcion, vigentes, command.ClienteId, pedidos, ahora, _reglas);

            switch (resultado.Tipo)
            {
                case TipoResultadoReserva.FuncionNoDisponible:
                    return ReservarAsientosResultado.FuncionNoDisponible();
                case TipoResultadoReserva.AsientosInexistentes:
                    return ReservarAsientosResultado.AsientosInexistentes(ADtos(resultado.Asientos!));
                case TipoResultadoReserva.AsientosNoDisponibles:
                    return ReservarAsientosResultado.AsientosNoDisponibles(ADtos(resultado.Asientos!));
                case TipoResultadoReserva.LimiteSuperado:
                    return ReservarAsientosResultado.LimiteSuperado(resultado.AsientosRestantes);
            }

            var reserva = resultado.Reserva!;

            await _reservaRepository.Agregar(reserva, ct);
            _outbox.Registrar(new ReservaCreada(reserva.Id, reserva.FuncionId, reserva.Asientos, reserva.VenceEn, ahora));

            var sala = funcion.Sala;

            return ReservarAsientosResultado.Ok(new ReservaDto(
                reserva.Id,
                reserva.EstadoEn(ahora),
                reserva.CreadaEn,
                reserva.VenceEn,
                reserva.CanceladaEn,
                ADtos(reserva.Asientos),
                new FuncionReservaDto(funcion.Id, funcion.FechaHoraInicio, pelicula.Id, pelicula.Titulo,
                    sala.SucursalId, sala.Sucursal?.Nombre ?? string.Empty, sala.Id, sala.Nombre)));
        }, cancellationToken);
    }

    private (IReadOnlyList<Asiento>? Asientos, IReadOnlyDictionary<string, string[]>? Errores) Validar(IReadOnlyList<AsientoPedidoDto>? pedidos)
    {
        var errores = new List<string>();

        if (pedidos is null || pedidos.Count == 0)
            return (null, Error("Debe indicar al menos un asiento."));

        if (pedidos.Count > _reglas.MaximoAsientosPorCliente)
            errores.Add($"Una reserva admite como máximo {_reglas.MaximoAsientosPorCliente} asientos.");

        var asientos = new List<Asiento>();

        foreach (var pedido in pedidos)
        {
            if (pedido is null
                || string.IsNullOrEmpty(pedido.Fila)
                || pedido.Fila.Length != 1
                || pedido.Fila[0] is < 'A' or > 'Z'
                || pedido.Numero < 1)
            {
                errores.Add("Cada asiento debe tener una fila de una letra mayúscula (A-Z) y un número mayor o igual a 1.");
                break;
            }

            asientos.Add(new Asiento(pedido.Fila[0], pedido.Numero));
        }

        if (asientos.Distinct().Count() != asientos.Count)
            errores.Add("El pedido no puede repetir asientos.");

        return errores.Count > 0 ? (null, Error(errores.ToArray())) : (asientos, null);
    }

    private static IReadOnlyDictionary<string, string[]> Error(params string[] mensajes) =>
        new Dictionary<string, string[]> { [CampoAsientos] = mensajes };

    private static IReadOnlyList<AsientoPedidoDto> ADtos(IEnumerable<Asiento> asientos) =>
        asientos.Select(a => new AsientoPedidoDto(a.Fila.ToString(), a.Numero)).ToList();
}
