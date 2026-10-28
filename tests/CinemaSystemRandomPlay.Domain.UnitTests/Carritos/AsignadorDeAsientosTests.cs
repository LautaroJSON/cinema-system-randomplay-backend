using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using static CinemaSystemRandomPlay.Domain.UnitTests.Carritos.CarritoFactory;

namespace CinemaSystemRandomPlay.Domain.UnitTests.Carritos;

public class AsignadorDeAsientosTests
{
    private readonly AsignadorDeAsientos _asignador = new();
    private readonly ReglasCarrito _reglas = new();

    private ResultadoSeleccion Crear(Funcion funcion, IReadOnlyList<Carrito> existentes, params Asiento[] pedidos) =>
        _asignador.Crear(funcion, existentes, pedidos, Ahora, _reglas);

    private ResultadoSeleccion Reemplazar(Funcion funcion, Carrito carrito, IReadOnlyList<Carrito> existentes, params Asiento[] pedidos) =>
        _asignador.Reemplazar(funcion, carrito, existentes, pedidos, Ahora, _reglas);

    [Fact]
    public void Crear_AsientosLibres_CreaCarritoActivoQueVenceALosCincoMinutos()
    {
        var funcion = CrearFuncion();

        var resultado = Crear(funcion, [], new Asiento('C', 6), new Asiento('C', 5));

        Assert.Equal(TipoResultadoSeleccion.Ok, resultado.Tipo);
        var carrito = resultado.Carrito!;
        Assert.Equal(funcion.Id, carrito.FuncionId);
        Assert.Equal(EstadoCarrito.Activo, carrito.Estado);
        Assert.Equal(Ahora, carrito.CreadoEn);
        Assert.Equal(Ahora.AddMinutes(5), carrito.VenceEn);
        Assert.Null(carrito.EmailEntrega);
        Assert.Equal([new Asiento('C', 5), new Asiento('C', 6)], carrito.Asientos);
    }

    [Fact]
    public void Crear_CongelaElPrecioDeLaFuncionYCalculaElTotal()
    {
        var funcion = CrearFuncion();

        var carrito = Crear(funcion, [], new Asiento('C', 5), new Asiento('C', 6), new Asiento('C', 7)).Carrito!;

        Assert.Equal(funcion.PrecioEntrada, carrito.PrecioUnitario);
        Assert.Equal(Dinero.Ars(16500m), carrito.Total);
    }

    [Fact]
    public void Crear_FuncionQueEmpiezaAntesDeLaRetencion_VenceAlInicioDeLaFuncion()
    {
        var funcion = CrearFuncion(Ahora.AddMinutes(2));

        var resultado = Crear(funcion, [], new Asiento('A', 1));

        Assert.Equal(Ahora.AddMinutes(2), resultado.Carrito!.VenceEn);
    }

    [Fact]
    public void Crear_FuncionYaComenzada_DevuelveFuncionNoDisponible()
    {
        var funcion = CrearFuncion(Ahora.AddMinutes(-1));

        Assert.Equal(TipoResultadoSeleccion.FuncionNoDisponible, Crear(funcion, [], new Asiento('A', 1)).Tipo);
    }

    [Fact]
    public void Crear_AsientosInexistentes_DevuelveSoloLosInexistentesOrdenados()
    {
        var funcion = CrearFuncion();

        var resultado = Crear(funcion, [], new Asiento('Z', 1), new Asiento('A', 15), new Asiento('A', 1));

        Assert.Equal(TipoResultadoSeleccion.AsientosInexistentes, resultado.Tipo);
        Assert.Equal([new Asiento('A', 15), new Asiento('Z', 1)], resultado.Asientos);
    }

    [Fact]
    public void Crear_AsientoRetenidoPorOtroCarrito_DevuelveSoloLosNoDisponibles()
    {
        var funcion = CrearFuncion();
        var otro = CarritoDe(funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));

        var resultado = Crear(funcion, [otro], new Asiento('C', 7), new Asiento('C', 6));

