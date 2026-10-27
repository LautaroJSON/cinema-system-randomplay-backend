using CinemaSystemRandomPlay.Application.Reservas.Dtos;

namespace CinemaSystemRandomPlay.Api.Modulos.Reservas.Contracts;

public record ReservaResponse(
    Guid Id,
    string Estado,
    DateTimeOffset CreadaEn,
    DateTimeOffset VenceEn,
    DateTimeOffset? CanceladaEn,
    IReadOnlyList<AsientoResponse> Asientos,
    FuncionReservaResponse Funcion)
{
    public static ReservaResponse DesdeDto(ReservaDto dto) =>
        new(dto.Id, dto.Estado.ToString(), dto.CreadaEn, dto.VenceEn, dto.CanceladaEn,
            dto.Asientos.Select(AsientoResponse.DesdeDto).ToList(), FuncionReservaResponse.DesdeDto(dto.Funcion));
}

public record AsientoResponse(string Fila, int Numero)
{
    public static AsientoResponse DesdeDto(AsientoPedidoDto dto) => new(dto.Fila, dto.Numero);
}

public record FuncionReservaResponse(
    Guid FuncionId,
    DateTimeOffset HoraInicio,
    Guid PeliculaId,
    string PeliculaTitulo,
    Guid SucursalId,
    string SucursalNombre,
    Guid SalaId,
    string SalaNombre)
{
    public static FuncionReservaResponse DesdeDto(FuncionReservaDto dto) =>
        new(dto.FuncionId, dto.HoraInicio, dto.PeliculaId, dto.PeliculaTitulo, dto.SucursalId, dto.SucursalNombre, dto.SalaId, dto.SalaNombre);
}
