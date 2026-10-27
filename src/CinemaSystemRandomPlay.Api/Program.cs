using CinemaSystemRandomPlay.Api.Autenticacion;
using CinemaSystemRandomPlay.Api.Compartido;
using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Catalogo.Queries;
using CinemaSystemRandomPlay.Application.Funciones.Ports;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using CinemaSystemRandomPlay.Application.Reservas.Commands;
using CinemaSystemRandomPlay.Application.Reservas.Ports;
using CinemaSystemRandomPlay.Application.Reservas.Queries;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Domain.Reservas;
using CinemaSystemRandomPlay.Infrastructure.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Reservas;
using CinemaSystemRandomPlay.Infrastructure.Reservas;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
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

// Autenticación: JWT bearer. Authority/audiencias vienen de Authentication:Schemes:Bearer (ver research.md, Decisión 7).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "sub";
        options.TokenValidationParameters.RoleClaimType = builder.Configuration["Autenticacion:ClaimRoles"] ?? "roles";
    });

// Todo endpoint sin política declarada exige usuario autenticado: fallback cerrado (Decisión 8).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Politicas.Customer, policy => policy.RequireAuthenticatedUser().RequireRole(Politicas.Customer));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
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
    builder.Configuration.GetValue("Reservas:MaximoAsientosPorCliente", 10)));
builder.Services.AddSingleton<ReservadorDeAsientos>();
builder.Services.AddScoped<IReservaRepository, ReservaRepository>();
builder.Services.AddScoped<IConsultaReservas, ConsultaReservas>();
builder.Services.AddScoped<ReservarAsientosCommandHandler>();
builder.Services.AddScoped<CancelarReservaCommandHandler>();
builder.Services.AddScoped<ListarMisReservasQueryHandler>();
builder.Services.AddScoped<ObtenerMiReservaQueryHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    await dbContext.Database.MigrateAsync();
    await DevDataSeeder.SeedAsync(dbContext);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Necesario para que WebApplicationFactory<Program> sea accesible desde el proyecto de tests.
public partial class Program
{
}
