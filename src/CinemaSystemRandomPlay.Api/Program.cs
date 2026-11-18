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
using CinemaSystemRandomPlay.Infrastructure.Email;
using CinemaSystemRandomPlay.Infrastructure.Outbox;
using CinemaSystemRandomPlay.Application.Pagos.Manejadores;
using CinemaSystemRandomPlay.Infrastructure.Pagos;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Pagos;
using CinemaSystemRandomPlay.Application.Pagos.Commands;
using CinemaSystemRandomPlay.Application.Pagos.Ports;
using CinemaSystemRandomPlay.Domain.Pagos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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

// Mercado Pago (spec 008). Los secretos (AccessToken, ClaveSecretaAvisos) solo por user-secrets o variables de entorno. Se valida
// al arrancar, salvo en el generador de docs/openapi.json (ver OpcionesInvitados arriba).
var opcionesMercadoPago = builder.Services.AddOptions<OpcionesMercadoPago>()
    .Bind(builder.Configuration.GetSection(OpcionesMercadoPago.Seccion))
    .Validate(o => o.Validar().Count == 0,
        $"La sección {OpcionesMercadoPago.Seccion} es inválida: en modo Real hacen falta AccessToken, ClaveSecretaAvisos y UrlAvisos " +
        "(ver specs/008-pago-mercado-pago/quickstart.md).");
if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
    opcionesMercadoPago.ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcionesMercadoPago>>().Value);
builder.Services.AddOptions<OpcionesOutbox>().Bind(builder.Configuration.GetSection(OpcionesOutbox.Seccion));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcionesOutbox>>().Value);

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

// /health para el hosting y el ping que lo mantiene despierto (ver Compartido/BaseDeDatosDisponible.cs).
builder.Services.AddHealthChecks().AddCheck<BaseDeDatosDisponible>("base-de-datos");

// CORS: orígenes del front permitidos, por configuración (Cors:OrigenesPermitidos). Sin orígenes, no se permite ninguno.
var origenesPermitidos = builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(origenesPermitidos)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Location", "Retry-After")));

// Detrás del proxy del hosting, la IP del cliente y el esquema llegan en X-Forwarded-For/Proto. Con ForwardLimit = 1 (el valor
// por defecto) solo se toma la última IP, la que agrega el proxy, así que un cliente no puede falsear la suya para eludir el
// límite por origen. En Development y Testing no hay proxy y no se aplica.
var detrasDeProxy = !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing");
if (detrasDeProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // El proxy del hosting no tiene una IP fija conocida.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Compartido
builder.Services.AddScoped<IReloj, RelojSistema>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajoEf>();
builder.Services.AddScoped<IOutbox, OutboxEf>();

// Catalogo
builder.Services.AddScoped<IPeliculaRepository, PeliculaRepository>();
builder.Services.AddScoped<IFuncionAvailabilityChecker, FuncionesDeHoyAvailabilityChecker>();
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
builder.Services.AddScoped<IniciarPagoCommandHandler>();

// Pagos (spec 008). El simulador se registra siempre (sin estado externo); la pasarela es el simulador o Mercado Pago según
// MercadoPago:Modo, leído de las opciones ya enlazadas al resolverla.
builder.Services.AddSingleton<ProcesadorDePagosSimulado>();
builder.Services.AddScoped<IProcesadorDePagos>(sp => sp.GetRequiredService<OpcionesMercadoPago>().Modo == ModoMercadoPago.Real
    ? ActivatorUtilities.CreateInstance<ProcesadorDePagosMercadoPago>(sp)
    : sp.GetRequiredService<ProcesadorDePagosSimulado>());
builder.Services.AddSingleton<ConfirmadorDePagos>();
builder.Services.AddSingleton<IGeneradorDeCodigos, GeneradorDeCodigosCrockford>();
builder.Services.AddScoped<IRegistroDePagos, RegistroDePagos>();
builder.Services.AddScoped<ProcesarPagoCommandHandler>();
builder.Services.AddSingleton<IVerificadorDeAvisos, VerificadorDeAvisosMercadoPago>();
builder.Services.AddScoped<RecibirAvisoDePagoCommandHandler>();
builder.Services.AddSingleton(sp =>
    new OpcionesVerificacionDePagos(TimeSpan.FromSeconds(sp.GetRequiredService<OpcionesMercadoPago>().SegundosTimeoutConsulta)));

// Outbox (Principio VI): publicador en background con reintentos. Expuesto también como servicio para los tests.
builder.Services.AddSingleton<ProcesadorDeOutbox>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProcesadorDeOutbox>());

// Email de entrega de las entradas (spec 008): lo envía el Outbox. Smtp sirve para Mailpit (Development) y para cualquier proveedor
// por SMTP; el registrador solo escribe en el log y se admite en Development y Testing. En el resto, sin proveedor, falla de forma
// explícita.
var opcionesEmail = builder.Configuration.GetSection(OpcionesEmail.Seccion).Get<OpcionesEmail>() ?? new OpcionesEmail();
builder.Services.AddSingleton(opcionesEmail);
if (opcionesEmail.Proveedor == ProveedorEmail.Smtp)
{
    if (string.IsNullOrWhiteSpace(opcionesEmail.Smtp.Host) && Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
        throw new InvalidOperationException($"Con {OpcionesEmail.Seccion}:Proveedor=Smtp falta {OpcionesEmail.Seccion}:Smtp:Host.");
    builder.Services.AddScoped<IEnviadorDeEmail, EnviadorDeEmailSmtp>();
}
else if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddScoped<IEnviadorDeEmail, EnviadorDeEmailRegistrador>();
else
    builder.Services.AddScoped<IEnviadorDeEmail, EnviadorDeEmailNoConfigurado>();
builder.Services.AddScoped<IManejadorDeMensaje, EnviarEmailDeEntrega>();
builder.Services.AddScoped<IManejadorDeMensaje, EjecutarDevolucion>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Públicos aunque la política de fallback exija autenticación: solo existen en Development.
    app.MapOpenApi().AllowAnonymous();
    // Referencia navegable de la API en /scalar, con el documento de /openapi/v1.json.
    app.MapScalarApiReference().AllowAnonymous();

    // Decisión explícita de descartar las reservas de bases de desarrollo al pasar a compradores por email (feature 006) y
    // después a carritos anónimos (feature 007, FR-024).
    Environment.SetEnvironmentVariable("CINEMA_PERMITIR_DESCARTE_RESERVAS", "true");
}

// Migraciones y datos de demo: siempre en Development; fuera de él, solo si se piden de forma explícita
// (BaseDeDatos:AplicarMigraciones y BaseDeDatos:SembrarDatosDemo), por ejemplo en el deploy de demostración.
var aplicarMigraciones = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("BaseDeDatos:AplicarMigraciones");
var sembrarDatosDemo = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("BaseDeDatos:SembrarDatosDemo");
if (aplicarMigraciones || sembrarDatosDemo)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

    if (aplicarMigraciones)
        await dbContext.Database.MigrateAsync();
    if (sembrarDatosDemo)
        await DevDataSeeder.SeedAsync(dbContext);
}

if (detrasDeProxy)
    app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseCors();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();

app.Run();

// Necesario para que WebApplicationFactory<Program> sea accesible desde el proyecto de tests.
public partial class Program
{
}
