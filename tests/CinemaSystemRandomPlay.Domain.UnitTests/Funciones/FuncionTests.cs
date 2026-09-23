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

    [Fact]
    public void YaComenzo_ConHorarioFuturo_DevuelveFalse()
    {
        var ahora = new DateTimeOffset(2026, 9, 23, 15, 0, 0, TimeSpan.FromHours(-3));
        var funcion = new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ahora.AddMinutes(1));

        Assert.False(funcion.YaComenzo(ahora));
    }

    [Fact]
    public void YaComenzo_ConHorarioIgualAAhora_DevuelveFalse()
    {
        var ahora = new DateTimeOffset(2026, 9, 23, 15, 0, 0, TimeSpan.FromHours(-3));
        var funcion = new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ahora);

        Assert.False(funcion.YaComenzo(ahora));
    }

    [Fact]
    public void YaComenzo_ConHorarioPasado_DevuelveTrue()
    {
        var ahora = new DateTimeOffset(2026, 9, 23, 15, 0, 0, TimeSpan.FromHours(-3));
        var funcion = new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ahora.AddMinutes(-1));

        Assert.True(funcion.YaComenzo(ahora));
    }
}
