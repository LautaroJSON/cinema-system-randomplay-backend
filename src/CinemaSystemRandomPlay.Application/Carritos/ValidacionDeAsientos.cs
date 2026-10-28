using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Application.Carritos;

/// <summary>Validación de forma de una selección, común a crear un carrito y reemplazar su selección.</summary>
internal static class ValidacionDeAsientos
{
    private const string CampoAsientos = "asientos";

    public static (IReadOnlyList<Asiento>? Asientos, IReadOnlyDictionary<string, string[]>? Errores) Validar(
        IReadOnlyList<AsientoPedidoDto>? pedidos, int maximoAsientos)
    {
        var errores = new List<string>();

        if (pedidos is null || pedidos.Count == 0)
            return (null, Error("Debe indicar al menos un asiento."));

        if (pedidos.Count > maximoAsientos)
            errores.Add($"Un carrito admite como máximo {maximoAsientos} asientos.");

        var asientos = new List<Asiento>();

        foreach (var pedido in pedidos)
        {
            if (pedido is null
                || string.IsNullOrEmpty(pedido.Fila)
                || pedido.Fila.Length != 1
                || pedido.Fila[0] is < 'A' or > 'Z'
                || pedido.Numero < 1)
            {
                errores.Add("Cada asiento debe tener una fila de una letra mayúscula (A-Z) y un número mayor o igual a 1.");
                break;
            }

            asientos.Add(new Asiento(pedido.Fila[0], pedido.Numero));
        }

        if (asientos.Distinct().Count() != asientos.Count)
            errores.Add("El pedido no puede repetir asientos.");

        return errores.Count > 0 ? (null, Error(errores.ToArray())) : (asientos, null);
    }

    public static IReadOnlyDictionary<string, string[]> ErrorEn(string campo, params string[] mensajes) =>
        new Dictionary<string, string[]> { [campo] = mensajes };

    public static IReadOnlyList<AsientoPedidoDto> ADtos(IEnumerable<Asiento> asientos) =>
        asientos.Select(a => new AsientoPedidoDto(a.Fila.ToString(), a.Numero)).ToList();

    private static IReadOnlyDictionary<string, string[]> Error(params string[] mensajes) => ErrorEn(CampoAsientos, mensajes);
}
