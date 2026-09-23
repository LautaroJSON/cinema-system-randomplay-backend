using CinemaSystemRandomPlay.Application.Funciones.Dtos;
using CinemaSystemRandomPlay.Application.Funciones.Ports;
using CinemaSystemRandomPlay.Application.Funciones.Queries;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;

namespace CinemaSystemRandomPlay.Application.UnitTests.Funciones;

public class ObtenerMapaAsientosQueryHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 23, 15, 0, 0, TimeSpan.FromHours(-3));

    private readonly IPeliculaRepository _peliculaRepository = Substitute.For<IPeliculaRepository>();
    private readonly IFuncionRepository _funcionRepository = Substitute.For<IFuncionRepository>();
    private readonly IReloj _reloj = Substitute.For<IReloj>();
    private readonly IOcupacionAsientos _ocupacionAsientos = Substitute.For<IOcupacionAsientos>();

    public ObtenerMapaAsientosQueryHandlerTests()
    {
        _reloj.Ahora.Returns(Ahora);
        _ocupacionAsientos.ObtenerOcupados(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Asiento>());
    }

    private ObtenerMapaAsientosQueryHandler CrearHandler() =>
        new(_peliculaRepository, _funcionRepository, _reloj, _ocupacionAsientos);

    private void ConfigurarOcupados(Guid funcionId, params Asiento[] ocupados)
    {
        _ocupacionAsientos.ObtenerOcupados(funcionId, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Asiento>(ocupados));
    }

    private static Funcion CrearFuncionConSala(Pelicula pelicula, DateTimeOffset fechaHoraInicio, params FilaSala[] filas)
    {
        var sucursal = new Sucursal(Guid.NewGuid(), "Sucursal Centro");
        var sala = new Sala(Guid.NewGuid(), sucursal.Id, "Sala 2", filas);
        typeof(Sala).GetProperty(nameof(Sala.Sucursal))!.SetValue(sala, sucursal);

        var funcion = new Funcion(Guid.NewGuid(), pelicula.Id, sala.Id, fechaHoraInicio);
        typeof(Funcion).GetProperty(nameof(Funcion.Sala))!.SetValue(funcion, sala);
        return funcion;
    }

    private void ConfigurarFuncion(Funcion funcion, Pelicula? pelicula)
    {
        _funcionRepository.ObtenerConSala(funcion.Id, Arg.Any<CancellationToken>()).Returns(funcion);
        _peliculaRepository.ObtenerPorId(funcion.PeliculaId, Arg.Any<CancellationToken>()).Returns(pelicula);
    }

    [Fact]
    public async Task Handle_FuncionValida_DevuelveLaDisposicionConContexto()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis.");
        var funcion = CrearFuncionConSala(pelicula, Ahora.AddHours(2), new FilaSala('B', 10), new FilaSala('A', 8), new FilaSala('C', 14));
        ConfigurarFuncion(funcion, pelicula);

        var resultado = await CrearHandler().Handle(new ObtenerMapaAsientosQuery(funcion.Id));

        Assert.Equal(EstadoMapaAsientos.Ok, resultado.Estado);
        var mapa = Assert.IsType<MapaAsientosDto>(resultado.Mapa);
        Assert.Equal(funcion.Id, mapa.FuncionId);
        Assert.Equal(funcion.FechaHoraInicio, mapa.HoraInicio);
        Assert.Equal(pelicula.Id, mapa.PeliculaId);
        Assert.Equal("Interstellar", mapa.PeliculaTitulo);
        Assert.Equal(funcion.Sala!.SucursalId, mapa.SucursalId);
        Assert.Equal("Sucursal Centro", mapa.SucursalNombre);
        Assert.Equal(funcion.SalaId, mapa.SalaId);
        Assert.Equal("Sala 2", mapa.SalaNombre);
        Assert.Equal(32, mapa.TotalAsientos);
        Assert.Equal(["A", "B", "C"], mapa.Filas.Select(f => f.Fila));
        Assert.Equal([8, 10, 14], mapa.Filas.Select(f => f.CantidadAsientos));
    }

    [Fact]
    public async Task Handle_FuncionInexistente_DevuelveFuncionNoEncontrada()
    {
        _funcionRepository.ObtenerConSala(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Funcion?)null);

        var resultado = await CrearHandler().Handle(new ObtenerMapaAsientosQuery(Guid.NewGuid()));

        Assert.Equal(EstadoMapaAsientos.FuncionNoEncontrada, resultado.Estado);
        Assert.Null(resultado.Mapa);
    }

    [Fact]
    public async Task Handle_PeliculaInexistente_DevuelveFuncionNoEncontrada()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis.");
        var funcion = CrearFuncionConSala(pelicula, Ahora.AddHours(2), new FilaSala('A', 8));
        ConfigurarFuncion(funcion, null);

        var resultado = await CrearHandler().Handle(new ObtenerMapaAsientosQuery(funcion.Id));

        Assert.Equal(EstadoMapaAsientos.FuncionNoEncontrada, resultado.Estado);
    }

    [Fact]
    public async Task Handle_PeliculaInactiva_DevuelveFuncionNoEncontrada()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Vieja", 80, Clasificacion.ATP, "Sinopsis.", activa: false);
        var funcion = CrearFuncionConSala(pelicula, Ahora.AddHours(2), new FilaSala('A', 8));
        ConfigurarFuncion(funcion, pelicula);

        var resultado = await CrearHandler().Handle(new ObtenerMapaAsientosQuery(funcion.Id));

        Assert.Equal(EstadoMapaAsientos.FuncionNoEncontrada, resultado.Estado);
    }

    [Fact]
    public async Task Handle_FuncionYaComenzada_DevuelveFuncionNoDisponible()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis.");
        var funcion = CrearFuncionConSala(pelicula, Ahora.AddMinutes(-10), new FilaSala('A', 8));
        ConfigurarFuncion(funcion, pelicula);

        var resultado = await CrearHandler().Handle(new ObtenerMapaAsientosQuery(funcion.Id));

        Assert.Equal(EstadoMapaAsientos.FuncionNoDisponible, resultado.Estado);
        Assert.Null(resultado.Mapa);
    }

    [Fact]
    public async Task Handle_SinAsientosOcupados_TodosDisponibles()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis.");
        var funcion = CrearFuncionConSala(pelicula, Ahora.AddHours(2), new FilaSala('A', 3), new FilaSala('B', 4));
        ConfigurarFuncion(funcion, pelicula);

        var resultado = await CrearHandler().Handle(new ObtenerMapaAsientosQuery(funcion.Id));

        var mapa = resultado.Mapa!;
        Assert.Equal(7, mapa.CantidadDisponibles);
        Assert.Equal(mapa.TotalAsientos, mapa.CantidadDisponibles);
        Assert.Equal([1, 2, 3], mapa.Filas[0].AsientosDisponibles);
        Assert.Equal([1, 2, 3, 4], mapa.Filas[1].AsientosDisponibles);
    }

    [Fact]
    public async Task Handle_ConAsientosOcupados_NoLosDevuelveYDescuentaLosDisponibles()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis.");
        var funcion = CrearFuncionConSala(pelicula, Ahora.AddHours(2), new FilaSala('B', 6), new FilaSala('C', 6));
        ConfigurarFuncion(funcion, pelicula);
        ConfigurarOcupados(funcion.Id, new Asiento('C', 5), new Asiento('C', 6));

        var resultado = await CrearHandler().Handle(new ObtenerMapaAsientosQuery(funcion.Id));

        var mapa = resultado.Mapa!;
        Assert.Equal(12, mapa.TotalAsientos);
        Assert.Equal(10, mapa.CantidadDisponibles);
        Assert.Equal([1, 2, 3, 4, 5, 6], mapa.Filas[0].AsientosDisponibles);
        Assert.Equal([1, 2, 3, 4], mapa.Filas[1].AsientosDisponibles);
    }

    [Fact]
    public async Task Handle_TodosLosAsientosOcupados_CeroDisponiblesYFilasPresentes()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Interstellar", 169, Clasificacion.ATP, "Sinopsis.");
        var funcion = CrearFuncionConSala(pelicula, Ahora.AddHours(2), new FilaSala('A', 2));
        ConfigurarFuncion(funcion, pelicula);
        ConfigurarOcupados(funcion.Id, new Asiento('A', 1), new Asiento('A', 2));

        var resultado = await CrearHandler().Handle(new ObtenerMapaAsientosQuery(funcion.Id));

        var mapa = resultado.Mapa!;
        Assert.Equal(0, mapa.CantidadDisponibles);
        var fila = Assert.Single(mapa.Filas);
        Assert.Equal(2, fila.CantidadAsientos);
        Assert.Empty(fila.AsientosDisponibles);
    }
}
