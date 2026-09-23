using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Funciones;

public class SalaTests
{
    private static readonly FilaSala[] FilasValidas = [new('A', 10), new('B', 10)];

    [Fact]
    public void Constructor_ConDatosValidos_CreaLaSala()
    {
        var id = Guid.NewGuid();
        var sucursalId = Guid.NewGuid();
        var sala = new Sala(id, sucursalId, "Sala 1", FilasValidas);

        Assert.Equal(id, sala.Id);
        Assert.Equal(sucursalId, sala.SucursalId);
        Assert.Equal("Sala 1", sala.Nombre);
        Assert.Equal(2, sala.Filas.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ConNombreVacio_LanzaDomainException(string nombreInvalido)
    {
        Assert.Throws<DomainException>(() => new Sala(Guid.NewGuid(), Guid.NewGuid(), nombreInvalido, FilasValidas));
    }

    [Fact]
    public void Constructor_ConSucursalIdVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => new Sala(Guid.NewGuid(), Guid.Empty, "Sala 1", FilasValidas));
    }

    [Fact]
    public void Constructor_SinFilas_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => new Sala(Guid.NewGuid(), Guid.NewGuid(), "Sala 1", []));
    }

    [Fact]
    public void Constructor_ConFilasNull_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => new Sala(Guid.NewGuid(), Guid.NewGuid(), "Sala 1", null!));
    }

    [Fact]
    public void Constructor_ConLetrasDeFilaRepetidas_LanzaDomainException()
    {
        FilaSala[] filas = [new('A', 10), new('B', 8), new('A', 12)];

        Assert.Throws<DomainException>(() => new Sala(Guid.NewGuid(), Guid.NewGuid(), "Sala 1", filas));
    }

    [Fact]
    public void Filas_PasadasDesordenadas_SeExponenOrdenadasPorLetra()
    {
        FilaSala[] filas = [new('C', 12), new('A', 8), new('B', 10)];

        var sala = new Sala(Guid.NewGuid(), Guid.NewGuid(), "Sala 1", filas);

        Assert.Equal(['A', 'B', 'C'], sala.Filas.Select(f => f.Letra));
    }

    [Fact]
    public void TotalAsientos_ConFilasIrregulares_SumaTodasLasFilas()
    {
        FilaSala[] filas = [new('A', 8), new('B', 10), new('C', 14)];

        var sala = new Sala(Guid.NewGuid(), Guid.NewGuid(), "Sala 1", filas);

        Assert.Equal(32, sala.TotalAsientos);
    }

    private static Sala CrearSala(params FilaSala[] filas) => new(Guid.NewGuid(), Guid.NewGuid(), "Sala 1", filas);

    [Fact]
    public void AsientosDisponibles_SinOcupados_DevuelveTodasLasFilasCompletas()
    {
        var sala = CrearSala(new FilaSala('A', 3), new FilaSala('B', 4));

        var disponibles = sala.AsientosDisponibles([]);

        Assert.Equal(2, disponibles.Count);
        Assert.Equal('A', disponibles[0].Letra);
        Assert.Equal(3, disponibles[0].CantidadAsientos);
        Assert.Equal([1, 2, 3], disponibles[0].NumerosDisponibles);
        Assert.Equal([1, 2, 3, 4], disponibles[1].NumerosDisponibles);
    }

    [Fact]
    public void AsientosDisponibles_ConOcupados_LosExcluyeDeSuFilaSinAfectarAlResto()
    {
        var sala = CrearSala(new FilaSala('B', 6), new FilaSala('C', 6));

        var disponibles = sala.AsientosDisponibles([new Asiento('C', 5), new Asiento('C', 6)]);

        Assert.Equal([1, 2, 3, 4, 5, 6], disponibles[0].NumerosDisponibles);
        Assert.Equal([1, 2, 3, 4], disponibles[1].NumerosDisponibles);
        Assert.Equal(6, disponibles[1].CantidadAsientos);
    }

    [Fact]
    public void AsientosDisponibles_FilaEnteraOcupada_LaDevuelveConListaVacia()
    {
        var sala = CrearSala(new FilaSala('A', 2), new FilaSala('B', 2));

        var disponibles = sala.AsientosDisponibles([new Asiento('A', 1), new Asiento('A', 2)]);

        Assert.Equal(2, disponibles.Count);
        Assert.Equal('A', disponibles[0].Letra);
        Assert.Empty(disponibles[0].NumerosDisponibles);
        Assert.Equal(2, disponibles[0].CantidadAsientos);
    }

    [Fact]
    public void AsientosDisponibles_OcupadosQueNoPertenecenALaSala_SeIgnoran()
    {
        var sala = CrearSala(new FilaSala('A', 3));

        var disponibles = sala.AsientosDisponibles([new Asiento('Z', 1), new Asiento('A', 99)]);

        var fila = Assert.Single(disponibles);
        Assert.Equal([1, 2, 3], fila.NumerosDisponibles);
    }

    [Fact]
    public void AsientosDisponibles_DevuelveFilasOrdenadasPorLetraYNumerosAscendentes()
    {
        var sala = CrearSala(new FilaSala('C', 3), new FilaSala('A', 3), new FilaSala('B', 3));

        var disponibles = sala.AsientosDisponibles([new Asiento('B', 2)]);

        Assert.Equal(['A', 'B', 'C'], disponibles.Select(f => f.Letra));
        Assert.Equal([1, 3], disponibles[1].NumerosDisponibles);
    }
}
