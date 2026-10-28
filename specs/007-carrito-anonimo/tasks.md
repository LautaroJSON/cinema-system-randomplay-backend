---

description: "Task list for Carrito Anónimo con Retención Corta"
---

# Tasks: Carrito Anónimo con Retención Corta

**Input**: Design documents from `/specs/007-carrito-anonimo/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/carritos-api.md, quickstart.md

**Tests**: incluidos según la "Estrategia de tests" de plan.md y el Principio V: invariantes, casos de uso que modifican estado, seguridad de acceso, persistencia y concurrencia contra Postgres real. Se escriben antes de la implementación que cubren y deben fallar primero. El test de concurrencia es obligatorio y suma los casos de reemplazo y de dos carritos que disputan un asiento. No llevan tests: DTOs, requests/responses, registro en el DI, opciones, `DesafioAntiBotsDesactivado`, `EnviadorDeEmail*`, `EmisorDeTokenDeCarritoJwt` por separado y la documentación.

**Organization**: tareas agrupadas por historia de usuario, en orden de prioridad: US1 (P1), US2 (P2), US4 (P2) y US3 (P3). La Fase 2 concentra el retiro de la 006 y el renombre `Reserva` → `Carrito`, que son atómicos: borrar tipos y renombrar el agregado rompe la compilación de todo lo que los usa.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece (US1..US4)
- Todas las rutas son relativas a la raíz del repositorio

## Path Conventions

`Domain/` = `src/CinemaSystemRandomPlay.Domain/`, `App/` = `src/CinemaSystemRandomPlay.Application/`, `Infra/` = `src/CinemaSystemRandomPlay.Infrastructure/`, `Api/` = `src/CinemaSystemRandomPlay.Api/`, `DomainTests/` = `tests/CinemaSystemRandomPlay.Domain.UnitTests/`, `AppTests/` = `tests/CinemaSystemRandomPlay.Application.UnitTests/`, `IntTests/` = `tests/CinemaSystemRandomPlay.IntegrationTests/`.

---

## Phase 1: Setup

**Purpose**: línea base y configuración

- [X] T001 Ejecutar `dotnet build` y `dotnet test` sobre la solución en la rama `007-carrito-anonimo` para confirmar una línea base verde (anotar los conteos: 118 Domain, 66 Application y 144 Integración)
- [X] T002 [P] En `Api/appsettings.json`: reemplazar la sección `Reservas` por `Carritos` (`MinutosRetencion: 5`, `MaximoAsientosPorCarrito: 10`, `PorcentajeMaximoRetenidoPorFuncion: 50`, `CreacionesPorOrigen: 10`, `VentanaCreacionMinutos: 10`) y dejar en `Invitados` solo `TokenMinutosVigencia: 60` (sin `ClaveFirma`)

---

## Phase 2: Foundational — retiro de la 006 y renombre a `Carrito`

**Purpose**: eliminar la verificación por email, convertir `Reserva` en `Carrito` en todas las capas, emitir y validar el token del carrito y migrar la base. Al terminar, la solución compila y los tests que sobreviven pasan. Todavía no hay endpoints de carrito: llegan en US1.

**⚠️ CRITICAL**: ninguna historia puede empezar antes de terminar esta fase.

### Retiro de la verificación por email

- [X] T003 Eliminar `Domain/Verificaciones/` (`VerificacionEmail`, `EstadoVerificacion`, `ResultadoVerificacion`, `ReglasVerificacion`, `IVerificacionEmailRepository`), `App/Verificaciones/Commands/` y `App/Verificaciones/Ports/ICodigosDeVerificacion.cs` e `IEmisorDeCredencial.cs`
- [X] T004 Mover `App/Verificaciones/Ports/IEnviadorDeEmail.cs` a `App/Compartido/Ports/IEnviadorDeEmail.cs` con el record genérico `MensajeDeEmail(string Destinatario, string Asunto, string Cuerpo)` en lugar de `MensajeDeVerificacion`, y adaptar `Infra/Invitados/EnviadorDeEmailRegistrador.cs` (registra destinatario y asunto, no el cuerpo) y `Infra/Invitados/EnviadorDeEmailNoConfigurado.cs` (el mensaje menciona la feature de pago)
- [X] T005 Eliminar `Infra/Invitados/CodigosDeVerificacionHmac.cs`, `Infra/Invitados/EmisorDeCredencialJwt.cs`, `Infra/Persistence/Verificaciones/` y el `DbSet` de verificaciones de `Infra/Persistence/CinemaDbContext.cs`
- [X] T006 Eliminar `Api/Modulos/Verificaciones/` (controller y contracts)
- [X] T007 [P] Eliminar los tests de comportamiento retirado: `DomainTests/Verificaciones/`, `AppTests/Verificaciones/`, `IntTests/Invitados/VerificacionEmailApiTests.cs`, `IntTests/Invitados/VerificacionEmailRepositoryTests.cs`, `IntTests/Invitados/LimitePorOrigenTests.cs` (se reescribe en US4), `IntTests/Reservas/ReservasInvitadoTests.cs` y `AppTests/Reservas/ListarMisReservasQueryHandlerTests.cs`

### Dominio `Carrito`

- [X] T008 Crear `Domain/Carritos/EstadoCarrito.cs` (`Activo`, `Expirado`, `Abandonado`) y `Domain/Carritos/ReglasCarrito.cs` (`MinutosRetencion` = 5, `MaximoAsientosPorCarrito` = 10, `PorcentajeMaximoRetenidoPorFuncion` = 50 entre 1 y 100, todos validados con `DomainException`; `TopeRetenidoPara(int totalAsientos) = Math.Max(1, total * porcentaje / 100)`), según data-model.md
- [X] T009 Crear `Domain/Carritos/Carrito.cs` a partir de `Domain/Reservas/Reserva.cs`: campos `Id`, `FuncionId`, `Asientos` (`_asientos`), `Estado`, `CreadoEn`, `VenceEn`, `AbandonadoEn`, `EmailEntrega` (`EmailComprador?`), `PrecioUnitario` y `Total`; `internal static Crear(...)`; `EstadoEn(ahora)` (también la estática con estado guardado y vencimiento) y `RetieneAsientosEn(ahora)`. Todavía sin reemplazo, abandono ni email
- [X] T010 Crear `Domain/Carritos/AsignadorDeAsientos.cs` (con `TipoResultadoSeleccion` y `ResultadoSeleccion`) a partir de `ReservadorDeAsientos`: operación `Crear(funcion, carritosActivos, pedidos, ahora, reglas)` con las validaciones 2 a 4 de data-model.md (función comenzada, inexistentes, no disponibles) y vencimiento `min(ahora + retención, inicio)`. Sin tope por comprador. El tope global llega en US4
- [X] T011 [P] Crear `Domain/Carritos/ICarritoRepository.cs` (`ListarActivosDeFuncion(funcionId, ahora)`, `ObtenerPorId(id)`, `ObtenerFuncionId(id)` sin seguimiento, `Agregar(carrito)`) y `Domain/Carritos/Eventos/CarritoCreado.cs` (`CarritoId`, `FuncionId`, `Asientos`, `VenceEn`, `OcurridoEn`, `PrecioUnitario`, `Total`, sin email)
- [X] T012 Eliminar `Domain/Reservas/` completo y actualizar las referencias del dominio
- [X] T013 [P] Adaptar los tests de dominio: `DomainTests/Reservas/ReservaTests.cs` → `DomainTests/Carritos/CarritoTests.cs` (vencimiento fijo con el mínimo entre retención e inicio, estado efectivo y validaciones de `Crear`); `ReservadorDeAsientosTests.cs` → `DomainTests/Carritos/AsignadorDeAsientosTests.cs` (casos de `Crear`; quitar los del tope por comprador); `ReservaFactory.cs` → `CarritoFactory.cs`; nuevo `DomainTests/Carritos/ReglasCarritoTests.cs` (validación y `TopeRetenidoPara` con 40 → 20, 41 → 20, 1 → 1, porcentaje 100). Revisar si `DomainTests/CompradoresDePrueba.cs` sigue en uso y eliminarlo si no

### Application

- [X] T014 Crear `App/Carritos/Dtos/` (`AsientoPedidoDto`, `FuncionCarritoDto`, `CarritoDto` con `EmailEntrega`) y `App/Carritos/Ports/IConsultaCarritos.cs` (`ObtenerPorId(carritoId)` → `CarritoLecturaDto?` con el contexto de la función, como `ReservaLecturaDto`) a partir de los de `App/Reservas/`
- [X] T015 [P] Crear `App/Carritos/Ports/IEmisorDeTokenDeCarrito.cs` (`TokenDeCarrito Emitir(Guid carritoId, DateTimeOffset ahora)` con `record TokenDeCarrito(string Valor, DateTimeOffset VenceEn)`) y `App/Carritos/Ports/IDesafioAntiBots.cs` (`Task<bool> Verificar(string? respuesta, string? origen, CancellationToken)`), según research.md, Decisiones 1 y 6
- [X] T016 Eliminar `App/Reservas/` y sus tests de aplicación (`AppTests/Reservas/`), conservando la idea de `ReservasTestSupport.cs` como `AppTests/Carritos/CarritosTestSupport.cs` (dobles de unidad de trabajo, reloj, outbox, funciones y películas). Revisar si `AppTests/CompradoresDePrueba.cs` sigue en uso y eliminarlo si no

### Infrastructure y migración

- [X] T017 Reemplazar `Infra/Persistence/Reservas/` por `Infra/Persistence/Carritos/`: `CarritoEntityConfiguration.cs` (tablas `Carritos` y `CarritoAsientos`, columnas e índice de data-model.md, `EmailEntrega` nullable con conversión a `EmailComprador`), `CarritoRepository.cs` (`ListarActivosDeFuncion` filtra `Estado == Activo && VenceEn > ahora`) y `ConsultaCarritos.cs`; actualizar el `DbSet` en `Infra/Persistence/CinemaDbContext.cs`
- [X] T018 Renombrar `Infra/Reservas/ReservasRetienenAsientos.cs` a `Infra/Carritos/CarritosRetienenAsientos.cs` (carritos activos y no vencidos de la función)
- [X] T019 [P] Actualizar `Infra/Invitados/OpcionesInvitados.cs`: dejar `ClaveFirma`, `TokenMinutosVigencia` (60), `Emisor`, `Audiencia`, `LargoMinimoClave`, `TipoToken = "portador-carrito"` y `ClaimCarrito = "carrito"`; quitar las opciones de verificación. Crear `Infra/Invitados/OpcionesCarritos.cs` (sección `Carritos`: retención, máximo, porcentaje, creaciones por origen, ventana)
- [X] T020 [P] Crear `Infra/Invitados/EmisorDeTokenDeCarritoJwt.cs` (implementa `IEmisorDeTokenDeCarrito`: JWT HS256 con `sub` y `carrito` = id, `tipo`, `jti` = `Guid.NewGuid()`, `iat` y `exp`) y `Infra/Invitados/DesafioAntiBotsDesactivado.cs` (devuelve `true`)
- [X] T021 Revisar `Infra/Persistence/DevDataSeeder.cs` y quitar cualquier referencia a reservas o verificaciones
- [X] T022 Generar la migración `ReemplazarReservasPorCarritos` con `export PATH="$PATH:$HOME/.dotnet/tools"` e `Invitados__ClaveFirma=<32+ bytes>`, con `dotnet ef migrations add ReemplazarReservasPorCarritos --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api`, y editar `Up` para que, **antes** de cualquier DDL, aplique la guarda de `AddCompradorEmailEnReservas` (misma variable `CINEMA_PERMITIR_DESCARTE_RESERVAS`, mensaje propio) y borre los mensajes `ReservaCreada` y `ReservaCancelada` del outbox. Después elimina `ReservaAsientos`, `Reservas` y `VerificacionesEmail` y crea `Carritos` y `CarritoAsientos`. `Down` recrea las tablas vacías de la 006. Documentar todo con un `<summary>` (research.md, Decisión 9)
- [X] T023 Test de integración de la migración en `IntTests/Carritos/MigracionCarritosTests.cs` (con el estilo de `IntTests/Reservas/MigracionCompradorEmailTests.cs`): sin la variable y con reservas sembradas tras `AddVerificacionesEmail`, falla y no borra nada; con la variable, descarta las reservas, sus asientos y su outbox, elimina `VerificacionesEmail` y deja `Carritos` y `CarritoAsientos` vacías; con la base vacía, aplica sin la variable. Mover `MigracionCompradorEmailTests.cs` a `IntTests/Carritos/` sin cambiar su lógica y verificar que sigue pasando

### Api y autenticación

- [X] T024 En `Api/Autenticacion/`: `Politicas.PortadorDeCarrito` reemplaza a `InvitadoVerificado`; `ClaimsPrincipalExtensions.ObtenerCarritoId()` devuelve `Guid?` desde el claim `carrito` (reemplaza a `ObtenerComprador`); `EsquemasDeAutenticacion` actualiza el comentario
- [X] T025 En `Api/Program.cs`: quitar los registros de verificación y de reservas; `OnTokenValidated` del esquema `Invitado` exige `tipo == TipoToken` y un claim `carrito` que sea un Guid; la política `PortadorDeCarrito` exige esquema `Invitado`, usuario autenticado, `tipo` y `carrito`; enlazar `OpcionesCarritos` y registrar `ReglasCarrito` (Singleton, desde `OpcionesCarritos`), `AsignadorDeAsientos` (Singleton), `ICarritoRepository` y `IConsultaCarritos` (Scoped), `IOcupacionAsientos` → `CarritosRetienenAsientos`, `IEmisorDeTokenDeCarrito` (Singleton), `IDesafioAntiBots` → `DesafioAntiBotsDesactivado` (Singleton) e `IEnviadorDeEmail` como hoy. Eliminar `Api/Modulos/Reservas/`
- [X] T026 Adaptar el soporte de tests de integración: `IntTests/Reservas/ReservasApiFactory.cs` → `IntTests/Carritos/CarritosApiFactory.cs` (sube `Carritos:CreacionesPorOrigen` a 1000; `LimpiarDatos` usa `Carritos`; conserva `RelojDePrueba` y `ConfiguracionAdicional`); `ReservasTestHelpers.cs` → `CarritosTestHelpers.cs`; `IntTests/Compartido/TokensDePrueba.cs`: `Invitado(email, …)` pasa a `Carrito(Guid carritoId, vencido, claveFirma, emisor, tipo, conClaimCarrito)`; adaptar `IntTests/Invitados/EnviadorDeEmailEnMemoria.cs` a `MensajeDeEmail`; actualizar `Catalogo/CatalogoApiFactory.cs` y `Funciones/FuncionesApiFactory.cs` si referencian opciones eliminadas. Revisar si `IntTests/CompradoresDePrueba.cs` sigue en uso y eliminarlo si no
- [X] T027 Ejecutar `dotnet build` (sin advertencias nuevas) y `dotnet test`, y dejar en verde los tests que sobreviven (los de API de reservas se reescriben en US1, así que hasta entonces se excluyen o se borran junto con `ReservasControllerTests`, `ReservasAislamientoTests`, `EndpointsPublicosTests`, `MapaConReservasTests`, `ReservaRepositoryTests` y `ReservaConcurrenteTests`, cuyos casos se reescriben en las fases siguientes)

**Checkpoint**: la solución compila, la migración está probada y no queda rastro de la verificación por email.

---

## Phase 3: User Story 1 — Elegir asientos y retenerlos sin identificarme (Priority: P1) 🎯 MVP

**Goal**: crear un carrito sin credencial, recibir su token y consultarlo con ese token; el mapa refleja lo retenido.

**Independent Test**: `POST /api/funciones/{id}/carritos` sin credencial → `201` con token; el mapa ya no muestra esos asientos; `GET /api/carritos/{id}` con el token muestra el carrito; con un token ajeno da `404` y sin token `401`; al vencer, el estado es `Expirado` y los asientos se liberan.

### Tests for User Story 1

- [X] T028 [P] [US1] Tests de aplicación en `AppTests/Carritos/CrearCarritoCommandHandlerTests.cs`: pedido inválido (vacío, más del máximo, repetidos, mal formado) sin abrir transacción; función inexistente o película inactiva; función comenzada; inexistentes; no disponibles; ok registra `CarritoCreado` sin email y devuelve un token del emisor; el desafío se evalúa antes de la transacción y, si rechaza, no hay transacción, outbox ni repositorio
- [X] T029 [P] [US1] Tests de aplicación en `AppTests/Carritos/ObtenerCarritoQueryHandlerTests.cs`: id distinto del claim → no encontrado **sin consultar** `IConsultaCarritos`; igual → devuelve el estado efectivo (incluido `Expirado`)
- [X] T030 [P] [US1] Tests de API en `IntTests/Carritos/CarritosControllerTests.cs`: crear `201` con `Location`, `carrito`, `token` y `tokenVenceEn`, con `venceEn` = creación + 5 min o el inicio si es antes; los errores `400`, `404 funcion-no-encontrada`, `409 asientos-no-disponibles` con lista, `410` y `422` con lista; `GET` con su token `200`; un solo `CarritoCreado` en el outbox por éxito y ninguno en los rechazos
- [X] T031 [P] [US1] Tests de acceso en `IntTests/Carritos/CarritosAccesoTests.cs`: sobre un carrito existente y sobre un id inexistente, sin token, con token vencido, con otra clave, con otro emisor, con `tipo` incorrecto, sin claim `carrito` y con un token del personal (`TokensDePrueba.Crear`) → `401` en todos los casos; con el token de otro carrito → `404 carrito-no-encontrado` con el mismo body que un id inexistente con su token
- [X] T032 [P] [US1] Tests de modalidades en `IntTests/Carritos/EndpointsPublicosTests.cs` (reescribe el de `Reservas/`): catálogo, horarios y mapa siguen públicos; `POST …/carritos` es público; `GET /api/reservas`, `POST /api/funciones/{id}/reservas` y `POST /api/verificaciones-email` → `404`
- [X] T033 [P] [US1] Tests de mapa en `IntTests/Carritos/MapaConCarritosTests.cs` (reescribe `MapaConReservasTests`): el carrito activo excluye sus asientos; al avanzar `RelojDePrueba` más allá de `venceEn`, vuelven y `GET` muestra `Expirado`
- [X] T034 [US1] Test de concurrencia obligatorio en `IntTests/Carritos/CarritoConcurrenteTests.cs` (reescribe `ReservaConcurrenteTests`): 20 creaciones simultáneas del mismo asiento → exactamente un `201` y 19 `409 asientos-no-disponibles`, con un solo carrito y un solo `CarritoCreado` en la base; 10 creaciones solapadas en la fila E → ningún asiento en dos carritos

### Implementation for User Story 1

- [X] T035 [US1] Implementar `App/Carritos/Commands/CrearCarritoCommand.cs` (`CrearCarritoCommand(FuncionId, Asientos, Desafio, Origen)`, resultado y handler): valida forma, evalúa `IDesafioAntiBots` **antes** de la transacción, toma el bloqueo de la función, lista los activos, invoca `AsignadorDeAsientos.Crear`, agrega el carrito, registra `CarritoCreado` y, después de confirmar, emite el token con `IEmisorDeTokenDeCarrito`
- [X] T036 [US1] Implementar `App/Carritos/Queries/ObtenerCarritoQuery.cs` (`ObtenerCarritoQuery(CarritoId, CarritoDelToken)`): si los ids difieren devuelve `null` sin consultar; si no, `IConsultaCarritos.ObtenerPorId` → `CarritoDto.DesdeLectura(lectura, ahora)`
- [X] T037 [US1] Crear `Api/Modulos/Carritos/Contracts/` (`CrearCarritoRequest(Asientos, Desafio)`, `AsientoRequest`, `CarritoResponse`, `FuncionCarritoResponse`, `CarritoCreadoResponse(Carrito, Token, TokenVenceEn)`) y `Api/Modulos/Carritos/CarritosController.cs` con `POST api/funciones/{funcionId:guid}/carritos` (`[AllowAnonymous]`; pasa `HttpContext.Connection.RemoteIpAddress` como origen) y `GET api/carritos/{carritoId:guid}` (`[Authorize(Policy = Politicas.PortadorDeCarrito)]`), con el mapeo de errores del contrato (`desafio-rechazado` → `403`)
- [X] T038 [US1] Registrar `CrearCarritoCommandHandler` y `ObtenerCarritoQueryHandler` (Scoped) en `Api/Program.cs` y reemplazar los ejemplos de reservas y verificación en `Api/CinemaSystemRandomPlay.Api.http` por crear y consultar un carrito

**Checkpoint**: US1 completa. Se pueden retener asientos de forma anónima y la concurrencia de creación está probada.

---

## Phase 4: User Story 2 — Cambiar mi selección sin perder el lugar en la fila (Priority: P2)

**Goal**: `PUT /api/carritos/{id}/asientos` reemplaza la selección de forma atómica, todo o nada y sin extender el vencimiento.

**Independent Test**: carrito con C5 y C6 → `PUT` con D1 y D2 → `200`; el mapa muestra C5 y C6 libres y D1 y D2 retenidos; `venceEn` igual; un reemplazo con un asiento ajeno da `409` y el carrito no cambia.

### Tests for User Story 2

- [X] T039 [P] [US2] Tests de dominio en `DomainTests/Carritos/CarritoTests.cs`: `ReemplazarAsientos` deja exactamente la nueva selección, no modifica `VenceEn`, conserva las instancias comunes (C6 en {C5,C6} → {C6,C7}) y rechaza fuera de `Activo`, vacío, repetidos o más del máximo
- [X] T040 [P] [US2] Tests de dominio en `DomainTests/Carritos/AsignadorDeAsientosTests.cs`: `Reemplazar` con no activo → `CarritoNoActivo(estado)` antes que cualquier otra validación; los asientos propios cuentan como disponibles; los de otro carrito → `AsientosNoDisponibles` sin modificar el carrito; ok devuelve liberados y retenidos
- [X] T041 [P] [US2] Tests de aplicación en `AppTests/Carritos/ReemplazarSeleccionCommandHandlerTests.cs`: id distinto del claim → no encontrado sin tocar el repositorio; carrito inexistente → no encontrado; pedido inválido; no activo; no disponibles; ok registra `SeleccionReemplazada` con liberados, retenidos y `VenceEn` sin cambios
- [X] T042 [P] [US2] Tests de API en `IntTests/Carritos/ReemplazoDeSeleccionTests.cs`: reemplazo total y parcial reflejado en el mapa; `venceEn` igual después de avanzar el reloj 3 minutos; `409 asientos-no-disponibles` y luego `GET` con la selección anterior intacta; `400` (vacío) y `422`; `409 carrito-no-activo` con `estadoActual` en un carrito expirado
- [X] T043 [P] [US2] Test de repositorio en `IntTests/Carritos/CarritoRepositoryTests.cs`: un reemplazo con asientos comunes se persiste sin errores de seguimiento de EF y, al recargar en otro contexto, deja exactamente la nueva selección
- [X] T044 [US2] Agregar a `IntTests/Carritos/CarritoConcurrenteTests.cs` los casos obligatorios de reemplazo: (a) dos carritos reemplazan en paralelo hacia el mismo asiento libre → uno `200` y el otro `409`, y el perdedor conserva su selección anterior; (b) el carrito A reemplaza {C5,C6} por {D1,D2} mientras otros 10 pedidos crean carritos con C5 → nunca dos carritos activos con el mismo asiento, y A termina con {C5,C6} o {D1,D2} completos; (c) dos reemplazos paralelos del mismo carrito → la selección final es exactamente una de las dos

### Implementation for User Story 2

- [X] T045 [US2] Agregar a `Domain/Carritos/Carrito.cs` el método `internal ReemplazarAsientos(IReadOnlyList<Asiento> nuevos, DateTimeOffset ahora, int maximo)` (diferencia sobre `_asientos`) y a `Domain/Carritos/AsignadorDeAsientos.cs` la operación `Reemplazar(funcion, carrito, carritosActivos, pedidos, ahora, reglas)` con el resultado `CarritoNoActivo`, y crear `Domain/Carritos/Eventos/SeleccionReemplazada.cs`
- [X] T046 [US2] Implementar `App/Carritos/Commands/ReemplazarSeleccionCommand.cs` (`ReemplazarSeleccionCommand(CarritoId, CarritoDelToken, Asientos)`): compara ids, valida forma, lee el `FuncionId` sin seguimiento, toma el bloqueo de la función, recarga el carrito con seguimiento, lista los activos, invoca `Reemplazar`, registra el evento y lee el carrito actualizado después de confirmar. Extraer la validación de forma compartida con `CrearCarrito` a `App/Carritos/ValidacionDeAsientos.cs`
- [X] T047 [US2] Agregar a `Api/Modulos/Carritos/CarritosController.cs` el endpoint `PUT api/carritos/{carritoId:guid}/asientos` (con `ReemplazarAsientosRequest` en `Contracts/`), mapear `carrito-no-activo` con `estadoActual`, registrar el handler en `Api/Program.cs` y agregar el ejemplo a `Api/CinemaSystemRandomPlay.Api.http`

**Checkpoint**: US1 y US2 funcionan; el reemplazo es atómico y está probado con concurrencia real.

---

## Phase 5: User Story 4 — Que nadie acapare la sala (Priority: P2)

**Goal**: tope global de asientos retenidos por función, límite de creación por origen y punto de extensión anti-bots.

**Independent Test**: en la sala Sur 1 (40 asientos, tope 20), retener 20 y pedir 1 más → `409 funcion-sin-cupo`; al abandonar o vencer, se acepta; 11 creaciones desde un origen con límite 10 → el 11.º recibe `429` con `Retry-After`; un desafío que rechaza da `403`.

### Tests for User Story 4

- [X] T048 [P] [US4] Tests de dominio en `DomainTests/Carritos/AsignadorDeAsientosTests.cs`: `Crear` y `Reemplazar` rechazan con `FuncionSinCupo` si `otros + pedidos > tope`; `Reemplazar` excluye al propio carrito (4 en el tope → otros 4 se acepta); el tope se evalúa después de no disponibles; el resultado no lleva cantidades
- [X] T049 [P] [US4] Tests de API en `IntTests/Carritos/TopePorFuncionTests.cs`: sala Sur 1, 20 retenidos con dos carritos → crear 1 más da `409 funcion-sin-cupo` sin extensiones con datos; tras abandonar uno (o avanzar el reloj) se acepta; un reemplazo dentro del tope se acepta
- [X] T050 [P] [US4] Tests de API en `IntTests/Carritos/LimitePorOrigenTests.cs` (con una factory derivada, `CreacionesPorOrigen = 3`): el 4.º `POST …/carritos` → `429 demasiados-pedidos` con `Retry-After`; los intentos rechazados (por ejemplo, `409`) también cuentan; `GET`, `PUT …/asientos` y `POST …/abandono` siguen respondiendo con el origen en el límite
- [X] T051 [P] [US4] Tests de API en `IntTests/Carritos/DesafioAntiBotsTests.cs` (factory derivada que registra un `IDesafioAntiBots` que rechaza): `POST …/carritos` → `403 desafio-rechazado`, sin carrito ni outbox; con la implementación por defecto, `desafio` se ignora
- [X] T052 [US4] Agregar a `IntTests/Carritos/CarritoConcurrenteTests.cs`: 20 creaciones simultáneas de 1 asiento distinto cada una en la sala Sur 1 con 15 ya retenidos (tope 20) → exactamente 5 `201`, y la suma retenida en la base nunca supera 20

### Implementation for User Story 4

- [X] T053 [US4] Agregar la validación del tope global (paso 5 de data-model.md) a `Crear` y `Reemplazar` en `Domain/Carritos/AsignadorDeAsientos.cs`, con `TipoResultadoSeleccion.FuncionSinCupo`, y propagar el estado `FuncionSinCupo` en `CrearCarritoCommand` y `ReemplazarSeleccionCommand`
- [X] T054 [US4] En `Api/Program.cs`: renombrar la política del rate limiter a `CreacionDeCarritos` (constante en `CarritosController`), leer el límite y la ventana de `OpcionesCarritos`, y hacer genérico el mensaje de `OnRejected` (`"Se superó el máximo de pedidos desde este origen. Intente más tarde."`, `codigo` `demasiados-pedidos`, `Retry-After`). Aplicar `[EnableRateLimiting(...)]` solo al `POST` de creación y mapear `funcion-sin-cupo` en el controller

**Checkpoint**: las tres defensas activas y probadas; la concurrencia sobre el tope es exacta.

---

## Phase 6: User Story 3 — Abandonar el carrito o dejar el email de entrega (Priority: P3)

**Goal**: abandonar libera de inmediato; el email de entrega se guarda normalizado sin verificar ni tocar el reloj.

**Independent Test**: abandonar → `200 Abandonado` y asientos libres; repetir → `409 carrito-no-activo`; `PUT …/email-entrega` con `" Ana@Example.com "` → `"ana@example.com"` con `venceEn` igual; con email inválido → `400`; un token ajeno → `404` en ambas.

### Tests for User Story 3

- [X] T055 [P] [US3] Tests de dominio en `DomainTests/Carritos/CarritoTests.cs`: `Abandonar` en activo → `Abandonado` con `AbandonadoEn`; en expirado o abandonado → `NoActivo(estado)` sin cambios; `IndicarEmailEntrega` en activo guarda y permite reemplazar, sin tocar `Estado` ni `VenceEn`; fuera de activo → `NoActivo`
- [X] T056 [P] [US3] Tests de aplicación en `AppTests/Carritos/AbandonarCarritoCommandHandlerTests.cs` y `AppTests/Carritos/IndicarEmailEntregaCommandHandlerTests.cs`: id distinto del claim → no encontrado sin tocar el repositorio; no activo; ok (abandonar registra `CarritoAbandonado` con los asientos liberados; el email no registra eventos); email inválido → pedido inválido
- [X] T057 [P] [US3] Tests de API en `IntTests/Carritos/AbandonoYEmailTests.cs`: abandonar → `200` y el mapa los muestra libres de inmediato; repetir → `409 carrito-no-activo` con `estadoActual = Abandonado`; email normalizado en la respuesta y en `GET`; `400` con email inválido y el anterior intacto; `409` en un carrito expirado; con el token de otro carrito → `404` en ambos endpoints; ningún email enviado (`EnviadorDeEmailEnMemoria` vacío)

### Implementation for User Story 3

- [X] T058 [US3] Agregar a `Domain/Carritos/Carrito.cs` los métodos `Abandonar(ahora)` → `ResultadoAbandono` e `IndicarEmailEntrega(EmailComprador, ahora)` → `ResultadoEmailEntrega` (`Domain/Carritos/ResultadoAbandono.cs`, `Domain/Carritos/ResultadoEmailEntrega.cs`) y crear `Domain/Carritos/Eventos/CarritoAbandonado.cs`
- [X] T059 [US3] Implementar `App/Carritos/Commands/AbandonarCarritoCommand.cs` (bloqueo de la función como en el reemplazo; registra `CarritoAbandonado`) e `App/Carritos/Commands/IndicarEmailEntregaCommand.cs` (valida con `EmailComprador.TryCrear`; mismo bloqueo; sin evento)
- [X] T060 [US3] Agregar a `Api/Modulos/Carritos/CarritosController.cs` los endpoints `POST api/carritos/{carritoId:guid}/abandono` y `PUT api/carritos/{carritoId:guid}/email-entrega` (con `EmailEntregaRequest` en `Contracts/`), registrar los handlers en `Api/Program.cs` y agregar los ejemplos a `Api/CinemaSystemRandomPlay.Api.http`

**Checkpoint**: las cuatro historias funcionan de punta a punta.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T061 [P] Actualizar `README.md`: tabla de features (agregar la 007 y marcar la 006 como reemplazada), flujo de uso (crear carrito → token → reemplazar, email, abandonar), tabla de endpoints y modalidades (portador del carrito), configuración (`Carritos`, `Invitados:ClaveFirma` y `TokenMinutosVigencia`), migración `ReemplazarReservasPorCarritos` con su guarda y "Deuda técnica conocida" (límite por origen por proceso y `ForwardedHeaders`; carritos viejos sin purga; sin recuperación de token; `IEnviadorDeEmail` sin uso hasta la feature de pago). Quitar toda mención a la verificación por email y al tope por email
- [X] T062 [P] Actualizar la nota de cabecera de `specs/004-reserva-asientos-funcion/spec.md`, `plan.md` y `contracts/reservas-api.md`: el mecanismo de identidad y los endpoints fueron reemplazados por el carrito anónimo con token de la [feature 007](../007-carrito-anonimo/spec.md); sin mencionar la verificación por email como vigente
- [X] T063 [P] Agregar a `specs/006-comprador-invitado-email/spec.md`, `plan.md` y `contracts/*.md` una nota de cabecera "Reemplazada por la feature 007 (carrito anónimo); este documento es el registro histórico de lo mergeado"
- [X] T064 Buscar restos con `grep -rniE "verificac|InvitadoVerificado|MaximoAsientosPorComprador|Reserva(s)?Controller|ObtenerComprador" src tests README.md` y eliminar los que no correspondan (las migraciones históricas quedan)
- [X] T065 Ejecutar `dotnet build` (sin advertencias nuevas) y `dotnet test` completos y anotar los conteos por proyecto; recorrer quickstart.md contra la API en `Development` con un Postgres descartable propio

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias.
- **Foundational (Phase 2)**: depende de Setup y bloquea todas las historias. Dentro de la fase: T003–T007 → T008–T013 → T014–T016 → T017–T023 → T024–T027.
- **US1 (Phase 3)**: depende de la Fase 2. Es el MVP.
- **US2 (Phase 4)**: depende de US1 (necesita crear carritos para reemplazarlos).
- **US4 (Phase 5)**: depende de US1 para el tope en la creación y de US2 para el tope en el reemplazo (T048, T053).
- **US3 (Phase 6)**: depende de US1. Es independiente de US2 y US4, aunque comparte archivos con ellas (`Carrito.cs`, `CarritosController.cs`, `Program.cs`), así que conviene hacerla después para no pisarse.
- **Polish (Phase 7)**: depende de todas.

### Within Each User Story

- Los tests se escriben primero y deben fallar.
- Dominio → Application → Api.
- `CarritoConcurrenteTests.cs` se extiende en US1, US2 y US4 de forma secuencial (mismo archivo).

### Parallel Opportunities

- T002 junto a T001.
- En la Fase 2: T007, T011, T013, T015, T019 y T020 en archivos distintos.
- Los tests marcados [P] de cada historia entre sí.
- T061, T062 y T063 entre sí.

---

## Parallel Example: User Story 1

```text
T028 AppTests/Carritos/CrearCarritoCommandHandlerTests.cs
T029 AppTests/Carritos/ObtenerCarritoQueryHandlerTests.cs
T030 IntTests/Carritos/CarritosControllerTests.cs
T031 IntTests/Carritos/CarritosAccesoTests.cs
T032 IntTests/Carritos/EndpointsPublicosTests.cs
T033 IntTests/Carritos/MapaConCarritosTests.cs
```

## Parallel Example: User Story 2

```text
T039 DomainTests/Carritos/CarritoTests.cs (reemplazo)
T041 AppTests/Carritos/ReemplazarSeleccionCommandHandlerTests.cs
T042 IntTests/Carritos/ReemplazoDeSeleccionTests.cs
T043 IntTests/Carritos/CarritoRepositoryTests.cs
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Fase 1 y Fase 2: retiro de la 006, renombre y migración.
2. Fase 3: crear y consultar un carrito, con concurrencia de creación probada.
3. Validar US1 por separado: un comprador anónimo retiene asientos, que vencen solos.

### Incremental Delivery

1. US1 → retención anónima.
2. US2 → reemplazo atómico, el núcleo del modelo de carrito.
3. US4 → defensas contra el acaparamiento.
4. US3 → abandono y email de entrega.
5. Polish → documentación y suite completa.

---

## Notes

- No hacer push ni abrir el PR hasta que el usuario lo pida. Mostrar el resultado de `dotnet build` y `dotnet test` antes.
- No borrar volúmenes de Docker ni datos locales: para el recorrido manual, usar un contenedor descartable propio.
- La migración nunca descarta datos sin `CINEMA_PERMITIR_DESCARTE_RESERVAS=true`.
