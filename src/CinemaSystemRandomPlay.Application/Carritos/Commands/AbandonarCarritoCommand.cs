using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Carritos.Commands;

public record AbandonarCarritoCommand(Guid CarritoId, Guid CarritoDelToken);

public class AbandonarCarritoCommandHandler : OperacionSobreCarritoHandler
{
    private readonly IOutbox _outbox;

    public AbandonarCarritoCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IFuncionRepository funcionRepository,
        ICarritoRepository carritoRepository,
        IConsultaCarritos consultaCarritos,
        IOutbox outbox,
        IReloj reloj)
        : base(unidadDeTrabajo, funcionRepository, carritoRepository, consultaCarritos, reloj)
    {
        _outbox = outbox;
    }

    public Task<ResultadoOperacionCarrito> Handle(AbandonarCarritoCommand command, CancellationToken cancellationToken = default) =>
        Ejecutar(command.CarritoId, command.CarritoDelToken, (_, carrito, ahora, _) =>
        {
            var liberados = carrito.Asientos;
            var abandono = carrito.Abandonar(ahora);

            if (!abandono.Abandonado)
                return Task.FromResult<ResultadoOperacionCarrito?>(ResultadoOperacionCarrito.CarritoNoActivo(abandono.EstadoActual));

            _outbox.Registrar(new CarritoAbandonado(carrito.Id, carrito.FuncionId, liberados, ahora));

            return Task.FromResult<ResultadoOperacionCarrito?>(null);
        }, cancellationToken);
}
