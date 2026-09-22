using CinemaSystemRandomPlay.Application.Funciones.Queries;
using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;

namespace CinemaSystemRandomPlay.Application.UnitTests.Funciones;

public class ListarHorariosDisponiblesHoyQueryHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 21, 15, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public async Task Handle_SucursalValidaConHorarios_DevuelveLosHorariosConNombreDeSala()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.");
        var sucursalId = Guid.NewGuid();
        var sala = new Sala(Guid.NewGuid(), sucursalId, "Sala 1");
        var funcion = new Funcion(Guid.NewGuid(), pelicula.Id, sala.Id, Ahora.AddHours(2));
        typeof(Funcion).GetProperty(nameof(Funcion.Sala))!.SetValue(funcion, sala);

        var peliculaRepository = Substitute.For<IPeliculaRepository>();
        peliculaRepository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);

        var funcionRepository = Substitute.For<IFuncionRepository>();
        funcionRepository.ExisteFuncionHoy(pelicula.Id, sucursalId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        funcionRepository.ListarHorariosDisponibles(pelicula.Id, sucursalId, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Funcion> { funcion });

        var reloj = Substitute.For<IReloj>();
        reloj.Ahora.Returns(Ahora);

        var handler = new ListarHorariosDisponiblesHoyQueryHandler(peliculaRepository, funcionRepository, reloj);

        var resultado = await handler.Handle(new ListarHorariosDisponiblesHoyQuery(pelicula.Id, sucursalId));

        Assert.NotNull(resultado);
        var horario = Assert.Single(resultado!);
        Assert.Equal(funcion.Id, horario.FuncionId);
        Assert.Equal(sala.Id, horario.SalaId);
        Assert.Equal("Sala 1", horario.SalaNombre);
    }

    [Fact]
    public async Task Handle_SucursalValidaSinHorariosRestantes_DevuelveListaVacia()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.");
        var sucursalId = Guid.NewGuid();

        var peliculaRepository = Substitute.For<IPeliculaRepository>();
        peliculaRepository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);

        var funcionRepository = Substitute.For<IFuncionRepository>();
        funcionRepository.ExisteFuncionHoy(pelicula.Id, sucursalId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        funcionRepository.ListarHorariosDisponibles(pelicula.Id, sucursalId, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Funcion>());

        var reloj = Substitute.For<IReloj>();
        reloj.Ahora.Returns(Ahora);

        var handler = new ListarHorariosDisponiblesHoyQueryHandler(peliculaRepository, funcionRepository, reloj);

        var resultado = await handler.Handle(new ListarHorariosDisponiblesHoyQuery(pelicula.Id, sucursalId));

        Assert.NotNull(resultado);
        Assert.Empty(resultado!);
    }

    [Fact]
    public async Task Handle_SucursalNoValidaParaLaPelicula_DevuelveNull()
    {
        var pelicula = new Pelicula(Guid.NewGuid(), "Matrix", 136, Clasificacion.Mas13, "Sinopsis.");
        var sucursalId = Guid.NewGuid();

        var peliculaRepository = Substitute.For<IPeliculaRepository>();
        peliculaRepository.ObtenerPorId(pelicula.Id, Arg.Any<CancellationToken>()).Returns(pelicula);

        var funcionRepository = Substitute.For<IFuncionRepository>();
        funcionRepository.ExisteFuncionHoy(pelicula.Id, sucursalId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(false);

        var reloj = Substitute.For<IReloj>();
        reloj.Ahora.Returns(Ahora);

        var handler = new ListarHorariosDisponiblesHoyQueryHandler(peliculaRepository, funcionRepository, reloj);

        var resultado = await handler.Handle(new ListarHorariosDisponiblesHoyQuery(pelicula.Id, sucursalId));

        Assert.Null(resultado);
        await funcionRepository.DidNotReceive().ListarHorariosDisponibles(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PeliculaNoEncontrada_DevuelveNull()
    {
        var peliculaRepository = Substitute.For<IPeliculaRepository>();
        peliculaRepository.ObtenerPorId(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Pelicula?)null);

        var funcionRepository = Substitute.For<IFuncionRepository>();
        var reloj = Substitute.For<IReloj>();
        reloj.Ahora.Returns(Ahora);

        var handler = new ListarHorariosDisponiblesHoyQueryHandler(peliculaRepository, funcionRepository, reloj);

        var resultado = await handler.Handle(new ListarHorariosDisponiblesHoyQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Null(resultado);
    }
}
