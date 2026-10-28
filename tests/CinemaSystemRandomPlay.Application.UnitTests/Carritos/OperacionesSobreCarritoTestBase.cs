using CinemaSystemRandomPlay.Application.Carritos.Ports;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Carritos;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Funciones;
using NSubstitute;
using static CinemaSystemRandomPlay.Application.UnitTests.Carritos.CarritosTestSupport;

namespace CinemaSystemRandomPlay.Application.UnitTests.Carritos;

/// <summary>Dobles comunes a los casos de uso sobre un carrito existente (reemplazar, abandonar, indicar el email).</summary>
public abstract class OperacionesSobreCarritoTestBase
{
    protected readonly UnidadDeTrabajoFake UnidadDeTrabajo = new();
    protected readonly IFuncionRepository FuncionRepository = Substitute.For<IFuncionRepository>();
    protected readonly ICarritoRepository CarritoRepository = Substitute.For<ICarritoRepository>();
    protected readonly IConsultaCarritos ConsultaCarritos = Substitute.For<IConsultaCarritos>();
    protected readonly IOutbox Outbox = Substitute.For<IOutbox>();
    protected readonly IReloj Reloj = Substitute.For<IReloj>();
    protected readonly List<Carrito> Activos = new();
    protected readonly Funcion Funcion;

    protected OperacionesSobreCarritoTestBase()
    {
        Reloj.Ahora.Returns(Ahora);
        (Funcion, _) = CrearFuncion();
        FuncionRepository.ObtenerConSalaBloqueando(Funcion.Id, Arg.Any<CancellationToken>()).Returns(Funcion);
        CarritoRepository.ListarActivosDeFuncion(Funcion.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Activos);
    }

    /// <summary>Registra el carrito en los dobles: repositorio, función, activos y lectura posterior.</summary>
    protected Carrito Existente(DateTimeOffset creadoEn, params Asiento[] asientos)
    {
        var carrito = CarritoDe(Funcion, creadoEn, asientos);
        CarritoRepository.ObtenerFuncionId(carrito.Id, Arg.Any<CancellationToken>()).Returns(Funcion.Id);
        CarritoRepository.ObtenerPorId(carrito.Id, Arg.Any<CancellationToken>()).Returns(carrito);
        ConsultaCarritos.ObtenerPorId(carrito.Id, Arg.Any<CancellationToken>()).Returns(_ => LecturaDe(carrito, Funcion));
        Activos.Add(carrito);
        return carrito;
    }

    protected async Task AssertNoTocaLaBase()
    {
        Assert.Equal(0, UnidadDeTrabajo.Ejecuciones);
        await CarritoRepository.DidNotReceive().ObtenerPorId(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await CarritoRepository.DidNotReceive().ObtenerFuncionId(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Outbox.DidNotReceive().Registrar(Arg.Any<object>());
    }
}
