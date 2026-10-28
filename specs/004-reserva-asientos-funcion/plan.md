# Implementation Plan: Reserva de Asientos de una Función

> **Nota (features 006 y 007):** el mecanismo de identidad y los endpoints de este documento fueron reemplazados. Hoy el comprador es **anónimo**: al elegir asientos obtiene un **carrito** con un token de acceso, retenido 5 minutos, con la selección reemplazable y sin tope por comprador. Las reglas de retención, todo o nada y no doble retención se conservan. Ver [feature 007](../007-carrito-anonimo/spec.md).

**Branch**: `004-reserva-asientos-funcion` | **Date**: 2026-10-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-reserva-asientos-funcion/spec.md`

## Summary

Un Cliente autenticado con rol `Customer` reserva de 1 a 10 asientos de una función. La reserva queda
`PendienteDePago` y retiene los asientos hasta `min(creación + 10 min, inicio de la función)`. Puede
listar sus reservas, consultar una y cancelarla. El mapa de asientos público deja de usar el stub
`SinAsientosOcupados` y resta los asientos retenidos, sin cambiar su contrato.

Enfoque técnico:
- Nuevo agregado `Reserva` y domain service `ReservadorDeAsientos` en `Domain/Reservas`. El reservador
  concentra las reglas entre reservas: asiento no duplicado y tope de 10 por Cliente y función.
- **Concurrencia**: el caso de uso corre en una transacción que primero bloquea la fila de la función
  (`SELECT ... FOR UPDATE`) y serializa los pedidos de esa función.
- **Vencimiento perezoso**: se calcula contra `IReloj` y no hace falta un proceso en segundo plano.
- Outbox mínimo (`OutboxMensajes`): se escribe en la misma transacción al crear y al cancelar. El
  publicador queda para la feature de SignalR.
- Primera autenticación de la API: JWT bearer con la configuración estándar de ASP.NET Core
  (`dotnet user-jwts` en desarrollo), política `Customer` y **fallback cerrado**. Los endpoints
  públicos existentes ya están marcados `[AllowAnonymous]`.

Detalle de cada decisión en [research.md](./research.md).

## Technical Context

**Language/Version**: C# 12 / .NET 10 (fijo por la constitución)

**Primary Dependencies**: ASP.NET Core (Controllers), `Microsoft.EntityFrameworkCore` 10 +
`Npgsql.EntityFrameworkCore.PostgreSQL` 10 (ya en uso). **Nueva**:
`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12 en la Api (misma versión que
`Microsoft.AspNetCore.Mvc.Testing`).

**Storage**: PostgreSQL, misma `CinemaDbContext`. Tablas nuevas: `Reservas`, `ReservaAsientos` (owned)
y `OutboxMensajes`.

**Testing**: xUnit + NSubstitute para Domain/Application; `Testcontainers.PostgreSql` +
`Microsoft.AspNetCore.Mvc.Testing` para integración (ya en uso). Tokens de test con
`JsonWebTokenHandler` y una clave simétrica (llega como dependencia transitiva de JwtBearer).

**Target Platform**: Linux server (API backend, sin frontend propio)

**Project Type**: web-service (backend puro, REST/JSON)

**Performance Goals**: SC-002: 20 o más pedidos simultáneos sobre el mismo asiento se resuelven
correctamente (1 éxito y el resto `409`). Las transacciones de reserva son cortas (bloqueo, una
lectura, dos inserciones), por eso serializar por función es aceptable.

**Constraints**: todo o nada (FR-004); ningún asiento retenido dos veces bajo concurrencia (FR-005);
vencimiento exacto sin procesos externos (FR-011); el contrato del mapa no cambia (FR-020); identidad
solo desde el token (FR-023); endpoints sin política no pueden quedar públicos (FR-025); controllers
sin lógica; I/O async; "ahora" vía `IReloj`.

