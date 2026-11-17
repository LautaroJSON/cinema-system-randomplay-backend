using System.Text.Json;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Pagos;
using CinemaSystemRandomPlay.Domain.Pagos.Eventos;

namespace CinemaSystemRandomPlay.Application.Pagos.Manejadores;

/// <summary>
/// Devuelve en la pasarela un pago aprobado que no pudo pagar su carrito (spec 008, FR-019 a FR-021). La llamada va fuera de toda
/// transacción y con el id de la devolución como clave de idempotencia: un reintento nunca devuelve dos veces. Si se agotan los
/// reintentos, la devolución queda <c>Fallida</c> y registrada.
/// </summary>
public class EjecutarDevolucion : IManejadorDeMensaje
{
    private readonly IRegistroDePagos _registroDePagos;
    private readonly IProcesadorDePagos _procesadorDePagos;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public EjecutarDevolucion(IRegistroDePagos registroDePagos, IProcesadorDePagos procesadorDePagos, IUnidadDeTrabajo unidadDeTrabajo,
        IReloj reloj)
    {
        _registroDePagos = registroDePagos;
        _procesadorDePagos = procesadorDePagos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public string Tipo => nameof(DevolucionSolicitada);

    public async Task Manejar(string payload, CancellationToken cancellationToken)
    {
        var devolucionId = LeerDevolucionId(payload);

        var devolucion = await _registroDePagos.ObtenerDevolucion(devolucionId, cancellationToken);
        if (devolucion is null || devolucion.Estado != EstadoDevolucion.Pendiente)
            return;

        await _procesadorDePagos.Devolver(devolucion.PagoId, devolucion.Id, cancellationToken);

        await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            var registrada = await _registroDePagos.ObtenerDevolucion(devolucionId, ct);
            registrada?.Completar(_reloj.Ahora);
            return true;
        }, cancellationToken);
    }

    public async Task AlAgotarIntentos(string payload, string error, CancellationToken cancellationToken)
    {
        var devolucionId = LeerDevolucionId(payload);

        await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            var devolucion = await _registroDePagos.ObtenerDevolucion(devolucionId, ct);
            devolucion?.Fallar(error, _reloj.Ahora);
            return true;
        }, cancellationToken);
    }

    private static Guid LeerDevolucionId(string payload) =>
        JsonSerializer.Deserialize<ReferenciaADevolucion>(payload)?.DevolucionId
        ?? throw new InvalidOperationException("El mensaje DevolucionSolicitada no tiene devolución.");

    private sealed record ReferenciaADevolucion(Guid DevolucionId);
}
