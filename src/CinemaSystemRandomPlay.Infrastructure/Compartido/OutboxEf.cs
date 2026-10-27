using System.Text.Json;
using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Infrastructure.Persistence;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Outbox;

namespace CinemaSystemRandomPlay.Infrastructure.Compartido;

public class OutboxEf : IOutbox
{
    private readonly CinemaDbContext _dbContext;
    private readonly IReloj _reloj;

    public OutboxEf(CinemaDbContext dbContext, IReloj reloj)
    {
        _dbContext = dbContext;
        _reloj = reloj;
    }

    public void Registrar(object evento)
    {
        _dbContext.OutboxMensajes.Add(new OutboxMensaje
        {
            Id = Guid.NewGuid(),
            Tipo = evento.GetType().Name,
            Payload = JsonSerializer.Serialize(evento, evento.GetType()),
            OcurridoEn = _reloj.Ahora
        });
    }
}