**Scale/Scope**: 4 Salas sembradas de 40 a 100 asientos; como mucho 10 asientos por reserva; 4 endpoints
nuevos y 1 implementación nueva de un puerto existente.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Arquitectura Hexagonal | PASS. `Reserva`, `ReservadorDeAsientos`, `ReglasReserva`, los eventos e `IReservaRepository` van en `Domain/Reservas`. Casos de uso y puertos (`IUnidadDeTrabajo`, `IOutbox`, `IConsultaReservas`) en `Application`. EF Core, el `FOR UPDATE` y el outbox en `Infrastructure`. JwtBearer solo en `Api`. Domain y Application no conocen ni `ClaimsPrincipal` ni `IOptions`. |
| II. DDD Ligero | PASS. "Un asiento no puede reservarse dos veces" y el tope por Cliente viven en el domain service `ReservadorDeAsientos`. El vencimiento y las transiciones viven en `Reserva` (`EstadoEn`, `Cancelar`), como pide la constitución. `Reserva` no se puede construir inválida. Se reutiliza el Value Object `Asiento`. Los errores son resultados explícitos. Nombres en español. |
| III. Bordes con DTOs | PASS. `ReservaDto`, `AsientoPedidoDto` y los resultados en Application. `CrearReservaRequest`, `ReservaResponse` y `ProblemDetails` en Api, con mapeo manual (`DesdeDto`), igual que `MapaAsientosResponse`. |
| IV. Inversión de dependencias | PASS. Puertos nuevos: `IReservaRepository`, `IUnidadDeTrabajo`, `IOutbox`, `IConsultaReservas`. Implementación nueva de `IOcupacionAsientos`. Registro deliberado: todo `Scoped` (comparten `CinemaDbContext` y su transacción), salvo `ReglasReserva` y `ReservadorDeAsientos`, que no tienen estado y van `Singleton`. |
| V. Testing por criterio | PASS. Ver "Estrategia de tests". Incluye el **test de integración de concurrencia contra Postgres real**, obligatorio sin excepción. |
| VI. Outbox + eventos | PASS con desviación documentada: `ReservaCreada` y `ReservaCancelada` se escriben en `OutboxMensajes` dentro de la misma transacción, y dentro de la transacción no hay llamadas de red. **No** se implementa el proceso en segundo plano que publica (ver Complexity Tracking). |
| VII. Auth explícita | PASS. JWT bearer con proveedor OIDC configurable. La API no gestiona contraseñas. Política `Customer` declarada en cada endpoint nuevo. `FallbackPolicy` = usuario autenticado, así que un endpoint sin política falla cerrado. Los públicos siguen con `[AllowAnonymous]` explícito. |
| VIII. Calidad de código | PASS. Nullable habilitado. Todo async, incluido el bloqueo (`ExecuteSqlInterpolatedAsync`). Los controllers solo leen `sub`, invocan el caso de uso y traducen el resultado a status + `ProblemDetails`. |

**Re-evaluación post-diseño (Phase 1)**: sin cambios. [data-model.md](./data-model.md) mantiene todas
las reglas en Domain. El bloqueo pesimista queda detrás de `IFuncionRepository` e `IUnidadDeTrabajo`,
así que Application no sabe que existe `FOR UPDATE`.
[contracts/reservas-api.md](./contracts/reservas-api.md) no cambia el contrato del mapa. La única
desviación sigue siendo la del Principio VI.

## Estrategia de tests (Principio V)

