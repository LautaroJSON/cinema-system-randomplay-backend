using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Funciones;

public class FuncionTests
{
    [Fact]
    public void Constructor_ConDatosValidos_CreaLaFuncion()
    {
        var id = Guid.NewGuid();
        var peliculaId = Guid.NewGuid();
        var salaId = Guid.NewGuid();
        var fechaHoraInicio = DateTimeOffset.Now.AddHours(2);

        var funcion = new Funcion(id, peliculaId, salaId, fechaHoraInicio);

        Assert.Equal(id, funcion.Id);
        Assert.Equal(peliculaId, funcion.PeliculaId);
        Assert.Equal(salaId, funcion.SalaId);
        Assert.Equal(fechaHoraInicio, funcion.FechaHoraInicio);
        Assert.Null(funcion.Sala);
    }

    [Fact]
    public void Constructor_ConPeliculaIdVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Funcion(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTimeOffset.Now));
    }

    [Fact]
    public void Constructor_ConSalaIdVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTimeOffset.Now));
    }
}