        Assert.Equal(TipoResultadoSeleccion.AsientosNoDisponibles, resultado.Tipo);
        Assert.Equal([new Asiento('C', 6)], resultado.Asientos);
    }

    [Fact]
    public void Crear_AsientosDeCarritosVencidosOAbandonados_EstanDisponibles()
    {
        var funcion = CrearFuncion();
        var vencido = CarritoDe(funcion, Ahora.AddMinutes(-6), new Asiento('C', 5));
        var abandonado = CarritoDe(funcion, Ahora, new Asiento('C', 6));
        abandonado.Abandonar(Ahora);

        var resultado = Crear(funcion, [vencido, abandonado], new Asiento('C', 5), new Asiento('C', 6));

        Assert.Equal(TipoResultadoSeleccion.Ok, resultado.Tipo);
    }

    [Fact]
    public void Crear_SuperaElTopeDeLaFuncion_DevuelveFuncionSinCupoSinDatos()
    {
        // 34 asientos al 50%: tope 17.
        var funcion = CrearFuncion();
        var otro = CarritoDe(funcion, Ahora, Fila('A', 1, 10));
        var otroMas = CarritoDe(funcion, Ahora, Fila('B', 1, 6));

        var resultado = Crear(funcion, [otro, otroMas], new Asiento('C', 1), new Asiento('C', 2));

        Assert.Equal(TipoResultadoSeleccion.FuncionSinCupo, resultado.Tipo);
        Assert.Null(resultado.Asientos);
        Assert.Null(resultado.Carrito);
    }

    [Fact]
    public void Crear_JustoHastaElTope_SeAcepta()
    {
        var funcion = CrearFuncion();
        var otro = CarritoDe(funcion, Ahora, Fila('A', 1, 10));
        var otroMas = CarritoDe(funcion, Ahora, Fila('B', 1, 6));

        Assert.Equal(TipoResultadoSeleccion.Ok, Crear(funcion, [otro, otroMas], new Asiento('C', 1)).Tipo);
    }

    [Fact]
    public void Crear_AsientoNoDisponibleYTopeSuperado_InformaPrimeroLosNoDisponibles()
    {
        var funcion = CrearFuncion();
        var otro = CarritoDe(funcion, Ahora, Fila('A', 1, 10));
        var otroMas = CarritoDe(funcion, Ahora, Fila('B', 1, 7));

        var resultado = Crear(funcion, [otro, otroMas], new Asiento('A', 1));

        Assert.Equal(TipoResultadoSeleccion.AsientosNoDisponibles, resultado.Tipo);
    }

    [Fact]
    public void Reemplazar_AsientosLibres_ReemplazaYDevuelveLaDiferencia()
    {
        var funcion = CrearFuncion();
        var carrito = CarritoDe(funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));

        var resultado = Reemplazar(funcion, carrito, [carrito], new Asiento('C', 6), new Asiento('C', 7));

        Assert.Equal(TipoResultadoSeleccion.Ok, resultado.Tipo);
        Assert.Same(carrito, resultado.Carrito);
        Assert.Equal([new Asiento('C', 5)], resultado.Liberados);
        Assert.Equal([new Asiento('C', 7)], resultado.Retenidos);
    }

    [Fact]
    public void Reemplazar_LosAsientosPropiosCuentanComoDisponibles()
    {
        var funcion = CrearFuncion();
        var carrito = CarritoDe(funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));

        var resultado = Reemplazar(funcion, carrito, [carrito], new Asiento('C', 5), new Asiento('C', 6), new Asiento('C', 7));

        Assert.Equal(TipoResultadoSeleccion.Ok, resultado.Tipo);
    }

    [Fact]
    public void Reemplazar_AsientoDeOtroCarrito_RechazaYConservaLaSeleccionAnterior()
    {
        var funcion = CrearFuncion();
        var carrito = CarritoDe(funcion, Ahora, new Asiento('C', 5), new Asiento('C', 6));
        var otro = CarritoDe(funcion, Ahora, new Asiento('B', 2));

        var resultado = Reemplazar(funcion, carrito, [carrito, otro], new Asiento('B', 1), new Asiento('B', 2));

        Assert.Equal(TipoResultadoSeleccion.AsientosNoDisponibles, resultado.Tipo);
        Assert.Equal([new Asiento('B', 2)], resultado.Asientos);
        Assert.Equal([new Asiento('C', 5), new Asiento('C', 6)], carrito.Asientos);
    }

    [Fact]
    public void Reemplazar_CarritoNoActivo_DevuelveCarritoNoActivoAntesQueOtrasValidaciones()
    {
        var funcion = CrearFuncion();
        var carrito = CarritoDe(funcion, Ahora, new Asiento('C', 5));
        carrito.Abandonar(Ahora);

        var resultado = Reemplazar(funcion, carrito, [carrito], new Asiento('Z', 99));

        Assert.Equal(TipoResultadoSeleccion.CarritoNoActivo, resultado.Tipo);
        Assert.Equal(EstadoCarrito.Abandonado, resultado.EstadoActual);
    }

    [Fact]
    public void Reemplazar_CarritoVencido_DevuelveCarritoNoActivoExpirado()
    {
        var funcion = CrearFuncion();
        var carrito = CarritoDe(funcion, Ahora.AddMinutes(-5), new Asiento('C', 5));

        var resultado = Reemplazar(funcion, carrito, [carrito], new Asiento('C', 6));

        Assert.Equal(TipoResultadoSeleccion.CarritoNoActivo, resultado.Tipo);
        Assert.Equal(EstadoCarrito.Expirado, resultado.EstadoActual);
    }

    [Fact]
    public void Reemplazar_EnElTope_DescuentaLosAsientosQueElPropioCarritoLibera()
    {
        var funcion = CrearFuncion();
        var otro = CarritoDe(funcion, Ahora, Fila('A', 1, 10));
        var carrito = CarritoDe(funcion, Ahora, Fila('B', 1, 7));

        var resultado = Reemplazar(funcion, carrito, [otro, carrito], Fila('C', 1, 7));

        Assert.Equal(TipoResultadoSeleccion.Ok, resultado.Tipo);
        Assert.Equal(Fila('C', 1, 7), carrito.Asientos);
    }

    [Fact]
    public void Reemplazar_QueSuperariaElTope_RechazaYConservaLaSeleccion()
    {
        var funcion = CrearFuncion();
        var otro = CarritoDe(funcion, Ahora, Fila('A', 1, 10));
        var carrito = CarritoDe(funcion, Ahora, Fila('B', 1, 7));

        var resultado = Reemplazar(funcion, carrito, [otro, carrito], Fila('C', 1, 8));

        Assert.Equal(TipoResultadoSeleccion.FuncionSinCupo, resultado.Tipo);
        Assert.Equal(Fila('B', 1, 7), carrito.Asientos);
    }
}
