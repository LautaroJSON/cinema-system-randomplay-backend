using CinemaSystemRandomPlay.Application.Funciones.Queries;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;

namespace CinemaSystemRandomPlay.Application.UnitTests.Funciones;

public class ListarSucursalesConFuncionesHoyQueryHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 21, 15, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public async Task Handle_PeliculaValidaConSucursales_DevuelveLasSucursales()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.");
        var sucursalA = new Sucursal(Guid.NewGuid(), "Sucursal Centro");
        var sucursalB = new Sucursal(Guid.NewGuid(), "Sucursal Norte");

        var peliculaRepository = Substitute.For<IPeliculaRepository>();
        peliculaRepository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);

        var funcionRepository = Substitute.For<IFuncionRepository>();
        funcionRepository.ListarSucursalesConFuncionesDisponibles(pelicula.Id, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Sucursal> { sucursalA, sucursalB });

        var reloj = Substitute.For<IReloj>();
        reloj.Ahora.Returns(Ahora);

        var handler = new ListarSucursalesConFuncionesHoyQueryHandler(peliculaRepository, funcionRepository, reloj);

        var resultado = await handler.Handle(new ListarSucursalesConFuncionesHoyQuery(pelicula.Id));

        Assert.NotNull(resultado);
        Assert.Equal(2, resultado!.Count);
        Assert.Contains(resultado, s => s.Id == sucursalA.Id && s.Nombre == "Sucursal Centro");
        Assert.Contains(resultado, s => s.Id == sucursalB.Id && s.Nombre == "Sucursal Norte");
    }

    [Fact]
    public async Task Handle_PeliculaValidaSinSucursales_DevuelveListaVacia()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.");

        var peliculaRepository = Substitute.For<IPeliculaRepository>();
        peliculaRepository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);

        var funcionRepository = Substitute.For<IFuncionRepository>();
        funcionRepository.ListarSucursalesConFuncionesDisponibles(pelicula.Id, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Sucursal>());

        var reloj = Substitute.For<IReloj>();
        reloj.Ahora.Returns(Ahora);

        var handler = new ListarSucursalesConFuncionesHoyQueryHandler(peliculaRepository, funcionRepository, reloj);

        var resultado = await handler.Handle(new ListarSucursalesConFuncionesHoyQuery(pelicula.Id));

        Assert.NotNull(resultado);
        Assert.Empty(resultado!);
    }

    [Fact]
    public async Task Handle_PeliculaInactiva_DevuelveNull()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Vieja", 90, Clasificacion.ATP, "Sinopsis.", activa: false);

        var peliculaRepository = Substitute.For<IPeliculaRepository>();
        peliculaRepository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);

        var funcionRepository = Substitute.For<IFuncionRepository>();
        var reloj = Substitute.For<IReloj>();
        reloj.Ahora.Returns(Ahora);

        var handler = new ListarSucursalesConFuncionesHoyQueryHandler(peliculaRepository, funcionRepository, reloj);

        var resultado = await handler.Handle(new ListarSucursalesConFuncionesHoyQuery(pelicula.Id));

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Handle_PeliculaInexistente_DevuelveNull()
    {
        var peliculaRepository = Substitute.For<IPeliculaRepository>();
        peliculaRepository.ObtenerPorId(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Pelicula?)null);

        var funcionRepository = Substitute.For<IFuncionRepository>();
        var reloj = Substitute.For<IReloj>();
        reloj.Ahora.Returns(Ahora);

        var handler = new ListarSucursalesConFuncionesHoyQueryHandler(peliculaRepository, funcionRepository, reloj);

        var resultado = await handler.Handle(new ListarSucursalesConFuncionesHoyQuery(Guid.NewGuid()));

        Assert.Null(resultado);
    }
}
