using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Verificaciones;

namespace CinemaSystemRandomPlay.Application.Verificaciones.Commands;

public record ConfirmarVerificacionEmailCommand(string? Email, string? Codigo);

public enum EstadoConfirmarVerificacion
{
    Ok,
    EmailInvalido,
    CodigoInvalido,
    CodigoVencido,
    CodigoInvalidado
}

public record ConfirmarVerificacionEmailResultado(EstadoConfirmarVerificacion Estado, CredencialEmitida? Credencial = null)
{
    public static ConfirmarVerificacionEmailResultado Ok(CredencialEmitida credencial) => new(EstadoConfirmarVerificacion.Ok, credencial);

    public static ConfirmarVerificacionEmailResultado Rechazo(EstadoConfirmarVerificacion estado) => new(estado);
}

public class ConfirmarVerificacionEmailCommandHandler
{
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IVerificacionEmailRepository _repository;
    private readonly ICodigosDeVerificacion _codigos;
    private readonly IEmisorDeCredencial _emisor;
    private readonly IReloj _reloj;
    private readonly ReglasVerificacion _reglas;

    public ConfirmarVerificacionEmailCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IVerificacionEmailRepository repository,
        ICodigosDeVerificacion codigos,
        IEmisorDeCredencial emisor,
        IReloj reloj,
        ReglasVerificacion reglas)
    {
        _unidadDeTrabajo = unidadDeTrabajo;
        _repository = repository;
        _codigos = codigos;
        _emisor = emisor;
        _reloj = reloj;
        _reglas = reglas;
    }

    public async Task<ConfirmarVerificacionEmailResultado> Handle(ConfirmarVerificacionEmailCommand command, CancellationToken cancellationToken = default)
    {
        if (!EmailComprador.TryCrear(command.Email, out var email))
            return ConfirmarVerificacionEmailResultado.Rechazo(EstadoConfirmarVerificacion.EmailInvalido);

        // No lanza ante un rechazo: la transacción confirma para persistir el intento fallido.
        return await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            await _repository.BloquearPorEmail(email!, ct);

            var verificacion = await _repository.ObtenerVigente(email!, ct);

            // Sin código vigente (nunca se pidió, ya se usó o se invalidó): indistinguible de un código incorrecto.
            if (verificacion is null)
                return ConfirmarVerificacionEmailResultado.Rechazo(EstadoConfirmarVerificacion.CodigoInvalido);

            var ahora = _reloj.Ahora;
            var resultado = verificacion.Verificar(_codigos.Hashear(email!, command.Codigo), ahora, _reglas.MaximoIntentosFallidos);

            return resultado switch
            {
                ResultadoVerificacion.Correcto => ConfirmarVerificacionEmailResultado.Ok(_emisor.Emitir(email!, ahora)),
                ResultadoVerificacion.Vencido => ConfirmarVerificacionEmailResultado.Rechazo(EstadoConfirmarVerificacion.CodigoVencido),
                ResultadoVerificacion.Invalidado => ConfirmarVerificacionEmailResultado.Rechazo(EstadoConfirmarVerificacion.CodigoInvalidado),
                _ => ConfirmarVerificacionEmailResultado.Rechazo(EstadoConfirmarVerificacion.CodigoInvalido)
            };
        }, cancellationToken);
    }
}
