using CinemaSystemRandomPlay.Domain.Pagos;

namespace CinemaSystemRandomPlay.Application.Pagos.Ports;

/// <summary>Pagos procesados y devoluciones. Los cambios se guardan con la unidad de trabajo en curso.</summary>
public interface IRegistroDePagos
{
    Task<bool> YaProcesado(string pagoId, CancellationToken cancellationToken = default);

    Task Registrar(PagoProcesado pago, CancellationToken cancellationToken = default);

    Task AgregarDevolucion(Devolucion devolucion, CancellationToken cancellationToken = default);

    Task<Devolucion?> ObtenerDevolucion(Guid devolucionId, CancellationToken cancellationToken = default);
}
