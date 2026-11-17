using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Pagos;
using CinemaSystemRandomPlay.Domain.Pagos.Eventos;

namespace CinemaSystemRandomPlay.Application.Pagos.Commands;

public record ProcesarPagoCommand(string PagoId);

public enum ResultadoProcesamientoPago
{
    /// <summary>El pago no existe o no referencia un carrito de este sistema.</summary>
    Ignorado,

    /// <summary>Rechazado, pendiente u otro estado: no tiene efecto.</summary>
    NoAprobado,

    /// <summary>Ya tuvo efecto antes (aviso duplicado o concurrente).</summary>
    YaProcesado,

    Acreditado,
    Devuelto
}

/// <summary>
/// Aplica un pago al carrito (specs/008-pago-mercado-pago/research.md, Decisión 6). Lo usan el aviso de la pasarela, la consulta
/// del carrito y el simulador. Consulta el pago a la pasarela fuera de la transacción: nunca decide con datos de un aviso. Dentro
/// de la transacción, con el bloqueo de la función, el registro de pagos procesados hace que cada pago tenga efecto una sola vez.
/// </summary>
public class ProcesarPagoCommandHandler
{
    private readonly IProcesadorDePagos _procesadorDePagos;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IFuncionRepository _funcionRepository;
    private readonly ICarritoRepository _carritoRepository;
    private readonly IConsultaCarritos _consultaCarritos;
    private readonly IRegistroDePagos _registroDePagos;
    private readonly ConfirmadorDePagos _confirmador;
    private readonly IGeneradorDeCodigos _generadorDeCodigos;
    private readonly IOutbox _outbox;
    private readonly IReloj _reloj;

    public ProcesarPagoCommandHandler(
        IProcesadorDePagos procesadorDePagos,
        IUnidadDeTrabajo unidadDeTrabajo,
        IFuncionRepository funcionRepository,
        ICarritoRepository carritoRepository,
        IConsultaCarritos consultaCarritos,
        IRegistroDePagos registroDePagos,
        ConfirmadorDePagos confirmador,
        IGeneradorDeCodigos generadorDeCodigos,
        IOutbox outbox,
        IReloj reloj)
    {
        _procesadorDePagos = procesadorDePagos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _funcionRepository = funcionRepository;
        _carritoRepository = carritoRepository;
        _consultaCarritos = consultaCarritos;
        _registroDePagos = registroDePagos;
        _confirmador = confirmador;
        _generadorDeCodigos = generadorDeCodigos;
        _outbox = outbox;
        _reloj = reloj;
    }

    /// <exception cref="ProcesadorDePagosNoDisponibleException">La pasarela no respondió; se puede reintentar.</exception>
    public async Task<ResultadoProcesamientoPago> Handle(ProcesarPagoCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.PagoId))
            return ResultadoProcesamientoPago.Ignorado;

        var pago = await _procesadorDePagos.ObtenerPago(command.PagoId, cancellationToken);

        return pago is null ? ResultadoProcesamientoPago.Ignorado : await Procesar(pago, cancellationToken);
    }

    /// <summary>Procesa un pago ya consultado a la pasarela (nunca uno armado con datos de un aviso).</summary>
    public async Task<ResultadoProcesamientoPago> Procesar(PagoConsultado pago, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(pago.ReferenciaExterna, out var carritoId))
            return ResultadoProcesamientoPago.Ignorado;

        if (pago.Estado != EstadoPagoExterno.Aprobado || pago.AprobadoEn is null)
            return ResultadoProcesamientoPago.NoAprobado;

        var lectura = await _consultaCarritos.ObtenerPorId(carritoId, cancellationToken);
        if (lectura is null)
            return ResultadoProcesamientoPago.Ignorado;

        var aprobado = new PagoAprobado(pago.Id, pago.Monto, pago.AprobadoEn.Value);
        var datosDeLaFuncion = new DatosDeLaFuncion(lectura.FuncionId, lectura.PeliculaTitulo, lectura.SucursalNombre,
            lectura.SalaNombre, lectura.HoraInicio);

        return await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            await _funcionRepository.ObtenerConSalaBloqueando(lectura.FuncionId, ct);

            if (await _registroDePagos.YaProcesado(pago.Id, ct))
                return ResultadoProcesamientoPago.YaProcesado;

            var carrito = await _carritoRepository.ObtenerPorId(carritoId, ct);
            if (carrito is null)
                return ResultadoProcesamientoPago.Ignorado;

            var ahora = _reloj.Ahora;
            var otros = await _carritoRepository.ListarQueRetienenAsientos(lectura.FuncionId, ahora, ct);
            var decision = _confirmador.Decidir(carrito, otros, aprobado, ahora);

            if (decision.Acreditar)
            {
                carrito.MarcarPagado(aprobado, ahora, datosDeLaFuncion, _generadorDeCodigos);
                _outbox.Registrar(new CarritoPagado(carrito.Id, carrito.FuncionId, aprobado.PagoId, carrito.Asientos, ahora));
                await _registroDePagos.Registrar(new PagoProcesado(aprobado, carritoId, ResultadoPagoProcesado.Acreditado, ahora), ct);
                return ResultadoProcesamientoPago.Acreditado;
            }

            var devolucion = new Devolucion(Guid.NewGuid(), aprobado, carritoId, decision.Motivo!.Value, ahora);
            await _registroDePagos.AgregarDevolucion(devolucion, ct);
            _outbox.Registrar(new DevolucionSolicitada(devolucion.Id, aprobado.PagoId, carritoId, devolucion.Motivo, ahora));
            await _registroDePagos.Registrar(new PagoProcesado(aprobado, carritoId, ResultadoPagoProcesado.Devuelto, ahora), ct);
            return ResultadoProcesamientoPago.Devuelto;
        }, cancellationToken);
    }
}
