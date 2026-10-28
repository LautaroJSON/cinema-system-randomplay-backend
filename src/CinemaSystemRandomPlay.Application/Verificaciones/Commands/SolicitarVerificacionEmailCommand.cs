using CinemaSystemRandomPlay.Application.Compartido.Ports;
using CinemaSystemRandomPlay.Application.Verificaciones.Ports;
using CinemaSystemRandomPlay.Domain.Compartido;
using CinemaSystemRandomPlay.Domain.Compradores;
using CinemaSystemRandomPlay.Domain.Verificaciones;

namespace CinemaSystemRandomPlay.Application.Verificaciones.Commands;

public record SolicitarVerificacionEmailCommand(string? Email);

public enum EstadoSolicitarVerificacion
{
    Aceptada,
    EmailInvalido,
    LimiteExcedido
}

public class SolicitarVerificacionEmailCommandHandler
{
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IVerificacionEmailRepository _repository;
    private readonly ICodigosDeVerificacion _codigos;
    private readonly IEnviadorDeEmail _enviador;
    private readonly IReloj _reloj;
    private readonly ReglasVerificacion _reglas;

    public SolicitarVerificacionEmailCommandHandler(
        IUnidadDeTrabajo unidadDeTrabajo,
        IVerificacionEmailRepository repository,
        ICodigosDeVerificacion codigos,
        IEnviadorDeEmail enviador,
        IReloj reloj,
        ReglasVerificacion reglas)
    {
        _unidadDeTrabajo = unidadDeTrabajo;
        _repository = repository;
        _codigos = codigos;
        _enviador = enviador;
        _reloj = reloj;
        _reglas = reglas;
    }

    public async Task<EstadoSolicitarVerificacion> Handle(SolicitarVerificacionEmailCommand command, CancellationToken cancellationToken = default)
    {
        if (!EmailComprador.TryCrear(command.Email, out var email))
            return EstadoSolicitarVerificacion.EmailInvalido;

        var codigo = _codigos.Generar();

        var estado = await _unidadDeTrabajo.EjecutarEnTransaccion(async ct =>
        {
            await _repository.BloquearPorEmail(email!, ct);

            var ahora = _reloj.Ahora;
            var desde = ahora.AddMinutes(-_reglas.VentanaSolicitudesMinutos);

            if (await _repository.ContarDesde(email!, desde, ct) >= _reglas.SolicitudesPorEmail)
                return EstadoSolicitarVerificacion.LimiteExcedido;

            var anterior = await _repository.ObtenerVigente(email!, ct);
            anterior?.Invalidar();

            var verificacion = VerificacionEmail.Crear(email!, _codigos.Hashear(email!, codigo), ahora, _reglas.CodigoMinutosVigencia);
            await _repository.Agregar(verificacion, ct);

            return EstadoSolicitarVerificacion.Aceptada;
        }, cancellationToken);

        // El envío va después de confirmar: ninguna llamada externa dentro de la transacción (Principio VI).
        if (estado == EstadoSolicitarVerificacion.Aceptada)
            await _enviador.Enviar(new MensajeDeVerificacion(email!, codigo), cancellationToken);

        return estado;
    }
}
