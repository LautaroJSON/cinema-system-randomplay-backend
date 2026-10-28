using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Carritos.Commands;

/// <summary>
/// Esqueleto común de las operaciones del portador sobre un carrito existente (reemplazar, abandonar, indicar el email).
/// <list type="number">
/// <item>Un token de otro carrito responde "no encontrado" sin tocar la base, igual que un id inexistente.</item>
/// <item>Toma el bloqueo de la función antes de cargar el carrito, en el mismo orden que la creación (función → carrito), así
/// que todo cambio de ocupación de una función queda serializado y se decide sobre el estado confirmado más reciente.</item>
/// <item>Lee el carrito después de confirmar la transacción.</item>
/// </list>
/// </summary>
public abstract class OperacionSobreCarritoHandler
{
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IFuncionRepository _funcionRepository;
    private readonly IConsultaCarritos _consultaCarritos;

    protected ICarritoRepository CarritoRepository { get; }
    protected IReloj Reloj { get; }

    protected OperacionSobreCarritoHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IFuncionRepository funcionRepository,
        ICarritoRepository carritoRepository,
        IConsultaCarritos consultaCarritos,
        IReloj reloj)
    {
        _unidadDeTrabajo = unidadDeTrabajo;
        _funcionRepository = funcionRepository;
        _consultaCarritos = consultaCarritos;
        CarritoRepository = carritoRepository;
        Reloj = reloj;
    }

    /// <param name="operacion">Aplica el cambio dentro de la transacción. Devuelve <c>null</c> si tuvo éxito o el rechazo.</param>
    protected async Task<ResultadoOperacionCarrito> Ejecutar(
        Guid carritoId,
        Guid carritoDelToken,
        Func<Funcion, Carrito, DateTimeOffset, CancellationToken, Task<ResultadoOperacionCarrito?>> operacion,
        CancellationToken cancellationToken)
    {
        if (carritoId != carritoDelToken)
            return ResultadoOperacionCarrito.CarritoNoEncontrado();

        var rechazo = await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            var funcionId = await CarritoRepository.ObtenerFuncionId(carritoId, ct);
            if (funcionId is null)
                return ResultadoOperacionCarrito.CarritoNoEncontrado();

            var funcion = await _funcionRepository.ObtenerConSalaBloqueando(funcionId.Value, ct);
            var carrito = await CarritoRepository.ObtenerPorId(carritoId, ct);

            if (funcion?.Sala is null || carrito is null)
                return ResultadoOperacionCarrito.CarritoNoEncontrado();

            return await operacion(funcion, carrito, Reloj.Ahora, ct);
        }, cancellationToken);

        if (rechazo is not null)
            return rechazo;

        var lectura = await _consultaCarritos.ObtenerPorId(carritoId, cancellationToken);

        return ResultadoOperacionCarrito.Ok(CarritoDto.DesdeLectura(lectura!, Reloj.Ahora));
    }
}
