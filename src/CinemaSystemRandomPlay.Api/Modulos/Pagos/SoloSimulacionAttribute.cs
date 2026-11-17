using CinemaSystemRandomPlay.Infrastructure.Pagos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CinemaSystemRandomPlay.Api.Modulos.Pagos;

/// <summary>
/// La acción existe solo en Development y Testing con <c>MercadoPago:Modo=Simulado</c>; en cualquier otro caso responde 404,
/// igual que una ruta inexistente.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SoloSimulacionAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var servicios = context.HttpContext.RequestServices;
        var entorno = servicios.GetRequiredService<IHostEnvironment>();
        var opciones = servicios.GetRequiredService<OpcionesMercadoPago>();

        var habilitado = (entorno.IsDevelopment() || entorno.IsEnvironment("Testing")) && opciones.Modo == ModoMercadoPago.Simulado;

        if (!habilitado)
            context.Result = new NotFoundResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
