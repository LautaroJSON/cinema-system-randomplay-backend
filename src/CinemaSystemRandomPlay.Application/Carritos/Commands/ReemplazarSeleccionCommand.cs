using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Carritos.Commands;

public record ReemplazarSeleccionCommand(Guid CarritoId, Guid CarritoDelToken, IReadOnlyList<AsientoPedidoDto>? Asientos);

/// <summary>Reemplaza la selección completa, todo o nada, sin cambiar el vencimiento.</summary>
public class ReemplazarSeleccionCommandHandler : OperacionSobreCarritoHandler
{
    private readonly IOutbox _outbox;
    private readonly AsignadorDeAsientos _asignador;
    private readonly ReglasCarrito _reglas;

    public ReemplazarSeleccionCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IFuncionRepository funcionRepository,
        ICarritoRepository carritoRepository,
        IConsultaCarritos consultaCarritos,
        IOutbox outbox,
        IReloj reloj,
        AsignadorDeAsientos asignador,
        ReglasCarrito reglas)
        : base(unidadDeTrabajo, funcionRepository, carritoRepository, consultaCarritos, reloj)
    {
        _outbox = outbox;
        _asignador = asignador;
        _reglas = reglas;
    }

    public async Task<ResultadoOperacionCarrito> Handle(ReemplazarSeleccionCommand command, CancellationToken cancellationToken = default)
    {
        if (command.CarritoId != command.CarritoDelToken)
            return ResultadoOperacionCarrito.CarritoNoEncontrado();

        var validacion = ValidacionDeAsientos.Validar(command.Asientos, _reglas.MaximoAsientosPorCarrito);

        if (validacion.Errores is not null)
            return ResultadoOperacionCarrito.PedidoInvalido(validacion.Errores);

        return await Ejecutar(command.CarritoId, command.CarritoDelToken, async (funcion, carrito, ahora, ct) =>
        {
            var activos = await CarritoRepository.ListarActivosDeFuncion(funcion.Id, ahora, ct);
            var seleccion = _asignador.Reemplazar(funcion, carrito, activos, validacion.Asientos!, ahora, _reglas);

            if (seleccion.Tipo != TipoResultadoSeleccion.Ok)
                return ResultadoOperacionCarrito.DesdeRechazo(seleccion);

            _outbox.Registrar(new SeleccionReemplazada(carrito.Id, carrito.FuncionId, seleccion.Liberados!, seleccion.Retenidos!,
                carrito.VenceEn, ahora));

            return null;
        }, cancellationToken);
    }
}
