using CinemaSystemRandomPlay.Api.Modulos.Pagos.Contracts;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Pagos.Commands;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Pagos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Modulos.Pagos;

/// <summary>
/// Simulación de pagos para desarrollo sin túnel y para pruebas (specs/008-pago-mercado-pago/research.md, Decisión 12). Solo existe
/// en Development y Testing con el procesador simulado; fuera de eso responde 404. Público de forma explícita: no hay dinero ni
/// credenciales en juego.
/// </summary>
[ApiController]
[AllowAnonymous]
[SoloSimulacion]
[ApiExplorerSettings(IgnoreApi = true)]
public class PagosSimuladosController : ControllerBase
{
    private readonly ProcesadorDePagosSimulado _simulador;
    private readonly IConsultaCarritos _consultaCarritos;
    private readonly ProcesarPagoCommandHandler _procesarPagoCommandHandler;
    private readonly IReloj _reloj;

    public PagosSimuladosController(
        ProcesadorDePagosSimulado simulador,
        IConsultaCarritos consultaCarritos,
        ProcesarPagoCommandHandler procesarPagoCommandHandler,
        IReloj reloj)
    {
        _simulador = simulador;
        _consultaCarritos = consultaCarritos;
        _procesarPagoCommandHandler = procesarPagoCommandHandler;
        _reloj = reloj;
    }

    /// <summary>Simula que el comprador pagó en la página de pago y procesa el pago igual que si hubiera llegado el aviso.</summary>
    [HttpPost("api/dev/pagos-simulados")]
    public async Task<IActionResult> Simular([FromBody] PagoSimuladoRequest request, CancellationToken cancellationToken)
    {
        var carrito = await _consultaCarritos.ObtenerPorId(request.CarritoId, cancellationToken);
        if (carrito is null)
            return NotFound();

        var total = carrito.PrecioUnitario.Multiplicar(carrito.Asientos.Count);
        var estado = request.Estado switch
        {
            EstadoPagoSimulado.Aprobado => EstadoPagoExterno.Aprobado,
            EstadoPagoSimulado.Rechazado => EstadoPagoExterno.Rechazado,
            _ => EstadoPagoExterno.Pendiente
        };

        var pagoId = _simulador.RegistrarPago(request.CarritoId, estado,
            request.Monto is { } monto ? Dinero.Ars(monto) : total, request.AprobadoEn ?? _reloj.Ahora);

        await _procesarPagoCommandHandler.Handle(new ProcesarPagoCommand(pagoId), cancellationToken);

        var actualizado = await _consultaCarritos.ObtenerPorId(request.CarritoId, cancellationToken);

        return Ok(new PagoSimuladoResponse(pagoId, CarritoDto.DesdeLectura(actualizado!, _reloj.Ahora).Estado.ToString()));
    }

    /// <summary>Página a la que apunta la URL de pago simulada.</summary>
    [HttpGet("api/dev/pagos-simulados/{preferenciaId}")]
    public IActionResult Pagina(string preferenciaId)
    {
        if (!_simulador.Solicitudes.TryGetValue(preferenciaId, out var solicitud))
            return NotFound();

        return Content(
            $"Pago simulado del carrito {solicitud.CarritoId} por {solicitud.Total.Monto:0.00} {solicitud.Total.Moneda}, " +
            $"válido hasta {solicitud.VenceEn:O}.\n\n" +
            "Para simular el resultado: POST /api/dev/pagos-simulados con " +
            $"{{\"carritoId\":\"{solicitud.CarritoId}\",\"estado\":\"Aprobado\"}} (o \"Rechazado\", \"Pendiente\"; " +
            "opcionalmente \"monto\" y \"aprobadoEn\").\n",
            "text/plain; charset=utf-8");
    }
}
