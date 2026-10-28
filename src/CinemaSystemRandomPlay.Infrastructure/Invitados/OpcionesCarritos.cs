namespace CinemaSystemRandomPlay.Infrastructure.Invitados;

/// <summary>Configuración de la sección <c>Carritos</c>: retención, topes y límite de creación por origen.</summary>
public class OpcionesCarritos
{
    public const string Seccion = "Carritos";

    public int MinutosRetencion { get; set; } = 5;
    public int MaximoAsientosPorCarrito { get; set; } = 10;
    public int PorcentajeMaximoRetenidoPorFuncion { get; set; } = 50;
    public int CreacionesPorOrigen { get; set; } = 10;
    public int VentanaCreacionMinutos { get; set; } = 10;
}
