using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Carritos.Eventos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Carritos.Commands;

public record IniciarPagoCommand(Guid CarritoId, Guid CarritoDelToken);

/// <summary>
/// Inicia el pago del carrito en la pasarela (specs/008-pago-mercado-pago/research.md, Decisión 4):
/// <list type="number">
/// <item>Lee el carrito sin bloqueo. Si ya está en pago, devuelve su URL sin llamar a la pasarela.</item>
/// <item>Crea el pago en la pasarela <b>fuera de toda transacción</b>, con el vencimiento extendido.</item>
/// <item>En una transacción corta, con el bloqueo de la función, el carrito pasa a <c>Pagando</c> si sigue activo y con la misma
/// selección que se envió a la pasarela.</item>
/// </list>
/// </summary>
public class IniciarPagoCommandHandler
{
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IFuncionRepository _funcionRepository;
    private readonly ICarritoRepository _carritoRepository;
    private readonly IConsultaCarritos _consultaCarritos;
    private readonly IProcesadorDePagos _procesadorDePagos;
    private readonly IOutbox _outbox;
    private readonly IReloj _reloj;

    public IniciarPagoCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IFuncionRepository funcionRepository,
        ICarritoRepository carritoRepository,
        IConsultaCarritos consultaCarritos,
        IProcesadorDePagos procesadorDePagos,
        IOutbox outbox,
        IReloj reloj)
    {
        _unidadDeTrabajo = unidadDeTrabajo;
        _funcionRepository = funcionRepository;
        _carritoRepository = carritoRepository;
        _consultaCarritos = consultaCarritos;
        _procesadorDePagos = procesadorDePagos;
        _outbox = outbox;
        _reloj = reloj;
    }

    public async Task<ResultadoOperacionCarrito> Handle(IniciarPagoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.CarritoId != command.CarritoDelToken)
            return ResultadoOperacionCarrito.CarritoNoEncontrado();

        var lectura = await _consultaCarritos.ObtenerPorId(command.CarritoId, cancellationToken);
        if (lectura is null)
            return ResultadoOperacionCarrito.CarritoNoEncontrado();

        var ahora = _reloj.Ahora;
        var estado = Carrito.EstadoEn(lectura.Estado, lectura.VenceEn, ahora);

        if (estado == EstadoCarrito.Pagando)
            return ResultadoOperacionCarrito.PagoIniciado(CarritoDto.DesdeLectura(lectura, ahora), lectura.UrlPago!);

        if (estado != EstadoCarrito.Activo)
            return ResultadoOperacionCarrito.CarritoNoActivo(estado);

        if (lectura.EmailEntrega is null)
            return ResultadoOperacionCarrito.EmailEntregaFaltante();

        PagoCreado pagoCreado;
        try
        {
            pagoCreado = await _procesadorDePagos.CrearPago(SolicitudPara(lectura, ahora), cancellationToken);
        }
        catch (ProcesadorDePagosNoDisponibleException)
        {
            return ResultadoOperacionCarrito.PagoNoDisponible();
        }

        var (rechazo, urlPago) = await _unidadDeTrabajo.EjecutarEnTransaccion<(ResultadoOperacionCarrito? Rechazo, string? UrlPago)>(async ct =>
        {
            var funcion = await _funcionRepository.ObtenerConSalaBloqueando(lectura.FuncionId, ct);
            var carrito = await _carritoRepository.ObtenerPorId(command.CarritoId, ct);

            if (funcion is null || carrito is null)
                return (ResultadoOperacionCarrito.CarritoNoEncontrado(), null);

            var momento = _reloj.Ahora;
            var resultado = carrito.IniciarPago(pagoCreado.PreferenciaId, pagoCreado.UrlPago, lectura.Asientos, momento,
                funcion.FechaHoraInicio);

            switch (resultado.Tipo)
            {
                case TipoResultadoInicioPago.Iniciado:
                    _outbox.Registrar(new PagoIniciado(carrito.Id, carrito.FuncionId, carrito.VenceEn, momento));
                    return (null, resultado.UrlPago);
                case TipoResultadoInicioPago.YaEnPago:
                    // Otro pedido ganó la carrera: la preferencia recién creada queda huérfana y vence sola.
                    return (null, resultado.UrlPago);
                case TipoResultadoInicioPago.NoActivo:
                    return (ResultadoOperacionCarrito.CarritoNoActivo(resultado.EstadoActual!.Value), null);
                case TipoResultadoInicioPago.SinEmailEntrega:
                    return (ResultadoOperacionCarrito.EmailEntregaFaltante(), null);
                default:
                    return (ResultadoOperacionCarrito.CarritoModificado(), null);
            }
        }, cancellationToken);

        if (rechazo is not null)
            return rechazo;

        var actualizado = await _consultaCarritos.ObtenerPorId(command.CarritoId, cancellationToken);

        return ResultadoOperacionCarrito.PagoIniciado(CarritoDto.DesdeLectura(actualizado!, _reloj.Ahora), urlPago!);
    }

    private static SolicitudDePago SolicitudPara(CarritoLecturaDto lectura, DateTimeOffset ahora)
    {
        var items = lectura.Asientos
            .OrderBy(a => a.Fila).ThenBy(a => a.Numero)
            .Select(a => new ItemDePago($"{lectura.PeliculaTitulo} — {lectura.SalaNombre} — Fila {a.Fila} Asiento {a.Numero}",
                lectura.PrecioUnitario.Monto))
            .ToList();

        return new SolicitudDePago(
            lectura.Id,
            items,
            lectura.PrecioUnitario.Multiplicar(lectura.Asientos.Count),
            ahora,
            Carrito.VencimientoAlIniciarPago(lectura.VenceEn, lectura.HoraInicio),
            lectura.EmailEntrega!);
    }
}
