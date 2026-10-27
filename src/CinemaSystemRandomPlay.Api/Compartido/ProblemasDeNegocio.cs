using Microsoft.AspNetCore.Mvc;

namespace CinemaSystemRandomPlay.Api.Compartido;

/// <summary>
/// Respuestas de error de negocio como <c>application/problem+json</c> con una extensión <c>codigo</c> estable.
/// </summary>
public static class ProblemasDeNegocio
{
    public static ObjectResult Crear(int status, string titulo, string codigo, IReadOnlyDictionary<string, object?>? extensiones = null)
    {
        var problema = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = titulo,
            Status = status
        };

        problema.Extensions["codigo"] = codigo;

        if (extensiones is not null)
        {
            foreach (var (clave, valor) in extensiones)
                problema.Extensions[clave] = valor;
        }

        return new ObjectResult(problema)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
