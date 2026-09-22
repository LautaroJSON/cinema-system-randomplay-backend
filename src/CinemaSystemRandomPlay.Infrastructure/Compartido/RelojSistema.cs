using CinemaSystemRandomPlay.Domain.Compartido;

namespace CinemaSystemRandomPlay.Infrastructure.Compartido;

public class RelojSistema : IReloj
{
    public DateTimeOffset Ahora => DateTimeOffset.Now;
}
