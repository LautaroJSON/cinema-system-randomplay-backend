---

description: "Task list for Reserva de Asientos de una Función"
---

# Tasks: Reserva de Asientos de una Función

**Input**: Design documents from `/specs/004-reserva-asientos-funcion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/reservas-api.md, quickstart.md

**Tests**: Incluidos. La constitución (Principio V) y la "Estrategia de tests" de plan.md los exigen. Se escriben primero y deben fallar antes de implementar. El test de concurrencia contra Postgres real es obligatorio.

**Organization**: Tareas agrupadas por historia de usuario para implementar y probar cada una de forma independiente.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: Historia a la que pertenece (US1..US4)
- Todas las rutas son relativas a la raíz del repositorio

## Path Conventions

Monolito modular de 4 proyectos (nombres de dominio en español, según la constitución):
`src/CinemaSystemRandomPlay.{Domain,Application,Infrastructure,Api}/` y `tests/CinemaSystemRandomPlay.{Domain.UnitTests,Application.UnitTests,IntegrationTests}/`. Abreviaturas usadas abajo: `Domain/` = `src/CinemaSystemRandomPlay.Domain/`, `App/` = `src/CinemaSystemRandomPlay.Application/`, `Infra/` = `src/CinemaSystemRandomPlay.Infrastructure/`, `Api/` = `src/CinemaSystemRandomPlay.Api/`, `DomainTests/` = `tests/CinemaSystemRandomPlay.Domain.UnitTests/`, `AppTests/` = `tests/CinemaSystemRandomPlay.Application.UnitTests/`, `IntTests/` = `tests/CinemaSystemRandomPlay.IntegrationTests/`.

---

## Phase 1: Setup

**Purpose**: Dependencias y configuración base

- [X] T001 Agregar el paquete `Microsoft.AspNetCore.Authentication.JwtBearer` versión 10.0.12 en `Api/CinemaSystemRandomPlay.Api.csproj`
- [X] T002 [P] Agregar las secciones `Reservas` (`MinutosRetencion: 10`, `MaximoAsientosPorCliente: 10`) y `Autenticacion` (`Authority`, `Audience`, `ClaimDeRol`/rol `Customer` según research.md Decisión 7) en `Api/appsettings.json` y los valores de desarrollo en `Api/appsettings.Development.json`
- [X] T003 [P] Ejecutar `dotnet build` y `dotnet test` sobre `CinemaSystemRandomPlay.slnx` para confirmar una línea base verde antes de empezar (las carpetas nuevas `Reservas/`, `Autenticacion/` y `Compartido/` se crean con el primer archivo de cada una) _(Nota: la línea base se verificó con build y los 22 tests unitarios; los de integración no corrían por falta de Docker en el entorno.)_

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Dominio base, puertos transversales, persistencia, autenticación y fallback cerrado. Bloquea todas las historias.

**⚠️ CRITICAL**: Ninguna historia puede empezar hasta completar esta fase

### Dominio

- [X] T004 [P] Crear enum `EstadoReserva` (`PendienteDePago`, `Cancelada`, `Expirada`) en `Domain/Reservas/EstadoReserva.cs`
- [X] T005 [P] Crear record `ReglasReserva(MinutosRetencion, MaximoAsientosPorCliente)` con valores por defecto 10/10 que lanza `DomainException` si algún valor es ≤ 0, en `Domain/Reservas/ReglasReserva.cs`
- [X] T006 [P] Agregar `bool Contiene(Asiento asiento)` a `Sala` (fila con `Letra == asiento.Fila` y `Numero <= CantidadAsientos`) en `Domain/Funciones/Sala.cs`
- [X] T007 Crear aggregate root `Reserva` en `Domain/Reservas/Reserva.cs`: propiedades de data-model.md (`Id`, `FuncionId`, `ClienteId`, `Asientos` ordenados, `Estado`, `CreadaEn`, `VenceEn`, `CanceladaEn`), factory `internal static` que valida `ClienteId` no vacío (≤255), 1..máximo asientos y sin repetidos (`DomainException`), `EstadoEn(ahora)`, `static EstadoEn(guardado, venceEn, ahora)` (borde exacto: `VenceEn <= ahora` ⇒ `Expirada`) y `RetieneAsientosEn(ahora)`. El método `Cancelar` se agrega en US4 (depende de T004)
- [X] T008 [P] Crear `IReservaRepository` (`ListarVigentesDeFuncion(funcionId, ahora)`, `ObtenerPorId(id)`, `Agregar(reserva)`) en `Domain/Reservas/IReservaRepository.cs`
- [X] T009 [P] Crear los records de evento `ReservaCreada` y `ReservaCancelada` (sin `ClienteId`) en `Domain/Reservas/Eventos/ReservaCreada.cs` y `Domain/Reservas/Eventos/ReservaCancelada.cs`
- [X] T010 Agregar `ObtenerConSalaBloqueando(Guid funcionId)` a `Domain/Funciones/IFuncionRepository.cs`

### Application (puertos)

- [X] T011 [P] Crear `IUnidadDeTrabajo` (`EjecutarEnTransaccion<T>(Func<CancellationToken, Task<T>>, CancellationToken)`) en `App/Compartido/Ports/IUnidadDeTrabajo.cs`
- [X] T012 [P] Crear `IOutbox` (`Registrar(object evento)`) en `App/Compartido/Ports/IOutbox.cs`
- [X] T013 [P] Crear `AsientoPedidoDto(Fila, Numero)` en `App/Reservas/Dtos/AsientoPedidoDto.cs` y `ReservaDto` (+ `FuncionReservaDto`) según data-model.md en `App/Reservas/Dtos/ReservaDto.cs`

### Infrastructure (persistencia)

- [X] T014 [P] Crear la entidad `OutboxMensaje` y `OutboxMensajeEntityConfiguration` (tabla `OutboxMensajes`, `Payload` jsonb, índice `(ProcesadoEn, OcurridoEn)`) en `Infra/Persistence/Outbox/`
- [X] T015 [P] Crear `ReservaEntityConfiguration` (tabla `Reservas`, `Estado` como string de 20, `Asientos` como owned en `ReservaAsientos` con PK `(ReservaId, Fila, Numero)`, FK a `Funciones` RESTRICT, índices `(FuncionId, Estado, VenceEn)` y `(ClienteId, CreadaEn)`) en `Infra/Persistence/Reservas/ReservaEntityConfiguration.cs`
- [X] T016 Agregar `DbSet<Reserva> Reservas` y `DbSet<OutboxMensaje> OutboxMensajes` y aplicar las configuraciones en `Infra/Persistence/CinemaDbContext.cs`
- [X] T017 Generar la migración `AddReservas` con `dotnet ef migrations add AddReservas` (proyecto Infrastructure, startup Api) en `Infra/Migrations/` y verificar que el esquema coincide con data-model.md
- [X] T018 [P] Implementar `UnidadDeTrabajoEf` (transacción sobre `CinemaDbContext`, `SaveChanges` + commit, rollback si falla) en `Infra/Compartido/UnidadDeTrabajoEf.cs`
- [X] T019 [P] Implementar `OutboxEf` (serializa el evento a `Payload`, agrega `OutboxMensaje` al contexto sin guardar) en `Infra/Compartido/OutboxEf.cs`
- [X] T020 Implementar `ObtenerConSalaBloqueando` en `Infra/Persistence/Funciones/FuncionRepository.cs` con `ExecuteSqlInterpolatedAsync("SELECT ... FOR UPDATE")` sobre la fila de la función y luego carga de Sala y Sucursal (requiere transacción activa)
- [X] T021 Implementar `ReservaRepository` (`ListarVigentesDeFuncion` filtra `Estado == PendienteDePago && VenceEn > ahora`; `ObtenerPorId`; `Agregar`) en `Infra/Persistence/Reservas/ReservaRepository.cs`

### Api (autenticación)

- [X] T022 [P] Crear `Politicas` (constante `"Customer"`) en `Api/Autenticacion/Politicas.cs`
- [X] T023 [P] Crear `ClaimsPrincipalExtensions.ObtenerClienteId()` (claim `sub`, sin mapeo de claims por defecto) en `Api/Autenticacion/ClaimsPrincipalExtensions.cs`
- [X] T024 [P] Crear helpers de `ProblemDetails` con extensión `codigo` en `Api/Compartido/ProblemasDeNegocio.cs`
- [X] T025 Configurar en `Api/Program.cs`: `AddAuthentication().AddJwtBearer` desde la sección `Autenticacion` (sin remapeo de claims), `AddAuthorization` con política `Customer` y `FallbackPolicy` = usuario autenticado, `UseAuthentication()`/`UseAuthorization()`, registro de `ReglasReserva` desde configuración (Singleton) y de `IUnidadDeTrabajo`, `IOutbox`, `IReservaRepository` (Scoped)
- [X] T026 Marcar `[AllowAnonymous]` explícito en todos los endpoints públicos existentes: `Api/Modulos/Catalogo/PeliculasController.cs`, `Api/Modulos/Funciones/FuncionesController.cs`, `Api/Modulos/Funciones/MapaAsientosController.cs` (FR-024, FR-025) _(Nota: los controllers ya tenían `[AllowAnonymous]` en cada acción pública; no hizo falta tocarlos y quedó cubierto por `EndpointsPublicosTests`.)_
- [X] T027 [P] Crear `TokensDePrueba` (emite JWT con `JsonWebTokenHandler` y clave simétrica; con/sin rol `Customer`, vencido, emisor inválido) en `IntTests/Compartido/TokensDePrueba.cs`
- [X] T028 Crear `ReservasApiFactory` (Postgres con Testcontainers, validación JWT con la clave de test, `IReloj` controlable, sembrado de funciones/Salas) en `IntTests/Reservas/ReservasApiFactory.cs`, tomando como modelo `IntTests/Funciones/FuncionesApiFactory.cs` _(Nota: usa `PostgresDePrueba`, que acepta `CINEMA_TEST_POSTGRES` como alternativa a Docker; las factories y repository tests existentes también.)_
- [X] T029 [P] Agregar a `IntTests/Catalogo/PeliculasControllerTests.cs` y `IntTests/Funciones/FuncionesControllerTests.cs` un caso "sin token sigue 200" por endpoint público (SC-007; usar la factory nueva si hace falta el esquema de auth) _(Nota: en lugar de editar los tests existentes, se cubrió con `IntTests/Reservas/EndpointsPublicosTests.cs`.)_

**Checkpoint**: Fundación lista. Compila, migración creada, endpoints públicos siguen anónimos.

---

## Phase 3: User Story 1 - Reservar asientos de una función (Priority: P1) 🎯 MVP

**Goal**: Un Cliente `Customer` reserva 1..10 asientos todo-o-nada; la reserva queda `PendienteDePago` con `VenceEn = min(creación+10 min, inicio)`, sin sobreventa bajo concurrencia.

**Independent Test**: Con un token `Customer`, `POST /api/funciones/{id}/reservas` sobre asientos libres devuelve `201` + `Location`; repetir sobre los mismos asientos devuelve `409 asientos-no-disponibles`; 20 pedidos concurrentes sobre un asiento producen 1×`201` y 19×`409`.

### Tests for User Story 1 ⚠️ (escribir primero, deben fallar)

- [X] T030 [P] [US1] Tests de `ReservadorDeAsientos` (función comenzada, inexistentes, no disponibles, límite por Cliente con `asientosRestantes`, orden de errores, vencidas no cuentan, `VenceEn` acotado al inicio de la función, éxito) en `DomainTests/Reservas/ReservadorDeAsientosTests.cs`
- [X] T031 [P] [US1] Tests de `Reserva` (construcción inválida, `EstadoEn` en el borde exacto de `VenceEn`) en `DomainTests/Reservas/ReservaTests.cs`
- [X] T032 [P] [US1] Tests de `Sala.Contiene` (último asiento de una fila, fila inexistente, número excedido) en `DomainTests/Funciones/SalaTests.cs`
- [X] T033 [P] [US1] Tests de `ReservarAsientosCommandHandler` con fakes (forma inválida → `PedidoInvalido`, película inactiva/función inexistente → `FuncionNoEncontrada`, error de dominio no escribe nada, éxito agrega reserva y registra `ReservaCreada`) en `AppTests/Reservas/ReservarAsientosCommandHandlerTests.cs`
- [X] T034 [P] [US1] Tests de integración HTTP de `POST /api/funciones/{funcionId}/reservas`: `201`+`Location`, cada `codigo` de error (`400`,`404`,`410`,`422`,`409` ×2), `401` sin token/token inválido, `403` sin rol, orden de errores, un único `OutboxMensaje` por éxito y ninguno en rechazos, en `IntTests/Reservas/ReservasControllerTests.cs`
- [X] T035 [P] [US1] **Obligatorio**: tests de concurrencia contra Postgres real en `IntTests/Reservas/ReservaConcurrenteTests.cs`: 20 pedidos paralelos de clientes distintos sobre el mismo asiento ⇒ 1 `201` y 19 `409` (SC-002), y mismo Cliente con 2 pedidos paralelos de 6 asientos ⇒ solo uno prospera (tope 10)
- [X] T036 [P] [US1] Tests de integración de `ReservaRepository.ListarVigentesDeFuncion` (filtra estado y `VenceEn`, aislamiento por función) en `IntTests/Reservas/ReservaRepositoryTests.cs`

### Implementation for User Story 1

- [X] T037 [US1] Implementar `ReservadorDeAsientos` y `ResultadoReserva` (Tipo + datos por caso; reglas 1–5 en el orden de data-model.md; listas de error ordenadas y sin repetidos; `VenceEn` calculado) en `Domain/Reservas/ReservadorDeAsientos.cs`
- [X] T038 [US1] Implementar `ReservarAsientosCommand`, handler y `ReservarAsientosResultado` en `App/Reservas/Commands/ReservarAsientosCommand.cs` (validación de forma, transacción con `ObtenerConSalaBloqueando`, película activa, `ListarVigentesDeFuncion`, reservador, `Agregar` + `IOutbox.Registrar(ReservaCreada)`, arma `ReservaDto`)
- [X] T039 [P] [US1] Crear `CrearReservaRequest` y `ReservaResponse` (con `DesdeDto`, mapeo manual como `MapaAsientosResponse`) en `Api/Modulos/Reservas/Contracts/`
- [X] T040 [US1] Crear `ReservasController` con `POST /api/funciones/{funcionId:guid}/reservas` (`[Authorize(Policy = Politicas.Customer)]`, cliente desde `sub`, traduce resultados a `201`/`ProblemDetails` con `codigo` según contracts/reservas-api.md) en `Api/Modulos/Reservas/ReservasController.cs`
- [X] T041 [US1] Registrar en `Api/Program.cs` `ReservadorDeAsientos` (Singleton) y `ReservarAsientosCommandHandler` (Scoped)
- [X] T042 [US1] Ejecutar los tests T030–T036 y corregir hasta que pasen, incluido el de concurrencia

**Checkpoint**: US1 funcional y probada de forma independiente (MVP).

---

## Phase 4: User Story 2 - Ver en el mapa los asientos retenidos (Priority: P2)

**Goal**: `GET /api/funciones/{id}/asientos` resta los asientos de reservas vigentes, sin cambiar su contrato ni su acceso público.

**Independent Test**: Crear una reserva sobre B3/B4, consultar el mapa y verificar 48 de 50 disponibles; tras vencer o cancelar vuelven a aparecer; otra función de la misma Sala no se ve afectada.

### Tests for User Story 2 ⚠️

- [X] T043 [US2] Agregar casos a `IntTests/Funciones/MapaAsientosControllerTests.cs` (o a `IntTests/Reservas/` con `ReservasApiFactory` si el mapa necesita el esquema de auth): reserva vigente resta asientos y cantidad, reserva vencida (avanzando el reloj) no resta, reserva de otra función no afecta, forma de respuesta idéntica y sin token sigue `200`. El caso "cancelada" se agrega en US4

### Implementation for User Story 2

- [X] T044 [US2] Implementar `ReservasRetienenAsientos : IOcupacionAsientos` (asientos de reservas `PendienteDePago` con `VenceEn > ahora` de la función) en `Infra/Reservas/ReservasRetienenAsientos.cs`, tomando la firma de `App/Funciones/Ports/IOcupacionAsientos.cs`
- [X] T045 [US2] En `Api/Program.cs` reemplazar el registro de `SinAsientosOcupados` por `ReservasRetienenAsientos` (Scoped) y eliminar `Infra/Funciones/SinAsientosOcupados.cs`
- [X] T046 [US2] Ejecutar T043 y las pruebas existentes del mapa; corregir cualquier test de la feature 003 que dependa del stub

**Checkpoint**: US1 y US2 funcionan; el mapa refleja las reservas.

---

## Phase 5: User Story 3 - Consultar mis reservas (Priority: P3)

**Goal**: El Cliente lista sus reservas (más recientes primero) y consulta una puntual, con estado efectivo y contexto de la función; las ajenas son `404`.

**Independent Test**: Con dos Clientes y reservas en distintos estados, `GET /api/reservas` devuelve solo las propias ordenadas por `creadaEn` desc con `Expirada` calculada; `GET /api/reservas/{id}` ajena devuelve `404 reserva-no-encontrada`.

### Tests for User Story 3 ⚠️

- [X] T047 [P] [US3] Tests de `ListarMisReservasQueryHandler` y `ObtenerMiReservaQueryHandler` (estado efectivo con el reloj, orden desc, ajena → `ReservaNoEncontrada`) en `AppTests/Reservas/ListarMisReservasQueryHandlerTests.cs`
- [X] T048 [P] [US3] Tests de integración de `ConsultaReservas` (aislamiento por Cliente, orden, datos de contexto de función) en `IntTests/Reservas/ReservaRepositoryTests.cs`
- [X] T049 [P] [US3] Tests HTTP de `GET /api/reservas` (`[]` sin reservas, orden, `401`/`403`) y `GET /api/reservas/{id}` (`200`, `404` ajena indistinguible de inexistente) en `IntTests/Reservas/ReservasControllerTests.cs`

### Implementation for User Story 3

- [X] T050 [P] [US3] Crear puerto `IConsultaReservas` y `ReservaLecturaDto` en `App/Reservas/Ports/IConsultaReservas.cs`
- [X] T051 [US3] Implementar `ConsultaReservas` (proyección con join a Función, Película, Sala, Sucursal; filtro por `ClienteId`; `CreadaEn` desc) en `Infra/Persistence/Reservas/ConsultaReservas.cs`
- [X] T052 [P] [US3] Implementar `ListarMisReservasQuery` + handler en `App/Reservas/Queries/ListarMisReservasQuery.cs` (aplica `Reserva.EstadoEn(guardado, venceEn, ahora)`)
- [X] T053 [P] [US3] Implementar `ObtenerMiReservaQuery` + handler en `App/Reservas/Queries/ObtenerMiReservaQuery.cs`
- [X] T054 [US3] Agregar `GET /api/reservas` y `GET /api/reservas/{reservaId:guid}` a `Api/Modulos/Reservas/ReservasController.cs` (política `Customer`, `404 reserva-no-encontrada`)
- [X] T055 [US3] Registrar `IConsultaReservas` y los handlers de consulta (Scoped) en `Api/Program.cs`

**Checkpoint**: US1–US3 funcionan de forma independiente.

---

## Phase 6: User Story 4 - Cancelar una reserva pendiente (Priority: P3)

**Goal**: El Cliente cancela una reserva propia pendiente y vigente; los asientos quedan libres de inmediato; expiradas o ya canceladas responden `409` con el estado actual.

**Independent Test**: Crear reserva, `POST /api/reservas/{id}/cancelacion` ⇒ `200` con `Cancelada`; el mapa vuelve a mostrar los asientos; cancelar de nuevo ⇒ `409 reserva-no-cancelable` con `estadoActual`.

### Tests for User Story 4 ⚠️

- [X] T056 [P] [US4] Agregar a `DomainTests/Reservas/ReservaTests.cs` tests de `Cancelar` en cada estado (pendiente vigente, vencida, ya cancelada; sin cambios cuando es `NoCancelable`)
- [X] T057 [P] [US4] Tests de `CancelarReservaCommandHandler` (ajena/inexistente → `ReservaNoEncontrada`, `NoCancelable`, éxito registra `ReservaCancelada`) en `AppTests/Reservas/CancelarReservaCommandHandlerTests.cs`
- [X] T058 [P] [US4] Tests HTTP de `POST /api/reservas/{id}/cancelacion` (`200`, `404`, `409` con `estadoActual`, `401`/`403`, outbox exacto) y el caso "cancelada libera asientos en el mapa" en `IntTests/Reservas/ReservasControllerTests.cs` e `IntTests/Funciones/MapaAsientosControllerTests.cs`

### Implementation for User Story 4

- [X] T059 [US4] Agregar `Cancelar(ahora)` y `ResultadoCancelacion` (éxito o `NoCancelable(estadoActual)`) en `Domain/Reservas/Reserva.cs` y `Domain/Reservas/ResultadoCancelacion.cs`
- [X] T060 [US4] Implementar `CancelarReservaCommand`, handler y resultado en `App/Reservas/Commands/CancelarReservaCommand.cs` (transacción, sin bloquear la función, `IOutbox.Registrar(ReservaCancelada)`)
- [X] T061 [US4] Agregar `POST /api/reservas/{reservaId:guid}/cancelacion` a `Api/Modulos/Reservas/ReservasController.cs` y registrar el handler en `Api/Program.cs`

**Checkpoint**: Las cuatro historias funcionan de forma independiente.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T062 [P] Actualizar `Api/CinemaSystemRandomPlay.Api.http` con los 4 endpoints nuevos y el header `Authorization`
- [X] T063 [P] Actualizar `README.md` con la feature de reservas, la configuración `Reservas`/`Autenticacion` y la nota de desviación del Principio VI (outbox sin publicador, vencimiento sin evento) que debe ir también en el PR
- [X] T064 Ejecutar la validación de `specs/004-reserva-asientos-funcion/quickstart.md` de punta a punta (incluye `dotnet user-jwts` en desarrollo) _(Nota: los pasos 1–10 y 12 están cubiertos por tests de integración; se verificó además el arranque en Development y los `401` sin token. No se ejecutó el flujo con `dotnet user-jwts`.)_
- [X] T065 Ejecutar `dotnet build` (sin warnings de nullable) y `dotnet test` de toda la solución

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** bloquea todo → **US1 (3)** → resto
- **US2 (4)**: usa `IReservaRepository` (fundacional) pero sus tests necesitan crear reservas, por lo que se hace después de US1
- **US3 (5)** y **US4 (6)**: dependen de US1 (necesitan reservas) y son independientes entre sí; US4 modifica `Reserva.cs` y `ReservasController.cs`, igual que otras fases, así que no deben hacerse a la vez en archivos compartidos
- **Polish (7)**: al final

### Within Each Story

- Tests primero (deben fallar) → dominio → aplicación → infraestructura → API
- T007 antes de T031/T037; T010 antes de T020; T016 antes de T017; T025 antes de T040; T028 antes de los tests de integración

### Parallel Opportunities

- Fundacional: T004, T005, T006, T008, T009, T011–T015, T018, T019, T022–T024, T027 son archivos distintos
- US1: todos los tests T030–T036 en paralelo; T039 en paralelo con T037/T038
- US3: T047–T049 y T050/T052/T053 en paralelo
- Tras US1, US3 y US4 pueden asignarse a personas distintas coordinando `ReservasController.cs`

## Parallel Example: User Story 1

```bash
Task: "Tests de ReservadorDeAsientos en tests/CinemaSystemRandomPlay.Domain.UnitTests/Reservas/ReservadorDeAsientosTests.cs"
Task: "Tests de ReservarAsientosCommandHandler en tests/CinemaSystemRandomPlay.Application.UnitTests/Reservas/ReservarAsientosCommandHandlerTests.cs"
Task: "Tests de concurrencia en tests/CinemaSystemRandomPlay.IntegrationTests/Reservas/ReservaConcurrenteTests.cs"
```

## Implementation Strategy

### MVP First (US1)

1. Setup + Foundational → 2. US1 → 3. **Detenerse y validar** (incluido el test de concurrencia) → 4. Demo

### Incremental Delivery

1. Fundación → US1 (MVP: retener asientos)
2. US2 (el mapa deja de mentir) → 3. US3 (ver mis reservas) → 4. US4 (cancelar)
5. Cada historia suma valor sin romper las anteriores

## Notes

- Controllers sin lógica: solo `sub` → caso de uso → status + `ProblemDetails`
- "Ahora" siempre vía `IReloj`; todo I/O async
- Commit por tarea o grupo lógico
