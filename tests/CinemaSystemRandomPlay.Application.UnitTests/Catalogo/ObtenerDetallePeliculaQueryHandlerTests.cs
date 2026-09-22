using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Application.Catalogo.Queries;
using CinemaSystemRandomPlay.Domain.Catalogo;
using NSubstitute;

namespace CinemaSystemRandomPlay.Application.UnitTests.Catalogo;

public class ObtenerDetallePeliculaQueryHandlerTests
{
    [Fact]
    public async Task Handle_PeliculaActivaExistente_DevuelveElDetalle()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.");

        var repository = Substitute.For<IPeliculaRepository>();
        repository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);

        var checker = Substitute.For<IFuncionAvailabilityChecker>();
        checker.TieneFuncionesProgramadas(pelicula.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new ObtenerDetallePeliculaQueryHandler(repository, checker);

        var resultado = await handler.Handle(new ObtenerDetallePeliculaQuery(pelicula.Id));

        Assert.NotNull(resultado);
        Assert.Equal("Matrix", resultado!.Titulo);
        Assert.Equal("Sinopsis.", resultado.Sinopsis);
        Assert.False(resultado.SinFuncionesDisponibles);
    }

    [Fact]
    public async Task Handle_PeliculaInactiva_DevuelveNull()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Vieja", 90, Clasificacion.ATP, "Sinopsis.", activa: false);

        var repository = Substitute.For<IPeliculaRepository>();
        repository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);

        var checker = Substitute.For<IFuncionAvailabilityChecker>();

        var handler = new ObtenerDetallePeliculaQueryHandler(repository, checker);

        var resultado = await handler.Handle(new ObtenerDetallePeliculaQuery(pelicula.Id));

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Handle_PeliculaInexistente_DevuelveNull()
    {
        var repository = Substitute.For<IPeliculaRepository>();
        repository.ObtenerPorId(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Pelicula?)null);

        var checker = Substitute.For<IFuncionAvailabilityChecker>();

        var handler = new ObtenerDetallePeliculaQueryHandler(repository, checker);

        var resultado = await handler.Handle(new ObtenerDetallePeliculaQuery(Guid.NewGuid()));

        Assert.Null(resultado);
    }
}