| Qué | ¿Se testea? | Tipo | Justificación |
|---|---|---|---|
| **Reserva concurrente del mismo asiento** (20 pedidos en paralelo, clientes distintos) | **Sí** | Integración, Postgres real (Testcontainers), vía HTTP | **Obligatorio por constitución.** Las condiciones de carrera solo aparecen contra el motor real. |
| Tope por Cliente bajo concurrencia (mismo Cliente, 2 × 6 asientos en paralelo) | Sí | Integración, Postgres real | El bloqueo es la única barrera de esta regla. Un bug sería silencioso. |
| `ReservadorDeAsientos` (función comenzada, inexistentes, no disponibles, tope, orden de errores, vencidas que no cuentan, `VenceEn` acotado al inicio) | Sí | Unit de Domain con objetos reales | Es el corazón de las reglas de negocio. |
| `Reserva` (construcción inválida, `EstadoEn` en el borde exacto de `VenceEn`, `Cancelar` en cada estado) | Sí | Unit de Domain | Transiciones de estado y manejo de fechas. |
| `Sala.Contiene` | Sí | Unit de Domain | Regla de borde (último asiento de una fila, fila inexistente). Barato. |
| `ReglasReserva` (valores inválidos) | No | — | Constructor que solo valida dos enteros mayores a cero. Ruido. |
| `ReservarAsientosCommandHandler` (validación de forma, película inactiva, no escribe nada si hay error, registra el outbox si hay éxito) | Sí | Unit de Application con fakes | Caso de uso que modifica estado. La validación de forma decide entre `400` y error de dominio. |
| `CancelarReservaCommandHandler` (ajena → no encontrada, no cancelable, outbox) | Sí | Unit de Application | Modifica estado. La regla de "ajena = no encontrada" es de seguridad. |
| Handlers de lectura (`ListarMisReservas`, `ObtenerMiReserva`) | Sí, mínimos | Unit de Application | Estado efectivo calculado con el reloj y orden. Bug silencioso si falla. |
| `ReservaRepository` + `ConsultaReservas` (`ListarVigentesDeFuncion` filtra por estado y `VenceEn`, orden, aislamiento por Cliente) | Sí | Integración, Postgres real | Consultas con filtro por fechas y estado. |
| `ReservasRetienenAsientos` (el mapa refleja reservas vigentes, no vencidas ni canceladas, por función) | Sí | Integración vía `GET /asientos` | Reemplaza el stub. Verifica FR-018 a FR-020 de punta a punta. |
| Endpoints de reservas: códigos y `codigo` de cada error, `Location`, `401`/`403` | Sí | Integración vía HTTP | Es el contrato público y la primera autenticación. |
| Endpoints públicos siguen anónimos | Sí | Integración (un test por endpoint existente sin token) | Regresión barata ante el `FallbackPolicy` (SC-007). |
| Outbox escrito en la misma transacción | Sí, dentro de los tests de integración de crear/cancelar | Integración | Principio VI. Se verifica que haya exactamente un mensaje por operación exitosa y ninguno en los rechazos. |
| DTOs, `Response`, `DesdeDto`, `ClaimsPrincipalExtensions`, registro en DI, configuración de EF, migración | No | — | Código trivial o mapeos 1 a 1, ya cubiertos indirectamente por los tests de integración. |

## Project Structure

### Documentation (this feature)

```text
specs/004-reserva-asientos-funcion/
├── plan.md               # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
│   └── reservas-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks, todavía no generado)
```

### Source Code (repository root)

