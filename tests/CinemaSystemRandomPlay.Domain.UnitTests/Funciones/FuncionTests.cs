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

        var funcion = new Funcion(id, peliculaId, salaId, fechaHoraInicio, Dinero.Ars(5500m));

        Assert.Equal(id, funcion.Id);
        Assert.Equal(peliculaId, funcion.PeliculaId);
        Assert.Equal(salaId, funcion.SalaId);
        Assert.Equal(fechaHoraInicio, funcion.FechaHoraInicio);
        Assert.Null(funcion.Sala);
        Assert.Equal(Dinero.Ars(5500m), funcion.PrecioEntrada);
    }

    [Fact]
    public void Constructor_SinPrecio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.Now, null!));
    }

    [Fact]
    public void Constructor_ConPrecioCero_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.Now, Dinero.Ars(0m)));
    }

    [Fact]
    public void Constructor_ConPeliculaIdVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Funcion(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTimeOffset.Now, Dinero.Ars(5500m)));
    }

    [Fact]
    public void Constructor_ConSalaIdVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTimeOffset.Now, Dinero.Ars(5500m)));
    }

    [Fact]
    public void YaComenzo_ConHorarioFuturo_DevuelveFalse()
    {
        var ahora = new DateTimeOffset(2026, 9, 23, 15, 0, 0, TimeSpan.FromHours(-3));
        var funcion = new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ahora.AddMinutes(1), Dinero.Ars(5500m));

        Assert.False(funcion.YaComenzo(ahora));
    }

    [Fact]
    public void YaComenzo_ConHorarioIgualAAhora_DevuelveFalse()
    {
        var ahora = new DateTimeOffset(2026, 9, 23, 15, 0, 0, TimeSpan.FromHours(-3));
        var funcion = new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ahora, Dinero.Ars(5500m));

        Assert.False(funcion.YaComenzo(ahora));
    }

    [Fact]
    public void YaComenzo_ConHorarioPasado_DevuelveTrue()
    {
        var ahora = new DateTimeOffset(2026, 9, 23, 15, 0, 0, TimeSpan.FromHours(-3));
        var funcion = new Funcion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ahora.AddMinutes(-1), Dinero.Ars(5500m));

        Assert.True(funcion.YaComenzo(ahora));
    }
}
