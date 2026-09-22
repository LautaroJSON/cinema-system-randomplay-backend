using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Application.Catalogo.Queries;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using CinemaSystemRandomPlay.Infrastructure.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CinemaDb")));

// Compartido
builder.Services.AddScoped<IReloj, RelojSistema>();

// Catalogo
builder.Services.AddScoped<IPeliculaRepository, PeliculaRepository>();
builder.Services.AddScoped<IFuncionAvailabilityChecker, NingunaFuncionDisponibleChecker>();
builder.Services.AddScoped<ListarPeliculasQueryHandler>();
builder.Services.AddScoped<ObtenerDetallePeliculaQueryHandler>();

// Funciones
builder.Services.AddScoped<IFuncionRepository, FuncionRepository>();
builder.Services.AddScoped<ListarSucursalesConFuncionesHoyQueryHandler>();
builder.Services.AddScoped<ListarHorariosDisponiblesHoyQueryHandler>();

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

app.UseAuthorization();

app.MapControllers();

app.Run();

// Necesario para que WebApplicationFactory<Program> sea accesible desde el proyecto de tests.
public partial class Program
{
}
