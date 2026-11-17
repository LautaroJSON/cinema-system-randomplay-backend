namespace CinemaSystemRandomPlay.Domain.Pagos;

/// <summary>Genera códigos de entrada únicos y no adivinables.</summary>
public interface IGeneradorDeCodigos
{
    string Generar();
}
