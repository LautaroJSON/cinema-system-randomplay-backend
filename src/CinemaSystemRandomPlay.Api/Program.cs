using CinemaSystemRandomPlay.Api.Autenticacion;
using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Api.Modulos.Carritos;
using CinemaSystemRandomPlay.Application.Carritos.Commands;
using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Carritos.Queries;
using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Catalogo.Queries;
using CinemaSystemRandomPlay.Application.Funciones.Ports;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Carritos;
using CinemaSystemRandomPlay.Infrastructure.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Carritos;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Invitados;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context => ProblemasDeNegocio.Crear(
            StatusCodes.Status400BadRequest,
            "El pedido es inválido.",
            "pedido-invalido",
            new Dictionary<string, object?>
            {
                ["errors"] = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray())
            });
    });

// Token del carrito del comprador anónimo: JWT firmado por esta API (specs/007-carrito-anonimo/research.md, Decisión 1). Las
// opciones se enlazan de forma diferida y se validan al arrancar: sin una ClaveFirma de al menos 32 bytes, la API no inicia.
var opcionesInvitados = builder.Services.AddOptions<OpcionesInvitados>()
    .Bind(builder.Configuration.GetSection(OpcionesInvitados.Seccion))
    .Validate(
        o => Encoding.UTF8.GetByteCount(o.ClaveFirma) >= OpcionesInvitados.LargoMinimoClave,
        $"Falta {OpcionesInvitados.Seccion}:ClaveFirma o mide menos de {OpcionesInvitados.LargoMinimoClave} bytes. " +
        "Configurarla con user-secrets o una variable de entorno (ver specs/007-carrito-anonimo/quickstart.md).");
// El generador de docs/openapi.json (GetDocument.Insider) arranca la app al compilar, sin secretos ni entorno: solo ahí no se
// valida al arrancar, porque nunca atiende pedidos.
if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
    opcionesInvitados.ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcionesInvitados>>().Value);
builder.Services.AddOptions<OpcionesCarritos>().Bind(builder.Configuration.GetSection(OpcionesCarritos.Seccion));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcionesCarritos>>().Value);

// Autenticación: dos esquemas separados. "Bearer" (por defecto) es el proveedor OIDC del personal; Authority/audiencias
// vienen de Authentication:Schemes:Bearer. "Invitado" valida solo tokens de carrito emitidos por esta API.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "sub";
        options.TokenValidationParameters.RoleClaimType = builder.Configuration["Autenticacion:ClaimRoles"] ?? "roles";
    })
    .AddJwtBearer(EsquemasDeAutenticacion.Invitado, options => options.MapInboundClaims = false);

builder.Services.AddOptions<JwtBearerOptions>(EsquemasDeAutenticacion.Invitado)
    .Configure<OpcionesInvitados>((options, invitados) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = OpcionesInvitados.Emisor,
            ValidateAudience = true,
            ValidAudience = OpcionesInvitados.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(invitados.ClaveFirma)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };

        // Un token con nuestra firma pero sin el tipo esperado o sin un carrito válido no es un token de carrito: se rechaza
        // como no autenticado (401), no como no autorizado.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = contexto =>
            {
                var tipo = contexto.Principal?.FindFirst("tipo")?.Value;
                var carrito = contexto.Principal?.FindFirst(OpcionesInvitados.ClaimCarrito)?.Value;

                if (tipo != OpcionesInvitados.TipoToken || !Guid.TryParse(carrito, out _))
                    contexto.Fail("El token no es de un carrito.");

                return Task.CompletedTask;
            }
        };
    });

// Modalidades de acceso: toda operación declara la suya. Sin declarar, el fallback exige usuario autenticado: cerrado (FR-020).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Politicas.PortadorDeCarrito, policy => policy
        .AddAuthenticationSchemes(EsquemasDeAutenticacion.Invitado)
        .RequireAuthenticatedUser()
        .RequireClaim("tipo", OpcionesInvitados.TipoToken)
        .RequireClaim(OpcionesInvitados.ClaimCarrito));
    options.AddPolicy(Politicas.Personal, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(Politicas.RolCinemaManager, Politicas.RolAdmin));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