```text
src/CinemaSystemRandomPlay.Domain/
  Reservas/
    Reserva.cs                     (nuevo: aggregate root)
    EstadoReserva.cs               (nuevo)
    ReglasReserva.cs               (nuevo)
    ReservadorDeAsientos.cs        (nuevo: domain service + ResultadoReserva)
    ResultadoCancelacion.cs        (nuevo)
    IReservaRepository.cs          (nuevo)
    Eventos/ReservaCreada.cs, ReservaCancelada.cs   (nuevos)
  Funciones/
    Sala.cs                        (modificado: Contiene)
    IFuncionRepository.cs          (modificado: ObtenerConSalaBloqueando)

src/CinemaSystemRandomPlay.Application/
  Compartido/Ports/
    IUnidadDeTrabajo.cs, IOutbox.cs          (nuevos)
  Reservas/
    Ports/IConsultaReservas.cs              (nuevo, incluye ReservaLecturaDto)
    Dtos/ReservaDto.cs, AsientoPedidoDto.cs (nuevos)
    Commands/ReservarAsientosCommand.cs     (nuevo: command + handler + resultado)
    Commands/CancelarReservaCommand.cs      (nuevo)
    Queries/ListarMisReservasQuery.cs       (nuevo)
    Queries/ObtenerMiReservaQuery.cs        (nuevo)

src/CinemaSystemRandomPlay.Infrastructure/
  Compartido/
    UnidadDeTrabajoEf.cs           (nuevo)
    OutboxEf.cs                    (nuevo)
  Reservas/
    ReservasRetienenAsientos.cs    (nuevo: IOcupacionAsientos)
  Funciones/
    SinAsientosOcupados.cs         (se elimina)
  Persistence/
    CinemaDbContext.cs             (modificado: DbSet Reservas, OutboxMensajes)
    Outbox/OutboxMensaje.cs, OutboxMensajeEntityConfiguration.cs   (nuevos)
    Reservas/ReservaEntityConfiguration.cs, ReservaRepository.cs, ConsultaReservas.cs   (nuevos)
    Funciones/FuncionRepository.cs (modificado: ObtenerConSalaBloqueando)
  Migrations/
    <timestamp>_AddReservas.cs     (nuevo)

src/CinemaSystemRandomPlay.Api/
  Autenticacion/
    ClaimsPrincipalExtensions.cs   (nuevo: ObtenerClienteId desde sub)
    Politicas.cs                   (nuevo: constante "Customer")
  Compartido/
    ProblemasDeNegocio.cs          (nuevo: helpers de ProblemDetails con `codigo`)
  Modulos/Reservas/
    ReservasController.cs          (nuevo: POST /api/funciones/{funcionId}/reservas;
                                    GET /api/reservas, GET /api/reservas/{id},
                                    POST /api/reservas/{id}/cancelacion)
    Contracts/CrearReservaRequest.cs, ReservaResponse.cs   (nuevos)
  Program.cs                       (modificado: JwtBearer, AddAuthorization con política y
                                    fallback, UseAuthentication, ReglasReserva desde config,
                                    registros nuevos, reemplazo de IOcupacionAsientos)
  appsettings.json                 (modificado: secciones Reservas y Autenticacion)
  CinemaSystemRandomPlay.Api.csproj (modificado: JwtBearer)

tests/CinemaSystemRandomPlay.Domain.UnitTests/
  Reservas/ReservaTests.cs, ReservadorDeAsientosTests.cs   (nuevos)
  Funciones/SalaTests.cs                                   (modificado: Contiene)

tests/CinemaSystemRandomPlay.Application.UnitTests/
  Reservas/ReservarAsientosCommandHandlerTests.cs, CancelarReservaCommandHandlerTests.cs,
           ListarMisReservasQueryHandlerTests.cs            (nuevos)

tests/CinemaSystemRandomPlay.IntegrationTests/
  Compartido/TokensDePrueba.cs                  (nuevo: emite JWT de test)
  Reservas/ReservasApiFactory.cs                (nuevo: Postgres + validación con clave de test)
  Reservas/ReservasControllerTests.cs           (nuevo)
  Reservas/ReservaConcurrenteTests.cs           (nuevo: obligatorio)
  Reservas/ReservaRepositoryTests.cs            (nuevo)
  Funciones/MapaAsientosControllerTests.cs      (modificado: casos con reservas)
  Catalogo/PeliculasControllerTests.cs, Funciones/FuncionesControllerTests.cs
                                                (modificados: un caso "sin token sigue 200")
```

**Structure Decision**: Mismo monolito modular de 4 proyectos con un módulo nuevo, `Reservas`, en cada
capa. `Reserva` es un agregado distinto de `Funcion` y tiene su propio ciclo de vida, por eso no va en
`Funciones`. Las rutas `POST /api/funciones/{funcionId}/reservas` y `/api/reservas/...` comparten
controller porque son el mismo recurso. Los puertos transversales (`IUnidadDeTrabajo`, `IOutbox`)
van en `Application/Compartido/Ports`, porque los va a reutilizar la feature de pago. La
autenticación vive solo en `Api`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principio VI: se escribe el outbox pero no existe el proceso en segundo plano que lo publica | No hay transporte al cual publicar: SignalR y las notificaciones en tiempo real están fuera de alcance de esta feature. Escribir los mensajes ahora cumple la parte transaccional del principio y evita reabrir los casos de uso después. | Publicar a un logger, o a un transporte simulado, agrega código sin consumidor real. No escribir outbox viola el "DEBEN registrarse" del principio. Los mensajes quedan en la tabla con `ProcesadoEn = NULL` hasta que la feature de SignalR agregue el publicador. Esto debe documentarse en el PR, como exige Gobernanza. |
| Vencimiento sin evento: una reserva que vence no genera un mensaje de "asientos liberados" | El vencimiento es perezoso (Decisión 3) y no ocurre ninguna escritura en ese instante. Sin consumidor de eventos, no hace falta. | Un proceso de barrido periódico no aporta nada visible hoy y agrega una pieza en segundo plano. Se suma junto con el publicador en la feature de SignalR. |
