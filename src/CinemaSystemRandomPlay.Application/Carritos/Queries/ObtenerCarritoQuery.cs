using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Commands;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Application.Carritos.Queries;

/// <param name="CarritoDelToken">Id del carrito al que da acceso el token del pedido.</param>
public record ObtenerCarritoQuery(Guid CarritoId, Guid CarritoDelToken);

/// <param name="Timeout">Tiempo máximo que la consulta del carrito espera a la pasarela antes de responder el estado conocido.</param>
public record OpcionesVerificacionDePagos(TimeSpan Timeout);

/// <summary>
/// Devuelve <c>null</c> si el carrito no existe o el token es de otro carrito, sin distinguir los dos casos. Si el carrito está en
/// pago (vigente o vencido), primero pregunta a la pasarela por pagos aprobados que todavía no se procesaron y los procesa igual
/// que un aviso (spec 008, FR-017b): así el comprador que vuelve de la página de pago ve el resultado aunque el aviso se demore.
/// Si la pasarela no responde a tiempo, responde el estado conocido.
/// </summary>
public class ObtenerCarritoQueryHandler
{
    private readonly IConsultaCarritos _consultaCarritos;
    private readonly IReloj _reloj;
    private readonly IProcesadorDePagos _procesadorDePagos;
    private readonly IRegistroDePagos _registroDePagos;
    private readonly ProcesarPagoCommandHandler _procesarPago;
    private readonly OpcionesVerificacionDePagos _opciones;

    public ObtenerCarritoQueryHandler(
        IConsultaCarritos consultaCarritos,
        IReloj reloj,
        IProcesadorDePagos procesadorDePagos,
        IRegistroDePagos registroDePagos,
        ProcesarPagoCommandHandler procesarPago,
        OpcionesVerificacionDePagos opciones)
    {
        _consultaCarritos = consultaCarritos;
        _reloj = reloj;
        _procesadorDePagos = procesadorDePagos;
        _registroDePagos = registroDePagos;
        _procesarPago = procesarPago;
        _opciones = opciones;
    }

    public async Task<CarritoDto?> Handle(ObtenerCarritoQuery query, CancellationToken cancellationToken = default)
    {
        if (query.CarritoId != query.CarritoDelToken)
            return null;

        var lectura = await _consultaCarritos.ObtenerPorId(query.CarritoId, cancellationToken);
        if (lectura is null)
            return null;

        if (lectura.Estado == EstadoCarrito.Pagando && await VerificarPagos(query.CarritoId, cancellationToken))
            lectura = await _consultaCarritos.ObtenerPorId(query.CarritoId, cancellationToken) ?? lectura;

        return CarritoDto.DesdeLectura(lectura, _reloj.Ahora);
    }

    /// <returns><c>true</c> si se procesó algún pago.</returns>
    private async Task<bool> VerificarPagos(Guid carritoId, CancellationToken cancellationToken)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(_opciones.Timeout);

        IReadOnlyList<PagoConsultado> pagos;
        try
        {
            pagos = await _procesadorDePagos.BuscarPagosDelCarrito(carritoId, limite.Token);
        }
        catch (ProcesadorDePagosNoDisponibleException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var procesado = false;

        foreach (var pago in pagos.Where(p => p.Estado == EstadoPagoExterno.Aprobado))
        {
            if (await _registroDePagos.YaProcesado(pago.Id, cancellationToken))
                continue;

            // El pago ya viene consultado a la pasarela: se procesa sin volver a pedirlo y sin el límite de tiempo, para no cortar
            // una transacción a mitad.
            await _procesarPago.Procesar(pago, cancellationToken);
            procesado = true;
        }

        return procesado;
    }
}
