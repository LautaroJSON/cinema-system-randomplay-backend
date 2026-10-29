using CinemaSystemRandomPlay.Api.Autenticacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CinemaSystemRandomPlay.Api.Compartido;

/// <summary>
/// Documento OpenAPI de la API: datos generales, esquemas de autenticación según la política de cada operación y la extensión
/// <c>codigo</c> de los errores de negocio. Lo sirve <c>/openapi/v1.json</c> y se escribe en <c>docs/openapi.json</c> al compilar.
/// </summary>
public static class ConfiguracionOpenApi
{
    public static IServiceCollection AgregarDocumentacionOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(DescribirDocumento);
            options.AddOperationTransformer(DeclararSeguridad);
            options.AddSchemaTransformer(DescribirProblemDetails);
        });

    private static Task DescribirDocumento(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Cinema System RandomPlay API",
            Version = "v1",
            Description =
                "API de venta de entradas de cine. Flujo del comprador: catálogo de películas → sucursales y horarios de hoy → " +
                "mapa de asientos de una función → carrito (retiene los asientos unos minutos) → email de entrega.\n\n" +
                "Los errores de negocio se responden como `application/problem+json` con una extensión `codigo` estable: " +
                "el cliente decide por `codigo`, no por `title`."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[EsquemasDeAutenticacion.Invitado] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token del carrito del comprador anónimo. Lo devuelve una sola vez `POST /api/funciones/{funcionId}/carritos` " +
                          "(campo `token`) y da acceso únicamente a ese carrito."
        };
        document.Components.SecuritySchemes[EsquemaPersonal] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token del proveedor OIDC del personal interno (roles CinemaManager o Admin)."
        };

        return Task.CompletedTask;
    }

    /// <summary>
    /// Refleja la autorización real de cada operación: anónima, portador de carrito o, por la política de fallback, el personal.
    /// </summary>
    private static Task DeclararSeguridad(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<IAllowAnonymous>().Any())
            return Task.CompletedTask;

        var esquema = metadata.OfType<IAuthorizeData>().Any(a => a.Policy == Politicas.PortadorDeCarrito)
            ? EsquemasDeAutenticacion.Invitado
            : EsquemaPersonal;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(esquema, context.Document)] = []
        });

        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd(StatusCodes.Status401Unauthorized.ToString(),
            new OpenApiResponse { Description = "Falta el token o no es válido (vencido, mal firmado o de otro tipo)." });

        return Task.CompletedTask;
    }

    private static Task DescribirProblemDetails(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type != typeof(ProblemDetails))
            return Task.CompletedTask;

        schema.Description =
            "Error en formato RFC 9457. Los errores de negocio agregan `codigo` y, según el caso, `errors` (pedido-invalido: " +
            "campo → mensajes), `asientos` (asientos-inexistentes, asientos-no-disponibles: lista de {fila, numero}) o " +
            "`estadoActual` (carrito-no-activo: Abandonado o Expirado).";
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["codigo"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Código estable del error de negocio, por ejemplo `asientos-no-disponibles`. Ausente en los 404 y 410 de consultas (catálogo, horarios, mapa de asientos)."
        };

        return Task.CompletedTask;
    }

    private const string EsquemaPersonal = "Personal";
}
