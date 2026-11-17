namespace CinemaSystemRandomPlay.Infrastructure.Outbox;

public class OpcionesOutbox
{
    public const string Seccion = "Outbox";

    /// <summary>Si es <c>false</c>, el proceso en background no corre (los tests llaman a <see cref="ProcesadorDeOutbox.ProcesarLote"/>).</summary>
    public bool Habilitado { get; set; } = true;

    public int IntervaloSegundos { get; set; } = 2;

    public int MaximoIntentos { get; set; } = 8;

    public int TamanoLote { get; set; } = 20;

    /// <summary>Tiempo durante el cual un mensaje tomado no lo toma otra instancia, aunque esta caiga a mitad.</summary>
    public int MinutosLease { get; set; } = 5;
}
