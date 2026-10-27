using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.Application.Verificaciones.Ports;

public interface ICodigosDeVerificacion
{
    /// <summary>Genera un código numérico de un solo uso, corto y no predecible.</summary>
    string Generar();

    /// <summary>Hash del par email + código: lo único que se guarda del código.</summary>
    string Hashear(EmailComprador email, string? codigo);
}