// Límite de pedidos de creación de carritos por origen (FR-021): ventana fija por IP del cliente; cuentan también los
// intentos rechazados. Es por proceso y, detrás de un proxy, exige configurar ForwardedHeaders para que RemoteIpAddress sea
// la IP real (ver specs/007-carrito-anonimo/research.md, Decisión 5).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<string>(CarritosController.PoliticaCreacionPorOrigen, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "origen-desconocido",
            _ =>
            {
                var carritos = contexto.RequestServices.GetRequiredService<OpcionesCarritos>();
                return new FixedWindowRateLimiterOptions
                {
                    PermitLimit = carritos.CreacionesPorOrigen,
                    Window = TimeSpan.FromMinutes(carritos.VentanaCreacionMinutos),
                    QueueLimit = 0
                };
            }));
    options.OnRejected = async (contexto, cancellationToken) =>
    {
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            contexto.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString();

        var problema = ProblemasDeNegocio.Crear(StatusCodes.Status429TooManyRequests,
            "Se superó el máximo de pedidos desde este origen. Intente más tarde.", "demasiados-pedidos");
        await problema.ExecuteResultAsync(new ActionContext(contexto.HttpContext, new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));
    };
});

// Documento OpenAPI: /openapi/v1.json en runtime y docs/openapi.json en cada build (ver Compartido/ConfiguracionOpenApi.cs).
builder.Services.AgregarDocumentacionOpenApi();

builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CinemaDb")));

// Compartido
builder.Services.AddScoped<IReloj, RelojSistema>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajoEf>();
builder.Services.AddScoped<IOutbox, OutboxEf>();

// Catalogo
builder.Services.AddScoped<IPeliculaRepository, PeliculaRepository>();
builder.Services.AddScoped<IFuncionAvailabilityChecker, NingunaFuncionDisponibleChecker>();
builder.Services.AddScoped<ListarPeliculasQueryHandler>();
builder.Services.AddScoped<ObtenerDetallePeliculaQueryHandler>();

// Funciones
builder.Services.AddScoped<IFuncionRepository, FuncionRepository>();
builder.Services.AddScoped<IOcupacionAsientos, CarritosRetienenAsientos>();
builder.Services.AddScoped<ListarSucursalesConFuncionesHoyQueryHandler>();
builder.Services.AddScoped<ListarHorariosDisponiblesHoyQueryHandler>();
builder.Services.AddScoped<ObtenerMapaAsientosQueryHandler>();

// Carritos
builder.Services.AddSingleton(sp =>
{
    var carritos = sp.GetRequiredService<OpcionesCarritos>();
    return new ReglasCarrito(carritos.MinutosRetencion, carritos.MaximoAsientosPorCarrito, carritos.PorcentajeMaximoRetenidoPorFuncion);
});
builder.Services.AddSingleton<AsignadorDeAsientos>();
builder.Services.AddScoped<ICarritoRepository, CarritoRepository>();
builder.Services.AddScoped<IConsultaCarritos, ConsultaCarritos>();
builder.Services.AddSingleton<IEmisorDeTokenDeCarrito, EmisorDeTokenDeCarritoJwt>();
// Punto de extensión anti-bots (FR-023): aprueba siempre hasta que se registre un proveedor real.
builder.Services.AddSingleton<IDesafioAntiBots, DesafioAntiBotsDesactivado>();
builder.Services.AddScoped<CrearCarritoCommandHandler>();
builder.Services.AddScoped<ObtenerCarritoQueryHandler>();
builder.Services.AddScoped<ReemplazarSeleccionCommandHandler>();
builder.Services.AddScoped<AbandonarCarritoCommandHandler>();
builder.Services.AddScoped<IndicarEmailEntregaCommandHandler>();

// Email al comprador: sin uso hasta la feature de pago, que enviará la entrada. El registrador escribe en el log: solo en
// Development y Testing. En el resto, falla de forma explícita.
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddScoped<IEnviadorDeEmail, EnviadorDeEmailRegistrador>();
else
    builder.Services.AddScoped<IEnviadorDeEmail, EnviadorDeEmailNoConfigurado>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Públicos aunque la política de fallback exija autenticación: solo existen en Development.
    app.MapOpenApi().AllowAnonymous();
    // Referencia navegable de la API en /scalar, con el documento de /openapi/v1.json.
    app.MapScalarApiReference().AllowAnonymous();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

    // Decisión explícita de descartar las reservas de bases de desarrollo al pasar a compradores por email (feature 006) y
    // después a carritos anónimos (feature 007, FR-024).
    Environment.SetEnvironmentVariable("CINEMA_PERMITIR_DESCARTE_RESERVAS", "true");
    await dbContext.Database.MigrateAsync();
    await DevDataSeeder.SeedAsync(dbContext);
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Necesario para que WebApplicationFactory<Program> sea accesible desde el proyecto de tests.
public partial class Program
{
}
