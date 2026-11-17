using System.Text.Json.Serialization;

namespace CinemaSystemRandomPlay.Api.Modulos.Pagos.Contracts;

/// <summary>
/// Cuerpo del webhook de Mercado Pago. Solo se lee el tipo (si no vino en el query) y el id (si no vino en el query); el resto no se
/// usa para decidir nada.
/// </summary>
public record AvisoMercadoPagoRequest(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("data")] AvisoMercadoPagoData? Data);

public record AvisoMercadoPagoData([property: JsonPropertyName("id")] string? Id);
