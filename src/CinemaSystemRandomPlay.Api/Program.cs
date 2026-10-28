using CinemaSystemRandomPlay.Api.Autenticacion;
using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Api.Modulos.Verificaciones;
using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Catalogo.Queries;
using CinemaSystemRandomPlay.Application.Funciones.Ports;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using CinemaSystemRandomPlay.Application.Reservas.Commands;
using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Application.Reservas.Queries;
using CinemaSystemRandomPlay.Application.Verificaciones.Commands;
using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Domain.Verificaciones;
using CinemaSystemRandomPlay.Infrastructure.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Reservas;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Verificaciones;
using CinemaSystemRandomPlay.Infrastructure.Reservas;
using CinemaSystemRandomPlay.Infrastructure.Invitados;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

// Credencial del comprador invitado: JWT firmado por esta API (research.md, Decisión 1). Las opciones se enlazan de forma
// diferida y se validan al arrancar: sin una ClaveFirma de al menos 32 bytes, la API no inicia.
builder.Services.AddOptions<OpcionesInvitados>()
    .Bind(builder.Configuration.GetSection(OpcionesInvitados.Seccion))
    .Validate(
        o => Encoding.UTF8.GetByteCount(o.ClaveFirma) >= OpcionesInvitados.LargoMinimoClave,
        $"Falta {OpcionesInvitados.Seccion}:ClaveFirma o mide menos de {OpcionesInvitados.LargoMinimoClave} bytes. " +
        "Configurarla con user-secrets o una variable de entorno (ver specs/006-comprador-invitado-email/quickstart.md).")
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcionesInvitados>>().Value);

// Autenticación: dos esquemas separados. "Bearer" (por defecto) es el proveedor OIDC del personal; Authority/audiencias
// vienen de Authentication:Schemes:Bearer. "Invitado" valida solo credenciales emitidas por esta API.
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

        // Una credencial con nuestra firma pero sin el tipo esperado o sin un email válido no es una credencial de invitado:
        // se rechaza como no autenticada (401), no como no autorizada.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = contexto =>
            {
                var tipo = contexto.Principal?.FindFirst("tipo")?.Value;
                var email = contexto.Principal?.FindFirst("email")?.Value;

                if (tipo != OpcionesInvitados.TipoCredencial || !EmailComprador.TryCrear(email, out _))
                    contexto.Fail("La credencial no es de un comprador invitado.");

                return Task.CompletedTask;
            }
        };
    });

// Modalidades de acceso: toda operación declara la suya. Sin declarar, el fallback exige usuario autenticado: cerrado (FR-017).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Politicas.InvitadoVerificado, policy => policy
        .AddAuthenticationSchemes(EsquemasDeAutenticacion.Invitado)
        .RequireAuthenticatedUser()
        .RequireClaim("tipo", OpcionesInvitados.TipoCredencial)
        .RequireClaim("email"));
    options.AddPolicy(Politicas.Personal, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(Politicas.RolCinemaManager, Politicas.RolAdmin));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
// Límite por origen de los pedidos de código (FR-006): ventana fija por IP del cliente. Es por proceso y, detrás de un
// proxy, exige configurar ForwardedHeaders para que RemoteIpAddress sea la IP real (ver research.md, Decisión 6).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<string>(VerificacionesEmailController.PoliticaLimitePorOrigen, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "origen-desconocido",
            _ =>
            {
                var invitados = contexto.RequestServices.GetRequiredService<OpcionesInvitados>();
                return new FixedWindowRateLimiterOptions
                {
                    PermitLimit = invitados.SolicitudesPorOrigen,
                    Window = TimeSpan.FromMinutes(invitados.VentanaSolicitudesMinutos),
                    QueueLimit = 0
                };
            }));
    options.OnRejected = async (contexto, cancellationToken) =>
    {
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            contexto.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString();

        var problema = ProblemasDeNegocio.Crear(StatusCodes.Status429TooManyRequests,
            "Se superó el máximo de pedidos de código desde este origen. Intente más tarde.", "demasiados-pedidos");
        await problema.ExecuteResultAsync(new ActionContext(contexto.HttpContext, new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));
    };
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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
builder.Services.AddScoped<IOcupacionAsientos, ReservasRetienenAsientos>();
builder.Services.AddScoped<ListarSucursalesConFuncionesHoyQueryHandler>();
builder.Services.AddScoped<ListarHorariosDisponiblesHoyQueryHandler>();
builder.Services.AddScoped<ObtenerMapaAsientosQueryHandler>();

// Reservas
builder.Services.AddSingleton(new ReglasReserva(
    builder.Configuration.GetValue("Reservas:MinutosRetencion", 10),
    builder.Configuration.GetValue("Reservas:MaximoAsientosPorComprador", 10)));
builder.Services.AddSingleton<ReservadorDeAsientos>();
builder.Services.AddScoped<IReservaRepository, ReservaRepository>();
builder.Services.AddScoped<IConsultaReservas, ConsultaReservas>();
builder.Services.AddScoped<ReservarAsientosCommandHandler>();
builder.Services.AddScoped<CancelarReservaCommandHandler>();
builder.Services.AddScoped<ListarMisReservasQueryHandler>();
builder.Services.AddScoped<ObtenerMiReservaQueryHandler>();

// Verificación del email del invitado
builder.Services.AddSingleton(sp =>
{
    var invitados = sp.GetRequiredService<OpcionesInvitados>();
    return new ReglasVerificacion(invitados.CodigoMinutosVigencia, invitados.MaximoIntentosFallidos,
        invitados.SolicitudesPorEmail, invitados.VentanaSolicitudesMinutos);
});
builder.Services.AddSingleton<ICodigosDeVerificacion, CodigosDeVerificacionHmac>();
builder.Services.AddSingleton<IEmisorDeCredencial, EmisorDeCredencialJwt>();
builder.Services.AddScoped<IVerificacionEmailRepository, VerificacionEmailRepository>();
// El registrador escribe el código en claro en el log: solo en Development y Testing. En el resto, falla de forma explícita.
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddScoped<IEnviadorDeEmail, EnviadorDeEmailRegistrador>();
else
    builder.Services.AddScoped<IEnviadorDeEmail, EnviadorDeEmailNoConfigurado>();
builder.Services.AddScoped<SolicitarVerificacionEmailCommandHandler>();
builder.Services.AddScoped<ConfirmarVerificacionEmailCommandHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

    // Decisión explícita de descartar las reservas de bases de desarrollo al pasar a compradores por email (FR-021).
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
