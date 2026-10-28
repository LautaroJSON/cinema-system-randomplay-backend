using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Carritos.Commands;

/// <param name="Desafio">Respuesta del cliente al desafío anti-bots, si la envió.</param>
/// <param name="Origen">Dirección de red del pedido, para el desafío.</param>
public record CrearCarritoCommand(Guid FuncionId, IReadOnlyList<AsientoPedidoDto>? Asientos, string? Desafio = null, string? Origen = null);

public class CrearCarritoCommandHandler
{
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IFuncionRepository _funcionRepository;
    private readonly IPeliculaRepository _peliculaRepository;
    private readonly ICarritoRepository _carritoRepository;
    private readonly IConsultaCarritos _consultaCarritos;
    private readonly IEmisorDeTokenDeCarrito _emisorDeToken;
    private readonly IDesafioAntiBots _desafio;
    private readonly IOutbox _outbox;
    private readonly IReloj _reloj;
    private readonly AsignadorDeAsientos _asignador;
    private readonly ReglasCarrito _reglas;

    public CrearCarritoCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IFuncionRepository funcionRepository,
        IPeliculaRepository peliculaRepository,
        ICarritoRepository carritoRepository,
        IConsultaCarritos consultaCarritos,
        IEmisorDeTokenDeCarrito emisorDeToken,
        IDesafioAntiBots desafio,
        IOutbox outbox,
        IReloj reloj,
        AsignadorDeAsientos asignador,
        ReglasCarrito reglas)
    {
        _unidadDeTrabajo = unidadDeTrabajo;
        _funcionRepository = funcionRepository;
        _peliculaRepository = peliculaRepository;
        _carritoRepository = carritoRepository;
        _consultaCarritos = consultaCarritos;
        _emisorDeToken = emisorDeToken;
        _desafio = desafio;
        _outbox = outbox;
        _reloj = reloj;
        _asignador = asignador;
        _reglas = reglas;
    }

    public async Task<ResultadoOperacionCarrito> Handle(CrearCarritoCommand command, CancellationToken cancellationToken = default)
    {
        var validacion = ValidacionDeAsientos.Validar(command.Asientos, _reglas.MaximoAsientosPorCarrito);

        if (validacion.Errores is not null)
            return ResultadoOperacionCarrito.PedidoInvalido(validacion.Errores);

        // El desafío puede ser una llamada de red: va antes de abrir la transacción (Principio VI).
        if (!await _desafio.Verificar(command.Desafio, command.Origen, cancellationToken))
            return ResultadoOperacionCarrito.DesafioRechazado();

        var pedidos = validacion.Asientos!;

        var resultado = await _unidadDeTrabajo.EjecutarEnTransaccion<(ResultadoOperacionCarrito? Rechazo, Guid CarritoId)>(async ct =>
        {
            var funcion = await _funcionRepository.ObtenerConSalaBloqueando(command.FuncionId, ct);

            if (funcion?.Sala is null)
                return (ResultadoOperacionCarrito.FuncionNoEncontrada(), Guid.Empty);

            var pelicula = await _peliculaRepository.ObtenerPorId(funcion.PeliculaId, ct);

            if (pelicula is null || !pelicula.Activa)
                return (ResultadoOperacionCarrito.FuncionNoEncontrada(), Guid.Empty);

            var ahora = _reloj.Ahora;
            var activos = await _carritoRepository.ListarActivosDeFuncion(funcion.Id, ahora, ct);
            var seleccion = _asignador.Crear(funcion, activos, pedidos, ahora, _reglas);

            if (seleccion.Tipo != TipoResultadoSeleccion.Ok)
                return (ResultadoOperacionCarrito.DesdeRechazo(seleccion), Guid.Empty);

            var carrito = seleccion.Carrito!;

            await _carritoRepository.Agregar(carrito, ct);
            _outbox.Registrar(new CarritoCreado(carrito.Id, carrito.FuncionId, carrito.Asientos, carrito.VenceEn, ahora,
                carrito.PrecioUnitario, carrito.Total));

            return (null, carrito.Id);
        }, cancellationToken);

        if (resultado.Rechazo is not null)
            return resultado.Rechazo;

        // Lectura y token después de confirmar: el carrito ya existe para cualquier otro pedido.
        var ahoraConfirmado = _reloj.Ahora;
        var lectura = await _consultaCarritos.ObtenerPorId(resultado.CarritoId, cancellationToken);

        return ResultadoOperacionCarrito.Ok(CarritoDto.DesdeLectura(lectura!, ahoraConfirmado), _emisorDeToken.Emitir(resultado.CarritoId, ahoraConfirmado));
    }
}
