using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Carritos.Commands;

public record IndicarEmailEntregaCommand(Guid CarritoId, Guid CarritoDelToken, string? Email);

/// <summary>Guarda el email al que se enviará la entrada. No lo verifica, no envía nada y no registra eventos.</summary>
public class IndicarEmailEntregaCommandHandler : OperacionSobreCarritoHandler
{
    public IndicarEmailEntregaCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IFuncionRepository funcionRepository,
        ICarritoRepository carritoRepository,
        IConsultaCarritos consultaCarritos,
        IReloj reloj)
        : base(unidadDeTrabajo, funcionRepository, carritoRepository, consultaCarritos, reloj)
    {
    }

    public async Task<ResultadoOperacionCarrito> Handle(IndicarEmailEntregaCommand command, CancellationToken cancellationToken = default)
    {
        if (command.CarritoId != command.CarritoDelToken)
            return ResultadoOperacionCarrito.CarritoNoEncontrado();

        if (!EmailComprador.TryCrear(command.Email, out var email))
            return ResultadoOperacionCarrito.PedidoInvalido(ValidacionDeAsientos.ErrorEn("email", "El email es inválido."));

        return await Ejecutar(command.CarritoId, command.CarritoDelToken, (_, carrito, ahora, _) =>
        {
            var resultado = carrito.IndicarEmailEntrega(email!, ahora);

            return Task.FromResult(resultado.Indicado ? null : ResultadoOperacionCarrito.CarritoNoActivo(resultado.EstadoActual));
        }, cancellationToken);
    }
}
