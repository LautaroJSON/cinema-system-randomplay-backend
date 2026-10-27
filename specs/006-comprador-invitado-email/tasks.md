---

description: "Task list for Comprador Invitado con Email Verificado"
---

# Tasks: Comprador Invitado con Email Verificado

**Input**: Design documents from `/specs/006-comprador-invitado-email/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/verificacion-email-api.md, contracts/reservas-api.md, quickstart.md

**Tests**: Incluidos según la "Estrategia de tests" de plan.md y el Principio V (por criterio: invariantes, casos de uso que modifican estado, unicidad y concurrencia contra Postgres real, seguridad de acceso). Se escriben antes de la implementación que cubren y deben fallar primero. El test de concurrencia de reservas sigue siendo obligatorio y se adapta. No llevan tests: DTOs, requests/responses, registro en el DI, clases de opciones, los dos enviadores de email, `EmisorDeCredencialJwt` por separado y la documentación.

**Organization**: Tareas agrupadas por historia de usuario. La Fase 2 concentra el cambio de identidad de la reserva (`ClienteId` → `Comprador`), que es atómico: cambiar el tipo rompe la compilación de todo lo que lo usa, así que no se puede repartir entre historias sin dejar la solución sin compilar.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: Historia a la que pertenece (US1..US3)
- Todas las rutas son relativas a la raíz del repositorio

## Path Conventions

Proyectos con nombres de dominio en español, según la constitución. Abreviaturas usadas abajo: `Domain/` = `src/CinemaSystemRandomPlay.Domain/`, `App/` = `src/CinemaSystemRandomPlay.Application/`, `Infra/` = `src/CinemaSystemRandomPlay.Infrastructure/`, `Api/` = `src/CinemaSystemRandomPlay.Api/`, `DomainTests/` = `tests/CinemaSystemRandomPlay.Domain.UnitTests/`, `AppTests/` = `tests/CinemaSystemRandomPlay.Application.UnitTests/`, `IntTests/` = `tests/CinemaSystemRandomPlay.IntegrationTests/`.

---

## Phase 1: Setup

**Purpose**: Dependencias, configuración y línea base

- [X] T001 Agregar `PackageReference` a `Microsoft.IdentityModel.JsonWebTokens` en `Infra/CinemaSystemRandomPlay.Infrastructure.csproj`, con la misma versión que resuelve JwtBearer 10.0.12 (verificar con `dotnet list src/CinemaSystemRandomPlay.Api package --include-transitive`)
- [X] T002 [P] En `Api/appsettings.json`: renombrar `Reservas:MaximoAsientosPorCliente` a `Reservas:MaximoAsientosPorComprador` (valor 10) y agregar la sección `Invitados` con `CodigoMinutosVigencia: 10`, `MaximoIntentosFallidos: 5`, `CredencialMinutosVigencia: 60`, `SolicitudesPorEmail: 5`, `SolicitudesPorOrigen: 20`, `VentanaSolicitudesMinutos: 60`. **No** incluir `ClaveFirma` (va por user-secrets o variable de entorno, ver quickstart.md)
- [X] T003 [P] Ejecutar `dotnet build` y `dotnet test` sobre la solución para confirmar una línea base verde antes de empezar (anotar si los tests de integración no corren por falta de Docker)

---

## Phase 2: Foundational — Identidad de la reserva por email

**Purpose**: Reemplazar `ClienteId` por `Comprador` (email normalizado) en toda la pila, cambiar la autenticación de las reservas al esquema `Invitado` y migrar la base. Al terminar, el comportamiento de la feature 004 se conserva con credenciales de invitado emitidas por los tests (todavía no hay endpoint que las emita: eso es US1).

**⚠️ CRITICAL**: Ninguna historia puede empezar hasta completar esta fase

### Dominio

- [X] T004 [P] Crear los tests de `EmailComprador` en `DomainTests/Compradores/EmailCompradorTests.cs`: recorta espacios y pasa a minúsculas (`" Ana@Example.com "` → `ana@example.com`), igualdad entre variantes de escritura, rechaza vacío/nulo, sin `@`, dos `@`, parte local o dominio vacíos, dominio sin punto o que empieza/termina con punto, espacios internos y más de 254 caracteres, y NO normaliza alias (`ana+1@example.com` ≠ `ana@example.com`). Deben fallar (el tipo no existe)
- [X] T005 Crear el Value Object `EmailComprador` (record inmutable, `Valor`, `TryCrear(string?, out EmailComprador)`, `Crear` que lanza `DomainException`, `ToString()` devuelve el valor normalizado) en `Domain/Compradores/EmailComprador.cs`, según data-model.md
- [X] T006 [P] Renombrar `MaximoAsientosPorCliente` a `MaximoAsientosPorComprador` en el record `ReglasReserva` (misma validación ≤ 0 y mismo valor por defecto) en `Domain/Reservas/ReglasReserva.cs`
- [X] T007 Cambiar `Reserva.ClienteId` (string) por `Reserva.Comprador` (`EmailComprador`) en `Domain/Reservas/Reserva.cs`: el factory recibe `EmailComprador` (rechaza `null` con `DomainException`) y se elimina la validación de largo de texto, que ahora garantiza el Value Object
- [X] T008 Cambiar `ReservadorDeAsientos.Reservar` para recibir `EmailComprador comprador` y contar el tope con `r.Comprador == comprador`; renombrar la referencia a `reglas.MaximoAsientosPorComprador` en `Domain/Reservas/ReservadorDeAsientos.cs`
- [X] T009 [P] Reemplazar el campo del cliente de `ReservaCreada` por `Comprador` (string con el email normalizado, para que el payload del outbox sea estable) en `Domain/Reservas/Eventos/ReservaCreada.cs`
- [X] T010 Adaptar los tests de dominio de reservas a `EmailComprador` en `DomainTests/Reservas/ReservaFactory.cs`, `DomainTests/Reservas/ReservaTests.cs` y `DomainTests/Reservas/ReservadorDeAsientosTests.cs`; agregar a este último el caso del tope por email normalizado (7 retenidos por `ana@example.com` + pedido de 4 con `ANA@example.com ` → `LimiteSuperado` con 3 restantes) y el de otro email que no comparte cupo (US2, escenarios 2 y 3)

### Application

- [X] T011 Cambiar `string ClienteId` por `EmailComprador Comprador` en `ReservarAsientosCommand` (`App/Reservas/Commands/ReservarAsientosCommand.cs`), `CancelarReservaCommand` (`App/Reservas/Commands/CancelarReservaCommand.cs`), `ListarMisReservasQuery` (`App/Reservas/Queries/ListarMisReservasQuery.cs`), `ObtenerMiReservaQuery` (`App/Reservas/Queries/ObtenerMiReservaQuery.cs`) y en `IConsultaReservas` (`App/Reservas/Ports/IConsultaReservas.cs`, métodos `ListarDeComprador` y `ObtenerDeComprador`). El `ReservaCreada` del handler de reservar pasa el email normalizado. Cancelar compara `reserva.Comprador == command.Comprador`; una reserva ajena sigue dando `NoEncontrada`
- [X] T012 Adaptar los tests de Application a `EmailComprador` en `AppTests/Reservas/ReservasTestSupport.cs`, `AppTests/Reservas/ReservarAsientosCommandHandlerTests.cs`, `AppTests/Reservas/CancelarReservaCommandHandlerTests.cs` y `AppTests/Reservas/ListarMisReservasQueryHandlerTests.cs`

### Infrastructure

- [X] T013 Mapear `Reserva.Comprador` a la columna `CompradorEmail` (`varchar(254)`, requerida) con un `HasConversion` de `EmailComprador` ↔ string y renombrar el índice a `(CompradorEmail, CreadaEn)` en `Infra/Persistence/Reservas/ReservaEntityConfiguration.cs`; actualizar `ListarDeComprador` y `ObtenerDeComprador` para filtrar por email en `Infra/Persistence/Reservas/ConsultaReservas.cs`
- [X] T014 [P] Crear la clase de opciones `OpcionesInvitados` (`ClaveFirma`, `CodigoMinutosVigencia`, `MaximoIntentosFallidos`, `CredencialMinutosVigencia`, `SolicitudesPorEmail`, `SolicitudesPorOrigen`, `VentanaSolicitudesMinutos`, `Emisor`/`Audiencia` constantes `cinema-api-invitados`) en `Infra/Invitados/OpcionesInvitados.cs`, con los valores por defecto de data-model.md
- [X] T015 Crear la migración EF `AddCompradorEmailEnReservas` (`dotnet ef migrations add AddCompradorEmailEnReservas --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api`) y **editar su `Up`** según research.md, Decisión 10: (1) guarda SQL que lanza una excepción con un mensaje claro si `Reservas` tiene filas y la variable de entorno `CINEMA_PERMITIR_DESCARTE_RESERVAS` no vale `true` al aplicar la migración; (2) `DELETE` de `ReservaAsientos`, `Reservas` y `OutboxMensajes` de tipo `ReservaCreada` y `ReservaCancelada`; (3) `RenameColumn` `ClienteId` → `CompradorEmail`, ajuste a `varchar(254)` y renombre del índice. Escribir un `Down` coherente. Revisar que el snapshot quedó actualizado en `Infra/Migrations/`
- [X] T016 Crear el test de la migración en `IntTests/Reservas/MigracionCompradorEmailTests.cs` con Testcontainers: migrar hasta `AddPrecios`, insertar con SQL una función, una reserva con `ClienteId` y su asiento y un mensaje de outbox `ReservaCreada`; (a) sin la variable, aplicar la migración siguiente falla y no borra nada; (b) con `CINEMA_PERMITIR_DESCARTE_RESERVAS=true` pasa y las tres tablas quedan vacías de datos de reservas, con la columna renombrada; (c) sobre una base sin reservas pasa sin la variable. Restaurar la variable de entorno al terminar (FR-021)
- [X] T017 En `Api/Program.cs`, dentro del bloque `IsDevelopment()`, fijar `Environment.SetEnvironmentVariable("CINEMA_PERMITIR_DESCARTE_RESERVAS", "true")` antes de `MigrateAsync()`, con un comentario que explique que es la decisión explícita de descartar reservas de bases de desarrollo (FR-021)

### Api (autenticación y controller)

- [X] T018 [P] Reemplazar `Customer` en `Api/Autenticacion/Politicas.cs` por las constantes `InvitadoVerificado` y `Personal`; crear `Api/Autenticacion/EsquemasDeAutenticacion.cs` con la constante `Invitado` (nombre del esquema) y `TipoCredencial = "invitado-verificado"`
- [X] T019 [P] Reemplazar `ObtenerClienteId` por `ObtenerComprador` en `Api/Autenticacion/ClaimsPrincipalExtensions.cs`: lee el claim `email` y devuelve un `EmailComprador?` con `TryCrear` (normaliza), o `null` si falta o es inválido
- [X] T020 En `Api/Program.cs`: (1) leer `Invitados` en `OpcionesInvitados`, fallar el arranque con un mensaje claro si `ClaveFirma` falta o mide menos de 32 bytes; (2) registrar el esquema JwtBearer `Invitado` (`MapInboundClaims = false`, emisor y audiencia `cinema-api-invitados`, `IssuerSigningKey` simétrica, `ValidAlgorithms = [HS256]`, `ValidateLifetime`, `ClockSkew` de 30 s, sin `Authority`) sin tocar el esquema `Bearer` del personal ni el esquema por defecto; (3) registrar las políticas `InvitadoVerificado` (esquema `Invitado`, usuario autenticado, claim `tipo` = `invitado-verificado`) y `Personal` (rol `CinemaManager` o `Admin`); eliminar la política `Customer`; mantener el `FallbackPolicy` cerrado; (4) leer `Reservas:MaximoAsientosPorComprador`
- [X] T021 Actualizar `Api/Modulos/Reservas/ReservasController.cs`: `[Authorize(Policy = Politicas.InvitadoVerificado)]`, obtener el comprador con `User.ObtenerComprador()` (devolver `Unauthorized()` si es `null`), pasar `EmailComprador` a los comandos y consultas, y cambiar el `title` de `limite-asientos-superado` a "por comprador"
- [X] T022 Adaptar `IntTests/Compartido/TokensDePrueba.cs`: eliminar `Customer(...)`, agregar `Invitado(email, vencido = false, clave = null, emisor = EmisorInvitados)` que firma un JWT HS256 con la `ClaveFirmaDePrueba` (constante de ≥ 32 bytes), audiencia/emisor `cinema-api-invitados` y claims `sub`, `email` y `tipo`; mantener `Crear(...)` para tokens del personal
- [X] T023 Configurar `Invitados:ClaveFirma` con `TokensDePrueba.ClaveFirmaDePrueba` en las factories `IntTests/Reservas/ReservasApiFactory.cs`, `IntTests/Funciones/FuncionesApiFactory.cs` y `IntTests/Catalogo/CatalogoApiFactory.cs` (vía `AddInMemoryCollection`)
- [X] T024 Adaptar los tests de integración existentes de reservas a credenciales de invitado en `IntTests/Reservas/ReservasControllerTests.cs`, `IntTests/Reservas/ReservaConcurrenteTests.cs` (cada pedido concurrente con un email distinto, sigue exigiendo 1 éxito y el resto `409`), `IntTests/Reservas/MapaConReservasTests.cs` y `IntTests/Reservas/EndpointsPublicosTests.cs`; reemplazar los casos de `403` por rol `Customer` por casos de `401`. Todos deben pasar: demuestran que el comportamiento de la 004 se conserva (SC-007)

**Checkpoint**: la solución compila sin advertencias nuevas; los tests de dominio, Application e integración pasan; las reservas funcionan con una credencial de invitado emitida por el test; el rol `Customer` ya no existe en el código.

---

## Phase 3: User Story 1 - Verificar mi email y obtener acceso de invitado (Priority: P1) 🎯 MVP

**Goal**: Cualquier persona pide un código para su email, lo ingresa y recibe una credencial con vencimiento que sirve para reservar.

**Independent Test**: Pedir el código de un email, tomarlo del doble de envío de email en memoria, confirmarlo y obtener una credencial válida (que reserva con éxito); un código incorrecto, vencido, usado o invalidado no entrega nada.

### Tests for User Story 1

> Escribir primero; deben fallar antes de implementar.

- [X] T025 [P] [US1] Crear `DomainTests/Verificaciones/VerificacionEmailTests.cs`: `Crear` deja la verificación `Vigente` y valida argumentos; `Verificar` con el hash correcto → `Correcto` y pasa a `Usada`; segundo uso → `YaUsado`; hash incorrecto → `Incorrecto` y suma un intento; al superar el máximo (5) → `Invalidado` y ya no acepta ni el código correcto; borde exacto de vencimiento (`VenceEn <= ahora` ⇒ `Vencido`); `Invalidar` pasa `Vigente` a `Invalidada` y no afecta otros estados
- [X] T026 [P] [US1] Crear `AppTests/Verificaciones/SolicitarVerificacionEmailCommandHandlerTests.cs` con dobles de los puertos: email inválido → `EmailInvalido` sin crear ni enviar; pedido válido → crea la verificación con el hash del código, invalida la vigente anterior, confirma la transacción y **después** envía el mensaje al email normalizado; el resultado es `Aceptada` tanto si el email tiene reservas como si no (FR-003); con 5 solicitudes en la ventana → `LimiteExcedido` sin crear ni enviar; si el envío lanza una excepción, la verificación ya quedó persistida
- [X] T027 [P] [US1] Crear `AppTests/Verificaciones/ConfirmarVerificacionEmailCommandHandlerTests.cs`: código correcto → `Ok` con la credencial y su vencimiento de `IEmisorDeCredencial`; incorrecto → `CodigoInvalido` y el intento se persiste; sin verificación vigente o ya usado → `CodigoInvalido` (indistinguible); vencido → `CodigoVencido`; intentos agotados → `CodigoInvalidado`; email inválido → `EmailInvalido`; solo el caso correcto emite credencial

### Implementation for User Story 1

- [X] T028 [P] [US1] Crear `EstadoVerificacion` (`Vigente`, `Usada`, `Invalidada`, `Vencida`), `ResultadoVerificacion` (`Correcto`, `Incorrecto`, `Vencido`, `YaUsado`, `Invalidado`) y el record `ReglasVerificacion(CodigoMinutosVigencia, MaximoIntentosFallidos, SolicitudesPorEmail, VentanaSolicitudesMinutos)` con validación > 0 en `Domain/Verificaciones/EstadoVerificacion.cs`, `Domain/Verificaciones/ResultadoVerificacion.cs` y `Domain/Verificaciones/ReglasVerificacion.cs`
- [X] T029 [US1] Crear el agregado `VerificacionEmail` (campos, estados y operaciones `Crear`, `Invalidar`, `EstadoEn`, `Verificar` de data-model.md) en `Domain/Verificaciones/VerificacionEmail.cs`
- [X] T030 [P] [US1] Crear `IVerificacionEmailRepository` (`BloquearPorEmail(EmailComprador)`, `ObtenerVigente(EmailComprador)`, `ContarDesde(EmailComprador, DateTimeOffset desde)`, `Agregar(VerificacionEmail)`) en `Domain/Verificaciones/IVerificacionEmailRepository.cs`
- [X] T031 [P] [US1] Crear los puertos `IEmisorDeCredencial` (`CredencialEmitida Emitir(EmailComprador, DateTimeOffset ahora)` con `Credencial` y `VenceEn`), `IEnviadorDeEmail` (`Task Enviar(MensajeDeVerificacion, CancellationToken)`, con `MensajeDeVerificacion(EmailComprador Destinatario, string Codigo)`) e `ICodigosDeVerificacion` (`string Generar()`, `string Hashear(EmailComprador, string codigo)`) en `App/Verificaciones/Ports/`
- [X] T032 [US1] Crear `SolicitarVerificacionEmailCommandHandler` en `App/Verificaciones/Commands/SolicitarVerificacionEmailCommand.cs` (resultado `Aceptada`/`EmailInvalido`/`LimiteExcedido`): normaliza con `EmailComprador.TryCrear`; en `IUnidadDeTrabajo.EjecutarEnTransaccion` toma `BloquearPorEmail`, cuenta con `ContarDesde(ahora − ventana)`, aplica el límite, invalida la vigente, genera el código, hashea y agrega la nueva; **fuera** de la transacción, envía el mensaje (research.md, Decisiones 5 a 7)
- [X] T033 [US1] Crear `ConfirmarVerificacionEmailCommandHandler` en `App/Verificaciones/Commands/ConfirmarVerificacionEmailCommand.cs` (resultado `Ok`/`EmailInvalido`/`CodigoInvalido`/`CodigoVencido`/`CodigoInvalidado`): en la transacción toma `BloquearPorEmail`, lee la vigente, aplica `Verificar` con el hash del código ingresado y confirma aunque el resultado sea un rechazo, para persistir el intento; emite la credencial solo con `Correcto`
- [X] T034 [US1] Crear `Infra/Persistence/Verificaciones/VerificacionEmailEntityConfiguration.cs` (tabla `VerificacionesEmail`, conversor de `EmailComprador`, `Estado` como string, índice único parcial `UX_VerificacionesEmail_Email_Vigente` con filtro `"Estado" = 'Vigente'` e índice `(Email, CreadaEn)`), agregar `DbSet<VerificacionEmail>` a `Infra/Persistence/CinemaDbContext.cs` e implementar `Infra/Persistence/Verificaciones/VerificacionEmailRepository.cs` (`BloquearPorEmail` con `pg_advisory_xact_lock(hashtextextended(email, 0))` vía `ExecuteSqlInterpolatedAsync`)
- [X] T035 [US1] Crear la migración EF `AddVerificacionesEmail` (`dotnet ef migrations add AddVerificacionesEmail ...`) y verificar que crea la tabla, ambos índices (el único con su filtro) y que el snapshot quedó actualizado en `Infra/Migrations/`
- [X] T036 [P] [US1] Implementar `ICodigosDeVerificacion` en `Infra/Invitados/CodigosDeVerificacionHmac.cs`: `Generar` con `RandomNumberGenerator.GetInt32(0, 1_000_000)` con relleno a 6 dígitos; `Hashear` con HMAC-SHA256 sobre `email:codigo` con una clave derivada de `ClaveFirma` (con otro contexto que la credencial), en Base64. Las comparaciones se hacen sobre el hash con `CryptographicOperations.FixedTimeEquals` (en el agregado, sobre los bytes Base64)
- [X] T037 [P] [US1] Implementar `IEmisorDeCredencial` en `Infra/Invitados/EmisorDeCredencialJwt.cs` con `JsonWebTokenHandler`: HS256, emisor y audiencia `cinema-api-invitados`, claims `sub` = email, `email`, `tipo` = `invitado-verificado`, `NotBefore` = `ahora`, `Expires` = `ahora` + `CredencialMinutosVigencia`
- [X] T038 [P] [US1] Crear `Infra/Invitados/EnviadorDeEmailRegistrador.cs` (escribe destinatario y código con `ILogger` en lugar de enviar, FR-007) y `Infra/Invitados/EnviadorDeEmailNoConfigurado.cs` (lanza `InvalidOperationException` con un mensaje que explica que el envío real está fuera de alcance de esta feature)
- [X] T039 [P] [US1] Crear `SolicitarVerificacionRequest(string? Email)`, `ConfirmarVerificacionRequest(string? Email, string? Codigo)` y `CredencialResponse(string Credencial, string Tipo, DateTimeOffset VenceEn)` con `DesdeResultado` manual en `Api/Modulos/Verificaciones/Contracts/`
- [X] T040 [US1] Crear `Api/Modulos/Verificaciones/VerificacionesEmailController.cs` con `[AllowAnonymous]` en la clase: `POST api/verificaciones-email` (`202`, `400 pedido-invalido`, `429 demasiados-pedidos` con `Retry-After`) con `[EnableRateLimiting("solicitud-codigo")]`, y `POST api/verificaciones-email/confirmacion` (`200`, `400 pedido-invalido`/`codigo-invalido`/`codigo-invalidado`, `410 codigo-vencido`), según contracts/verificacion-email-api.md. Un `codigo` que no son seis dígitos es `pedido-invalido`. Sin lógica de negocio
- [X] T041 [US1] En `Api/Program.cs`: registrar `ReglasVerificacion`, `ICodigosDeVerificacion` (Singleton), `IEmisorDeCredencial`, `IVerificacionEmailRepository` y los dos handlers (Scoped); registrar `IEnviadorDeEmail` como `EnviadorDeEmailRegistrador` solo en `Development` y `Testing`, y como `EnviadorDeEmailNoConfigurado` en el resto; configurar `AddRateLimiter` con la política `solicitud-codigo` (ventana fija por `RemoteIpAddress`, límite y ventana de `Invitados`, rechazo `429` con `Retry-After` y `application/problem+json` con `codigo` `demasiados-pedidos`) y agregar `app.UseRateLimiter()` antes de `MapControllers`
- [X] T042 [US1] Crear `IntTests/Invitados/EnviadorDeEmailEnMemoria.cs` (implementa `IEnviadorDeEmail`, guarda los mensajes y expone `UltimoCodigoDe(email)`) y reemplazar `IEnviadorDeEmail` por este doble en `IntTests/Reservas/ReservasApiFactory.cs`, que también deja los límites de origen altos por defecto para no interferir entre tests (se baja solo en el test de T045)
- [X] T043 [P] [US1] Crear `IntTests/Invitados/VerificacionEmailRepositoryTests.cs` con Testcontainers: el índice único parcial impide dos verificaciones `Vigente` del mismo email pero permite una `Vigente` junto a otras `Usada`/`Invalidada`; `ContarDesde` respeta la ventana; dos transacciones concurrentes de pedir código para el mismo email se serializan con `BloquearPorEmail` y no superan el límite
- [X] T044 [US1] Crear `IntTests/Invitados/VerificacionEmailApiTests.cs` con los escenarios de la User Story 1: flujo completo (pedir, leer del doble, confirmar, **reservar con la credencial obtenida**); email con mayúsculas y espacios se trata como el mismo; pedido con email inválido → `400` sin código; respuesta de pedir `202` idéntica para un email con reservas y otro sin ellas (comparar status y body, SC-008); un segundo pedido invalida el código anterior; código usado, incorrecto, vencido (avanzando `RelojDePrueba`) e invalidado tras 5 intentos fallidos con sus `codigo`; el código correcto de otro email no sirve; el pedido 6 del mismo email en la ventana → `429 demasiados-pedidos` sin mensaje nuevo
- [X] T045 [P] [US1] Crear `IntTests/Invitados/LimitePorOrigenTests.cs` con una factory propia que configura `Invitados:SolicitudesPorOrigen = 2`: el tercer pedido de código desde el mismo origen (para emails distintos) recibe `429` con `Retry-After` y no se envía ningún mensaje más; la confirmación de código no se ve afectada por este límite

**Checkpoint**: User Story 1 completa y probada: cualquier persona obtiene una credencial con dos llamadas y reserva con ella.

---

## Phase 4: User Story 2 - Reservar asientos como invitado verificado (Priority: P2)

**Goal**: Reservar exige una credencial vigente; el dueño es el email de la credencial, y el tope de 10 se cuenta por email normalizado. Las reglas y los errores son los de la feature 004.

**Independent Test**: Con una credencial para un email, reservar asientos disponibles queda a nombre de ese email; sin credencial, vencida o alterada, se rechaza.

### Tests for User Story 2

- [X] T046 [P] [US2] Crear `IntTests/Reservas/ReservasInvitadoTests.cs`: pedido sin credencial, con credencial vencida, con la firma alterada, firmada con otra clave y con un token del personal (esquema `Bearer`, aun con rol `Admin`) → `401` y ninguna reserva creada (FR-010); un `email` distinto en el body se ignora y el dueño es el de la credencial (FR-009); el tope por email normalizado de punta a punta (7 retenidos, pedido de 4 con credencial de `ANA@example.com ` → `409 limite-asientos-superado` con `asientosRestantes = 3`); otro email reserva en la misma función con el cupo lleno (FR-014); un caso de cada rechazo de la 004 (`400`, `404`, `410`, `422`, `409`) conserva status y `codigo` (FR-012, SC-007)

### Implementation for User Story 2

- [X] T047 [US2] Verificar con los tests de T046 que `ObtenerComprador` (T019) normaliza el email de la credencial y que un claim `email` inválido o ausente responde `401`; corregir `Api/Autenticacion/ClaimsPrincipalExtensions.cs` o `Api/Modulos/Reservas/ReservasController.cs` si algún caso falla _(Nota: verificado también arrancando la API: una credencial válida pasa; otra clave, vencida, de otro emisor o con `tipo` distinto dan 401.)_
- [X] T048 [P] [US2] Adaptar `IntTests/Reservas/EndpointsPublicosTests.cs` (FR-017/FR-018): los endpoints públicos existentes y los dos de verificación responden sin credencial; un endpoint sin política declarada responde `401` (fallback cerrado); `GET /api/reservas` con un token del personal responde `401`

**Checkpoint**: User Stories 1 y 2 funcionan juntas; el tope se aplica por email normalizado.

---

## Phase 5: User Story 3 - Ver y cancelar solo mis reservas (Priority: P3)

**Goal**: El invitado lista, consulta y cancela únicamente las reservas de su email; conocer un id o un email ajeno no da acceso.

**Independent Test**: Con reservas de dos emails, cada uno ve y cancela solo las propias; la credencial de uno nunca da acceso a las del otro aunque conozca el id.

### Tests for User Story 3

- [X] T049 [P] [US3] Crear `IntTests/Reservas/ReservasAislamientoTests.cs`: Ana lista sus reservas (pendiente, cancelada, vencida) con el mismo contenido y orden que la 004 y no ve las de Beto (US3, escenarios 1 y 2); consultar o cancelar con la credencial de Ana el id exacto de una reserva de Beto → `404 reserva-no-encontrada` con el mismo cuerpo que para un id inexistente (comparar status y body, SC-003); Ana cancela una propia pendiente y sus asientos quedan libres de inmediato en el mapa público; con la credencial vencida, consultar o cancelar da `401`, pero las reservas siguen existiendo y vencen por sus reglas (FR-016); una credencial nueva para el mismo email (obtenida por el flujo real, desde "otro dispositivo") ve las mismas reservas, y dos credenciales vigentes a la vez dan acceso a las mismas reservas (FR-011)

### Implementation for User Story 3

- [X] T050 [US3] Verificar con los tests de T049 que `ConsultaReservas` (`Infra/Persistence/Reservas/ConsultaReservas.cs`) y `CancelarReservaCommandHandler` (`App/Reservas/Commands/CancelarReservaCommand.cs`) solo operan sobre el email de la credencial y que no hay ninguna rama que revele la existencia de una reserva ajena; corregir lo que falle

**Checkpoint**: las tres historias funcionan de forma independiente y en conjunto.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T051 [P] Actualizar `README.md`: autenticación (invitado con email verificado, flujo de dos pasos, credencial con vencimiento, OIDC solo para personal, rol `Customer` eliminado), endpoints nuevos y modalidades de acceso por endpoint, configuración `Invitados` (incluida `ClaveFirma` con user-secrets), la limitación del límite por origen (por proceso y `ForwardedHeaders` detrás de un proxy) y la migración que descarta reservas de desarrollo; quitar el ítem de deuda técnica sobre la identidad
- [X] T052 [P] Actualizar `Api/CinemaSystemRandomPlay.Api.http` con el flujo de verificación y reemplazar los tokens `Customer` por la credencial de invitado
- [X] T053 [P] Agregar una nota al inicio de `specs/004-reserva-asientos-funcion/spec.md`, `plan.md` y `contracts/reservas-api.md` indicando que el mecanismo de identidad (rol `Customer`, `sub` del token) fue reemplazado por la feature 006, con el enlace a [contracts/reservas-api.md](./contracts/reservas-api.md); no reescribir el resto
- [X] T054 Recorrer [quickstart.md](./quickstart.md) de punta a punta contra Postgres local y confirmar que los tres escenarios dan lo esperado _(Nota: recorrido contra un Postgres descartable: pedir y confirmar código, un segundo uso del código da 400, reservar sin credencial da 401 y con credencial 201, Beto no ve ni cancela la reserva de Ana (404) y Ana sí la cancela.)_
- [X] T055 Ejecutar `dotnet build` (sin advertencias nuevas, nullable habilitado) y `dotnet test` sobre la solución completa, incluido `ReservaConcurrenteTests`; revisar que no queden referencias a `Customer`, `ClienteId` ni `MaximoAsientosPorCliente` con `grep -rn` sobre `src/`, `tests/` y `README.md` _(Nota: `dotnet build` sin advertencias, 184 tests unitarios y 144 de integración en verde (dos corridas seguidas), incluido `ReservaConcurrenteTests`.)_

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias.
- **Foundational (Fase 2)**: depende de Setup y **bloquea** todas las historias.
- **US1 (Fase 3)**: depende de Fase 2. Es el MVP: sin ella no hay forma de obtener una credencial real.
- **US2 (Fase 4)**: depende de Fase 2 (el comportamiento ya existe) y, para el flujo de punta a punta, de US1.
- **US3 (Fase 5)**: depende de Fase 2; el caso de "otro dispositivo" usa el flujo real de US1.
- **Polish (Fase 6)**: depende de las historias que se entreguen.

### Dentro de la Fase 2

- T004 → T005; T005 → T007, T008, T009, T011; T006 → T008.
- T007 + T008 → T010; T011 → T012; T007 → T013 → T015 → T016.
- T018, T019 y T014 son independientes entre sí; T020 depende de T014 y T018; T021 depende de T011, T019 y T020.
- T022 → T023 → T024; T024 depende además de T021.

### Dentro de US1

- Tests T025–T027 antes de T028–T033.
- T028 → T029; T029 + T030 + T031 → T032, T033.
- T034 depende de T029 y T030; T035 depende de T034.
- T040 depende de T032, T033 y T039; T041 depende de T036–T038 y T040.
- T042 depende de T041; T043 depende de T035; T044 y T045 dependen de T041 y T042.

### Oportunidades de paralelismo

- Fase 1: T002 y T003.
- Fase 2: T004 con T006, T009 y T014; T018 con T019; T012 con T013.
- US1: T025, T026 y T027 (tests); T028 con T030 y T031; T036, T037 y T038; T039; T043 y T045.
- US2 y US3 pueden avanzar en paralelo entre sí una vez completa la Fase 2 (archivos distintos).
- Polish: T051, T052 y T053.

### Ejemplo de ejecución paralela (US1, tests)

```text
T025 DomainTests/Verificaciones/VerificacionEmailTests.cs
T026 AppTests/Verificaciones/SolicitarVerificacionEmailCommandHandlerTests.cs
T027 AppTests/Verificaciones/ConfirmarVerificacionEmailCommandHandlerTests.cs
```

---

## Implementation Strategy

### MVP primero (US1)

1. Completar Fase 1 y Fase 2: la identidad por email queda integrada y la 004 sigue verde.
2. Completar Fase 3 (US1): ya se puede obtener una credencial y reservar de verdad.
3. **Detenerse y validar** con el Escenario 1 y 2 de [quickstart.md](./quickstart.md).

### Entrega incremental

1. Fases 1 y 2: base verde con la nueva identidad.
2. US1: verificación y credencial (MVP).
3. US2: endurecer y probar la reserva con credencial (tope por email, rechazos).
4. US3: probar el aislamiento entre emails.
5. Polish: README, `.http`, notas en la 004 y validación final.

### Notas

- Las tareas de test marcadas como "adaptar" no agregan comportamiento: mantienen el de la feature 004 con la nueva identidad.
- Confirmar que cada grupo de tareas deja la solución compilando antes de pasar al siguiente.
