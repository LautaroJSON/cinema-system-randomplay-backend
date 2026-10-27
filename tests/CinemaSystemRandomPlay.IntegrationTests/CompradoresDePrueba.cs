using CinemaSystemRandomPlay.Domain.Compradores;

namespace CinemaSystemRandomPlay.IntegrationTests;

internal static class CompradoresDePrueba
{
    public static readonly EmailComprador Ana = EmailComprador.Crear("ana@example.com");
    public static readonly EmailComprador Beto = EmailComprador.Crear("beto@example.com");
}
