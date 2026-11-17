using System.Globalization;
using System.Net;
using System.Text;
using CinemaSystemRandomPlay.Application.Carritos.Dtos;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Application.Pagos;

/// <summary>Arma el email con las entradas de un carrito pagado: una línea por entrada, en texto plano y HTML.</summary>
public static class MensajeDeEntrega
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    public static MensajeDeEmail Crear(CarritoDto carrito)
    {
        if (carrito.EmailEntrega is null)
            throw new InvalidOperationException("El carrito no tiene email de entrega.");

        var funcion = carrito.Funcion;
        // Hora del cine: la zona local del sistema, la misma convención que usa la consulta de funciones de hoy.
        var horario = TimeZoneInfo.ConvertTime(funcion.HoraInicio, TimeZoneInfo.Local).ToString("dddd d 'de' MMMM, HH:mm", Cultura);
        var asunto = $"Tus entradas para {funcion.PeliculaTitulo} — {horario}";

        var texto = new StringBuilder()
            .AppendLine($"¡Gracias por tu compra! Estas son tus entradas para {funcion.PeliculaTitulo}.")
            .AppendLine()
            .AppendLine($"Sucursal: {funcion.SucursalNombre}")
            .AppendLine($"Sala: {funcion.SalaNombre}")
            .AppendLine($"Función: {horario}")
            .AppendLine();

        foreach (var entrada in carrito.Entradas)
            texto.AppendLine($"- Entrada {entrada.Codigo}: fila {entrada.Asiento.Fila}, asiento {entrada.Asiento.Numero}, {Precio(entrada.PrecioUnitario)}");

        texto.AppendLine()
            .AppendLine($"Total: {Precio(carrito.Total)}")
            .AppendLine()
            .AppendLine("Presentá el código de cada entrada en el ingreso a la sala.");

        var html = new StringBuilder()
            .Append("<h1>Tus entradas para ").Append(H(funcion.PeliculaTitulo)).Append("</h1>")
            .Append("<p>").Append(H(funcion.SucursalNombre)).Append(" · ").Append(H(funcion.SalaNombre)).Append(" · ").Append(H(horario))
            .Append("</p><table border=\"1\" cellpadding=\"6\" cellspacing=\"0\"><tr><th>Código</th><th>Fila</th><th>Asiento</th><th>Precio</th></tr>");

        foreach (var entrada in carrito.Entradas)
        {
            html.Append("<tr><td><strong>").Append(H(entrada.Codigo)).Append("</strong></td><td>").Append(H(entrada.Asiento.Fila))
                .Append("</td><td>").Append(entrada.Asiento.Numero).Append("</td><td>").Append(H(Precio(entrada.PrecioUnitario)))
                .Append("</td></tr>");
        }

        html.Append("</table><p>Total: ").Append(H(Precio(carrito.Total))).Append("</p>")
            .Append("<p>Presentá el código de cada entrada en el ingreso a la sala.</p>");

        return new MensajeDeEmail(EmailComprador.Crear(carrito.EmailEntrega), asunto, texto.ToString(), html.ToString());
    }

    private static string Precio(Compartido.Dtos.DineroDto dinero) =>
        $"{dinero.Moneda} {dinero.Monto.ToString("N2", Cultura)}";

    private static string H(string texto) => WebUtility.HtmlEncode(texto);
}
