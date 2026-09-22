using CinemaSystemRandomPlay.Application.Catalogo.Ports;
using CinemaSystemRandomPlay.Application.Catalogo.Queries;
using CinemaSystemRandomPlay.Domain.Catalogo;
using NSubstitute;

namespace CinemaSystemRandomPlay.Application.UnitTests.Catalogo;

public class ListarPeliculasQueryHandlerTests
{
    [Fact]
    public async Task Handle_DevuelvePeliculasActivas_ConSinFuncionesDisponiblesResuelto()
    {
        var pelicula1 = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis 1");
        var pelicula2 = new Pelicula(Guid.NewGuid(), "Dune", 155, Clasificacion.Mas13, "Sinopsis 2");

        var repository = Substitute.For<IPeliculaRepository>();
        repository.ListarActivas(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<Pelicula> { pelicula1, pelicula2 });

        var checker = Substitute.For<IFuncionAvailabilityChecker>();
        checker.TieneFuncionesProgramadas(pelicula1.Id, Arg.Any<CancellationToken>()).Returns(true);
        checker.TieneFuncionesProgramadas(pelicula2.Id, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new ListarPeliculasQueryHandler(repository, checker);

        var resultado = await handler.Handle(new ListarPeliculasQuery());

        Assert.Equal(2, resultado.Count);
        Assert.False(resultado.Single(p => p.Id == pelicula1.Id).SinFuncionesDisponibles);
        Assert.True(resultado.Single(p => p.Id == pelicula2.Id).SinFuncionesDisponibles);
    }

    [Fact]
    public async Task Handle_SinPeliculasActivas_DevuelveListaVacia()
    {
        var repository = Substitute.For<IPeliculaRepository>();
        repository.ListarActivas(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Pelicula>());

        var checker = Substitute.For<IFuncionAvailabilityChecker>();

        var handler = new ListarPeliculasQueryHandler(repository, checker);

        var resultado = await handler.Handle(new ListarPeliculasQuery());

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_ConTitulo_LoPropagaAlRepositorio()
    {
        var repository = Substitute.For<IPeliculaRepository>();
        repository.ListarActivas(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new List<Pelicula>());

        var checker = Substitute.For<IFuncionAvailabilityChecker>();

        var handler = new ListarPeliculasQueryHandler(repository, checker);

        await handler.Handle(new ListarPeliculasQuery("amel"));

        await repository.Received(1).ListarActivas("amel", Arg.Any<CancellationToken>());
    }
}
