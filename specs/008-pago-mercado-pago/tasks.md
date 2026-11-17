---

description: "Task list for Pago del Carrito con Mercado Pago (credenciales de prueba)"
---

# Tasks: Pago del Carrito con Mercado Pago (credenciales de prueba)

**Input**: Design documents from `/specs/008-pago-mercado-pago/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/pagos-api.md, quickstart.md

**Tests**: incluidos según la "Estrategia de tests" de plan.md y el Principio V: invariantes y transiciones del carrito, la regla de dinero (`ConfirmadorDePagos`), casos de uso que modifican estado, la firma del aviso, el mapeo a Mercado Pago, el Outbox, persistencia y concurrencia contra Postgres real. Se escriben antes de la implementación que cubren y deben fallar primero. El test de concurrencia es obligatorio porque la feature cambia la ocupación de asientos. No llevan tests: DTOs, requests/responses, registro en el DI, clases de opciones, `EnviadorDeEmailSmtp` (se prueba con Mailpit en el quickstart), el endpoint de simulación por separado y la integración real con Mercado Pago en CI.

**Organization**: tareas agrupadas por historia de usuario, en orden de prioridad: US1 (P1), US5 (P1), US2 (P1), US3 (P2) y US4 (P2). US5 y US2 van después de US1 porque necesitan carritos en `Pagando` y el caso de uso `ProcesarPago`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece (US1..US5)
- Todas las rutas son relativas a la raíz del repositorio

## Path Conventions

`Domain/` = `src/CinemaSystemRandomPlay.Domain/`, `App/` = `src/CinemaSystemRandomPlay.Application/`, `Infra/` = `src/CinemaSystemRandomPlay.Infrastructure/`, `Api/` = `src/CinemaSystemRandomPlay.Api/`, `DomainTests/` = `tests/CinemaSystemRandomPlay.Domain.UnitTests/`, `AppTests/` = `tests/CinemaSystemRandomPlay.Application.UnitTests/`, `IntTests/` = `tests/CinemaSystemRandomPlay.IntegrationTests/`.

---

## Phase 1: Setup

**Purpose**: rama, línea base, constitución, dependencias y configuración

- [X] T001 Crear la rama `008-pago-mercado-pago` desde `master` (`git checkout -b 008-pago-mercado-pago`), ejecutar `dotnet build` y `dotnet test` sobre la solución y anotar en este archivo los conteos de tests por proyecto como línea base. **Línea base (2026-11-17)**: 126 Domain y 57 Application en verde; los de integración no corrieron porque Docker no está disponible en esta distro de WSL
- [X] T002 Enmendar `.specify/memory/constitution.md` a v4.1.0 (MINOR) con `/speckit-constitution`: Mercado Pago reemplaza a Stripe como pasarela de pago en el Principio I (lista de frameworks que `Domain` no referencia y adaptadores de `Infrastructure`), en el Principio VI (ejemplo "petición a Stripe") y en "Restricciones de Plataforma y Tecnología" (integraciones externas: Mercado Pago, servicio de email por SMTP y OIDC); actualizar el Sync Impact Report y `Last Amended`
- [X] T003 [P] Agregar a `src/CinemaSystemRandomPlay.Infrastructure/CinemaSystemRandomPlay.Infrastructure.csproj` los paquetes `mercadopago-sdk` versión `3.7.1` y `MailKit` (última 4.x estable, versión exacta fijada) y ejecutar `dotnet restore`
- [X] T004 [P] En `Api/appsettings.json` agregar las secciones `MercadoPago` (`Modo: "Simulado"`, `UrlRetorno: "http://localhost:5173/carritos/{carritoId}/pago"`, `SegundosTimeoutConsulta: 3`), `Outbox` (`Habilitado: true`, `IntervaloSegundos: 2`, `MaximoIntentos: 8`) y `Email` (`Proveedor: "Registrador"`, `Remitente: "entradas@cinema.local"`); en `Api/appsettings.Development.json` agregar `MercadoPago:ClaveSecretaAvisos` con un valor de desarrollo explícito (`"clave-de-avisos-de-desarrollo-no-usar-en-produccion"`). Ningún `AccessToken` en archivos versionados
- [X] T005 [P] En `docker-compose.yml` agregar el servicio `mailpit` (imagen `axllent/mailpit` con tag de versión fijo, puertos `1025:1025` y `8025:8025`) y en el servicio `api`: `depends_on` de `mailpit` y las variables `Email__Proveedor: Smtp`, `Email__Smtp__Host: mailpit`, `Email__Smtp__Puerto: "1025"`, `Email__Smtp__UsarTls: "false"` y `MercadoPago__Modo: Simulado`

---

## Phase 2: Foundational — estados nuevos, persistencia, puertos y Outbox

**Purpose**: estados `Pagando` y `Pagado` en la ocupación, entidades y tablas nuevas con su migración, puertos de pagos, procesador simulado y procesador del Outbox. Al terminar, la solución compila, los tests existentes pasan y todavía no hay endpoints nuevos.

**⚠️ CRITICAL**: ninguna historia puede empezar antes de terminar esta fase.

### Dominio

- [X] T006 [P] Agregar a `DomainTests/Carritos/CarritoTests.cs` tests de estado efectivo con los estados nuevos según data-model.md: `Pagado` guardado se devuelve `Pagado` aunque `VenceEn` haya pasado; `Pagando` vigente → `Pagando`; `Pagando` vencido → `Expirado`; `RetieneAsientosEn` es verdadero para `Activo`/`Pagando` vigentes y para `Pagado` siempre, y falso para `Pagando` vencido y `Abandonado`. Usar un helper de `DomainTests/Carritos/CarritoFactory.cs` que deje el carrito en un estado guardado dado (por reflexión o con los métodos que lleguen en US1; si hace falta, marcar los casos de `Pagando`/`Pagado` como pendientes hasta T036)
- [X] T007 Agregar `Pagando` y `Pagado` a `Domain/Carritos/EstadoCarrito.cs` y reescribir `Carrito.EstadoEn` (estática y de instancia) y `RetieneAsientosEn` en `Domain/Carritos/Carrito.cs` según "Estado efectivo" de data-model.md. `ReemplazarAsientos`, `Abandonar` e `IndicarEmailEntrega` siguen exigiendo `Activo` (el rechazo `EnPago` llega en US1)
- [X] T008 Crear `Domain/Carritos/Entrada.cs` (entidad con `Id`, `Codigo`, `FuncionId`, `Asiento`, `PeliculaTitulo`, `SucursalNombre`, `SalaNombre`, `HoraInicio`, `PrecioUnitario` (`Dinero`), `EmitidaEn`; constructor `internal` que valida no vacíos) y `Domain/Pagos/IGeneradorDeCodigos.cs` (`string Generar()`). Agregar a `Domain/Carritos/Carrito.cs` las propiedades de solo lectura `PreferenciaPagoId`, `UrlPago`, `PagoIniciadoEn`, `PagoId`, `PagoAprobadoEn`, `PagadoEn`, `EntregaEnviadaEn` y la colección `Entradas` (campo `_entradas`), sin métodos nuevos todavía
- [X] T009 [P] Crear en `Domain/Pagos/` las entidades de registro según data-model.md: `PagoProcesado.cs` (`PagoId`, `CarritoId`, `Monto` (`Dinero`), `AprobadoEn`, `Resultado` (`ResultadoPagoProcesado`: `Acreditado`, `Devuelto`), `ProcesadoEn`), `MotivoDevolucion.cs` (`PagoTardio`, `CarritoYaPagado`, `AsientosNoDisponibles`, `MontoDistinto`) y `Devolucion.cs` (`Id`, `PagoId`, `CarritoId`, `Monto`, `Motivo`, `Estado` (`EstadoDevolucion`: `Pendiente`, `Completada`, `Fallida`), `SolicitadaEn`, `ResueltaEn`, `UltimoError`; métodos `Completar(ahora)` y `Fallar(error, ahora)`, idempotentes y que no salen de un estado final)
- [X] T010 Renombrar `ICarritoRepository.ListarActivosDeFuncion` a `ListarQueRetienenAsientos(funcionId, ahora)` en `Domain/Carritos/ICarritoRepository.cs` y en `Infra/Persistence/Carritos/CarritoRepository.cs`, con el filtro `(Estado IN (Activo, Pagando) AND VenceEn > ahora) OR Estado = Pagado`; aplicar el mismo filtro en `Infra/Carritos/CarritosRetienenAsientos.cs`; actualizar las llamadas en `App/Carritos/Commands/CrearCarritoCommand.cs` y `App/Carritos/Commands/ReemplazarSeleccionCommand.cs` y los mocks de `AppTests/Carritos/`

### Application: puertos

- [X] T011 [P] Crear `App/Pagos/Ports/IProcesadorDePagos.cs` según research.md Decisión 2, con los records `ItemDePago(string Titulo, decimal PrecioUnitario)`, `SolicitudDePago(Guid CarritoId, IReadOnlyList<ItemDePago> Items, Dinero Total, DateTimeOffset VenceEn, string EmailPagador)`, `PagoCreado(string PreferenciaId, string UrlPago)`, `PagoConsultado(string Id, EstadoPagoExterno Estado, Dinero Monto, string? ReferenciaExterna, DateTimeOffset? AprobadoEn)` y el enum `EstadoPagoExterno` (`Aprobado`, `Rechazado`, `Pendiente`, `Devuelto`, `Otro`); y `App/Pagos/ProcesadorDePagosNoDisponibleException.cs`, que las implementaciones lanzan ante fallas de red o respuestas 5xx
- [X] T012 [P] Crear `App/Pagos/Ports/IVerificadorDeAvisos.cs` (`bool EsValido(string? firma, string? requestId, string? dataId)`) y `App/Pagos/Ports/IRegistroDePagos.cs` (`Task<bool> YaProcesado(string pagoId)`, `Task Registrar(PagoProcesado)`, `Task AgregarDevolucion(Devolucion)`, `Task<Devolucion?> ObtenerDevolucion(Guid id)`)
- [X] T013 [P] Crear `App/Compartido/Ports/IManejadorDeMensaje.cs`: `string Tipo { get; }`, `Task Manejar(string payload, CancellationToken)` y `Task AlAgotarIntentos(string payload, string error, CancellationToken)` con implementación por defecto vacía

### Infrastructure: persistencia y migración

- [X] T014 Extender `Infra/Persistence/Carritos/CarritoEntityConfiguration.cs` con las columnas nuevas de data-model.md (largos y conversiones a UTC como las existentes) y `OwnsMany(c => c.Entradas)` en la tabla `Entradas` (PK `Id`, FK `CarritoId`, `Fila`/`Numero` como en `CarritoAsientos`, `PrecioUnitario` como complex property, índice único `Codigo` e índice único `(FuncionId, Fila, Numero)`, navegación por campo `_entradas`)
- [X] T015 [P] Crear `Infra/Persistence/Pagos/PagoProcesadoEntityConfiguration.cs` (tabla `PagosProcesados`, PK `PagoId` varchar(50)) y `Infra/Persistence/Pagos/DevolucionEntityConfiguration.cs` (tabla `Devoluciones`, índice único `PagoId`, enums como string); agregar los `DbSet` en `Infra/Persistence/CinemaDbContext.cs`
- [X] T016 [P] Agregar a `Infra/Persistence/Outbox/OutboxMensaje.cs` las propiedades `Intentos`, `ProximoIntentoEn`, `UltimoError` (máx. 1000) y `FallidoEn`, y en `OutboxMensajeEntityConfiguration.cs` sus columnas y el índice `(ProcesadoEn, FallidoEn, ProximoIntentoEn)`
- [X] T017 Crear `Infra/Persistence/Pagos/RegistroDePagos.cs` (implementa `IRegistroDePagos` sobre `CinemaDbContext`, sin `SaveChanges`: lo hace la unidad de trabajo)
- [X] T018 Generar la migración `AgregarPagoDeCarritos` con `dotnet ef migrations add AgregarPagoDeCarritos --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api` y revisar el archivo generado en `Infra/Migrations/`: solo agrega columnas, tablas e índices; ningún `DropTable`, `DropColumn` ni borrado de datos
- [X] T019 [P] Crear `IntTests/Carritos/MigracionPagoDeCarritosTests.cs` siguiendo el patrón de `MigracionCarritosTests.cs`: migrar hasta la migración anterior, insertar un carrito activo con asientos, migrar hasta `AgregarPagoDeCarritos` y verificar que el carrito y sus asientos siguen intactos, con las columnas nuevas en nulo, y que existen los índices únicos de `Entradas` y `Devoluciones`

### Infrastructure: procesador del Outbox

- [X] T020 [P] Crear `IntTests/Outbox/ProcesadorDeOutboxTests.cs` (Testcontainers, con manejadores de prueba registrados en el DI): un mensaje con manejador queda con `ProcesadoEn`; una falla incrementa `Intentos`, guarda `UltimoError` y fija `ProximoIntentoEn` en el futuro, y el mensaje no se toma antes de ese momento; al llegar a `MaximoIntentos` queda con `FallidoEn` y se llama a `AlAgotarIntentos`; un tipo sin manejador queda procesado sin acción; dos `ProcesarLote` concurrentes nunca ejecutan el mismo mensaje dos veces
- [X] T021 Crear `Infra/Outbox/OpcionesOutbox.cs` e `Infra/Outbox/ProcesadorDeOutbox.cs` (`BackgroundService`) según research.md Decisión 10: método público `ProcesarLote(CancellationToken)` (toma hasta 20 mensajes con `FOR UPDATE SKIP LOCKED` y lease de 5 minutos en una transacción corta; ejecuta cada manejador fuera de la transacción en su propio scope; espera `min(2^Intentos × 10 s, 1 h)` entre reintentos); `ExecuteAsync` llama a `ProcesarLote` cada `IntervaloSegundos` si `Habilitado`. Usar `IReloj` para "ahora". Los errores se registran en el log sin el payload

### Infrastructure: procesador simulado y configuración

- [X] T022 [P] Crear `Infra/Pagos/OpcionesMercadoPago.cs` (`Modo` (`Simulado` | `Real`), `AccessToken`, `ClaveSecretaAvisos`, `UrlAvisos`, `UrlRetorno`, `SegundosTimeoutConsulta`) con un método de validación: con `Real` exige `AccessToken`, `ClaveSecretaAvisos` y `UrlAvisos`; con `Simulado` exige `ClaveSecretaAvisos`
- [X] T023 [P] Crear `Infra/Pagos/ProcesadorDePagosSimulado.cs` según research.md Decisión 2 (`Singleton`, `ConcurrentDictionary`): `CrearPago` guarda la solicitud y devuelve `PreferenciaId` `sim-pref-{guid}` y `UrlPago` `/api/dev/pagos-simulados/{preferenciaId}`; `RegistrarPago(carritoId, estado, monto, aprobadoEn)` devuelve un id `sim-{guid}`; `ObtenerPago`, `BuscarPagosDelCarrito` y `Devolver` (guarda las devoluciones por clave de idempotencia y no duplica). Propiedades para tests: `Solicitudes`, `Devoluciones` y `FallarProximasLlamadas(int cantidad)` (lanza `ProcesadorDePagosNoDisponibleException`) y `DemoraDeConsulta` (simula lentitud)
- [X] T024 [P] Crear `Infra/Pagos/GeneradorDeCodigosCrockford.cs` (`IGeneradorDeCodigos`: 10 caracteres del alfabeto `0123456789ABCDEFGHJKMNPQRSTVWXYZ` con `RandomNumberGenerator`, formato `XXXXX-XXXXX`) y su test `IntTests/Pagos/GeneradorDeCodigosCrockfordTests.cs` (formato, alfabeto sin `I`, `L`, `O`, `U`, 1000 códigos sin repetir)
- [X] T025 Registrar en `Api/Program.cs`: `OpcionesMercadoPago` (bind + validación al arrancar, salvo en `GetDocument.Insider` como `OpcionesInvitados`), `IProcesadorDePagos` → `ProcesadorDePagosSimulado` cuando `Modo == Simulado` (la implementación real se registra en US1), `IRegistroDePagos` (`Scoped`), `IGeneradorDeCodigos` (`Singleton`), `OpcionesOutbox` y `AddHostedService<ProcesadorDeOutbox>` más el propio `ProcesadorDeOutbox` resoluble para tests
- [X] T026 Adaptar `IntTests/Carritos/CarritosApiFactory.cs`: configuración `MercadoPago:Modo=Simulado`, `MercadoPago:ClaveSecretaAvisos` con una constante de prueba nueva en `IntTests/Compartido/TokensDePrueba.cs` (`ClaveAvisosDePrueba`), `Outbox:Habilitado=false`; propiedad `Pagos` (la instancia de `ProcesadorDePagosSimulado`, registrada como `Singleton` en lugar de la del DI); método `ProcesarOutbox()` que resuelve `ProcesadorDeOutbox` y llama a `ProcesarLote` hasta que no queden mensajes listos; `LimpiarDatos` borra también `Devoluciones` y `PagosProcesados` (las `Entradas` caen con los carritos)
- [X] T027 Ejecutar `dotnet build` y `dotnet test`: todo verde (los tests nuevos de esta fase incluidos) *(verificado: 179 tests de integración en verde)*

**Checkpoint**: la base soporta los estados y tablas nuevas; ninguna API cambió todavía.

---

## Phase 3: User Story 1 — Pagar el carrito y quedarme con mis asientos (Priority: P1) 🎯 MVP

**Goal**: iniciar el pago (URL, `Pagando`, +1 minuto acotado al inicio) y acreditar un pago aprobado a tiempo por el total (Pagado, una entrada por asiento, asientos vendidos para siempre). Sin cambios de selección, email ni abandono durante el pago.

**Independent Test**: con el procesador simulado, crear un carrito con email, `POST /api/carritos/{id}/pago`, simular un pago aprobado con `POST /api/dev/pagos-simulados` y verificar `Pagado`, una entrada por asiento en `GET /api/carritos/{id}` y que los asientos no vuelven al mapa después del vencimiento original.

### Tests for User Story 1

- [X] T028 [P] [US1] Agregar a `IntTests/Carritos/CarritosTestHelpers.cs` los helpers `CrearCarritoConEmail(factory, funcionId, asientos, email)` (devuelve id y token), `IniciarPago(cliente, carritoId)` y `SimularPago(cliente, carritoId, estado, monto?, aprobadoEn?)`
- [X] T029 [P] [US1] Agregar a `DomainTests/Carritos/CarritoTests.cs`: `VencimientoAlIniciarPago` (vence 20:05, inicio 21:00 → 20:06; inicio 20:05:30 → 20:05:30); `IniciarPago` devuelve `SinEmailEntrega`, `NoActivo` (expirado y abandonado), `YaEnPago` con la URL guardada sin tocar `VenceEn`, `SeleccionModificada` y `Iniciado` (estado, vencimiento, preferencia, URL y momento); `ReemplazarAsientos` (vía `AsignadorDeAsientos.Reemplazar`), `Abandonar` e `IndicarEmailEntrega` en `Pagando` devuelven `EnPago` sin cambiar nada; `MarcarPagado` deja `Pagado`, guarda pago y momentos y crea una `Entrada` por asiento con los datos de la función y el precio unitario, con los códigos de un generador falso; completar los casos pendientes de T006
- [X] T030 [P] [US1] Crear `DomainTests/Pagos/ConfirmadorDePagosTests.cs` con un caso por regla y por orden según research.md Decisión 6: carrito `Pagado` → `Devolver(CarritoYaPagado)`; `Activo` o `Abandonado` guardados → `Devolver(PagoTardio)`; monto distinto o moneda distinta → `Devolver(MontoDistinto)` (aunque además esté vencido, gana el orden); `AprobadoEn` posterior a `VenceEn` → `Devolver(PagoTardio)`; `AprobadoEn` igual a `VenceEn` → `Acreditar`; vencido al procesar, aprobado a tiempo, con un asiento retenido por otro carrito activo, en pago o pagado → `Devolver(AsientosNoDisponibles)`; vencido, aprobado a tiempo, asientos libres → `Acreditar`; vigente → `Acreditar`
- [X] T031 [P] [US1] Crear `AppTests/Carritos/IniciarPagoCommandHandlerTests.cs` (NSubstitute, patrón de `OperacionesSobreCarritoTestBase.cs`): token ajeno → `CarritoNoEncontrado` sin llamar al procesador; sin email → `EmailEntregaFaltante` sin llamar al procesador; ya en `Pagando` → misma URL sin llamar al procesador; el procesador lanza `ProcesadorDePagosNoDisponibleException` → `PagoNoDisponible` y no se abre la transacción; la `SolicitudDePago` lleva un ítem por asiento con el precio unitario, el total, el email y `VenceEn` = `VencimientoAlIniciarPago`; éxito → `Ok` con la URL y el evento `PagoIniciado` registrado; otro inicio ganó la carrera dentro de la transacción → devuelve la URL guardada; la selección cambió → `CarritoModificado`
- [X] T032 [P] [US1] Crear `AppTests/Pagos/ProcesarPagoCommandHandlerTests.cs`: pago inexistente → `Ignorado`; referencia que no es un Guid o carrito inexistente → `Ignorado`; pago no aprobado → `NoAprobado` sin abrir la transacción; pago ya en `IRegistroDePagos` → `YaProcesado` sin cambios; decisión `Acreditar` → carrito `Pagado`, `PagoProcesado` con resultado `Acreditado` y evento `CarritoPagado`; decisión `Devolver` → `PagoProcesado` con resultado `Devuelto`, `Devolucion` pendiente con el monto completo del pago y evento `DevolucionSolicitada`; la `ProcesadorDePagosNoDisponibleException` se propaga
- [X] T033 [P] [US1] Adaptar `AppTests/Carritos/ReemplazarSeleccionCommandHandlerTests.cs`, `AbandonarCarritoCommandHandlerTests.cs` e `IndicarEmailEntregaCommandHandlerTests.cs`: con el carrito en `Pagando` devuelven `CarritoEnPago` y no registran eventos
- [X] T034 [P] [US1] Crear `IntTests/Pagos/MapeoMercadoPagoTests.cs` (sin red): la `SolicitudDePago` se traduce a un `PreferenceRequest` con `external_reference` = id del carrito, un ítem por asiento en ARS, `expires = true`, `expiration_date_to` = `VenceEn`, `excluded_payment_types` `ticket` y `atm`, `installments = 1`, `binary_mode = true`, `notification_url` y `back_urls` con `{carritoId}` reemplazado; `auto_return = "approved"` solo con URL `https`; un `Payment` del SDK se traduce a `PagoConsultado` (`approved` → `Aprobado`, `rejected` → `Rechazado`, `pending`/`in_process` → `Pendiente`, `refunded` → `Devuelto`, otro → `Otro`; monto, `date_approved` y `external_reference`)
- [X] T035 [P] [US1] Crear `IntTests/Pagos/IniciarPagoApiTests.cs` (contra `CarritosApiFactory`): `200` con `urlPago`, `estado` `Pagando` y `venceEn` + 1 minuto; segunda llamada → misma `urlPago` y mismo `venceEn`; función que empieza antes del minuto extra → `venceEn` = inicio; sin email → `409 email-entrega-faltante`; expirado o abandonado → `409 carrito-no-activo`; procesador fallando → `503 pago-no-disponible` y el carrito sigue `Activo` con el `venceEn` original; sin token → `401`; token ajeno → `404`; en `Pagando`, `PUT …/asientos`, `POST …/abandono` y `PUT …/email-entrega` → `409 carrito-en-pago`
- [X] T036 [P] [US1] Crear `IntTests/Pagos/PagoAcreditadoTests.cs`: carrito con C5 y C6, pago iniciado y pago simulado aprobado por el total → `GET` muestra `Pagado`, `pago.pagadoEn` y dos entradas con código `XXXXX-XXXXX` distinto, película, sucursal, sala, horario, asiento y precio; tras `Reloj.Avanzar` más allá del vencimiento, el mapa sigue sin C5 y C6 y el carrito sigue `Pagado`; un pago rechazado deja `Pagando`; un carrito en `Pagando` vigente cuenta para el tope por función y sus asientos no figuran en el mapa; al vencer sin pago, figura `Expirado` y sus asientos vuelven al mapa
- [X] T037 [US1] Extender `IntTests/Carritos/CarritoConcurrenteTests.cs` (concurrencia obligatoria): 10 procesamientos simultáneos del mismo pago aprobado (vía `ProcesarPagoCommandHandler` en scopes separados) → el carrito queda `Pagado`, exactamente N entradas, una fila en `PagosProcesados` y un único mensaje `CarritoPagado` en el Outbox; dos `POST …/pago` simultáneos → ambos `200` con la misma `urlPago` en la respuesta final y `VenceEn` extendido una sola vez

### Implementation for User Story 1

- [X] T038 [US1] En `Domain/Carritos/Carrito.cs` implementar `VencimientoAlIniciarPago(inicioFuncion)`, `IniciarPago(preferenciaId, urlPago, asientosEsperados, ahora, inicioFuncion)`, `MarcarPagado(PagoAprobado, ahora, DatosDeLaFuncion, IGeneradorDeCodigos)` y `MarcarEntregaEnviada(ahora)` según data-model.md; crear `Domain/Carritos/ResultadoInicioPago.cs` (`Iniciado`, `YaEnPago(url)`, `NoActivo(estado)`, `SinEmailEntrega`, `SeleccionModificada`) y `Domain/Carritos/DatosDeLaFuncion.cs` (record con título, sucursal, sala y hora de inicio); agregar `EnPago` a `ResultadoAbandono`, `ResultadoEmailEntrega` y `TipoResultadoSeleccion.CarritoEnPago` (en `AsignadorDeAsientos.Reemplazar`), devueltos cuando el estado efectivo es `Pagando`
- [X] T039 [P] [US1] Crear `Domain/Pagos/PagoAprobado.cs` (value object: `PagoId` no vacío, `Monto` `Dinero`, `AprobadoEn`) y `Domain/Pagos/ConfirmadorDePagos.cs` con `DecisionDePago` (`Acreditar` | `Devolver(MotivoDevolucion)`) y el método `Decidir(carrito, carritosQueRetienenDeLaFuncion, pago, ahora)` con las reglas en el orden de research.md Decisión 6
- [X] T040 [P] [US1] Crear los eventos `Domain/Carritos/Eventos/PagoIniciado.cs` (`CarritoId`, `FuncionId`, `VenceEn`, `OcurridoEn`), `Domain/Carritos/Eventos/CarritoPagado.cs` (`CarritoId`, `FuncionId`, `PagoId`, `Asientos`, `OcurridoEn`) y `Domain/Pagos/Eventos/DevolucionSolicitada.cs` (`DevolucionId`, `PagoId`, `CarritoId`, `Motivo`, `OcurridoEn`)
- [X] T041 [US1] En `App/Carritos/ResultadoOperacionCarrito.cs` agregar `EmailEntregaFaltante`, `CarritoEnPago`, `CarritoModificado` y `PagoNoDisponible` (con fábricas) y `UrlPago` opcional; mapear `CarritoEnPago` desde `DesdeRechazo`, y desde los resultados de abandono y email en `AbandonarCarritoCommand.cs` e `IndicarEmailEntregaCommand.cs`
- [X] T042 [US1] Extender `App/Carritos/Ports/IConsultaCarritos.cs` (`CarritoLecturaDto` con `PreferenciaPagoId`, `UrlPago`, `PagoIniciadoEn`, `PagadoEn` y `Entradas` como `EntradaLecturaDto`) y `App/Carritos/Dtos/CarritoDto.cs` (`PagoDto?` con `UrlPago`, `IniciadoEn`, `PagadoEn`, y `IReadOnlyList<EntradaDto>`); actualizar `Infra/Persistence/Carritos/ConsultaCarritos.cs` para leer los campos nuevos y las entradas
- [X] T043 [US1] Crear `App/Carritos/Commands/IniciarPagoCommand.cs` (`IniciarPagoCommand(CarritoId, CarritoDelToken)` y su handler) con los tres pasos de research.md Decisión 4: lectura con `IConsultaCarritos` y validaciones previas; `IProcesadorDePagos.CrearPago` fuera de la transacción (los títulos de ítem: "Película — Sala — Fila C Asiento 5"); transacción con bloqueo de la función (`IFuncionRepository.ObtenerConSalaBloqueando`) y `Carrito.IniciarPago`; registra `PagoIniciado` en el Outbox; lee el carrito después de confirmar y devuelve `Ok` con la URL
- [X] T044 [US1] Crear `App/Pagos/Commands/ProcesarPagoCommand.cs` (`ProcesarPagoCommand(string PagoId)`, handler y enum `ResultadoProcesamientoPago`: `Ignorado`, `NoAprobado`, `YaProcesado`, `Acreditado`, `Devuelto`) según research.md Decisión 6: consulta fuera de la transacción; dentro, bloqueo de la función del carrito, `IRegistroDePagos.YaProcesado`, carritos que retienen asientos, `ConfirmadorDePagos.Decidir`, y según la decisión `MarcarPagado` + `CarritoPagado` o `Devolucion` + `DevolucionSolicitada`, siempre con el `PagoProcesado`. Registrar en el log (sin datos sensibles) los pagos ignorados y las devoluciones decididas
- [X] T045 [US1] Crear `Infra/Pagos/MapeoMercadoPago.cs` (clase estática pública con la traducción `SolicitudDePago` → `PreferenceRequest` y `Payment` → `PagoConsultado` de T034) e `Infra/Pagos/ProcesadorDePagosMercadoPago.cs` (`IProcesadorDePagos` con `PreferenceClient`, `PaymentClient` (`GetAsync` y `SearchAsync` por `external_reference`) y `PaymentRefundClient`; `RequestOptions` con el `AccessToken` y `X-Idempotency-Key`; devuelve `sandbox_init_point`; traduce `MercadoPagoApiException` 5xx y errores de red a `ProcesadorDePagosNoDisponibleException`; nunca registra el token ni respuestas completas)
- [X] T046 [US1] En `Api/Modulos/Carritos/CarritosController.cs` agregar `POST api/carritos/{carritoId:guid}/pago` (portador del carrito, documentación XML y `ProducesResponseType` como el resto) que invoca `IniciarPagoCommandHandler` y responde `PagoIniciadoResponse(urlPago, carrito)`; mapear en `Problema` los códigos `email-entrega-faltante` (409), `carrito-en-pago` (409), `carrito-modificado` (409) y `pago-no-disponible` (503) según contracts/pagos-api.md. En `Api/Modulos/Carritos/Contracts/` agregar `PagoIniciadoResponse`, `PagoResponse` y `EntradaResponse`, y los campos `Pago` y `Entradas` a `CarritoResponse` (actualizar el comentario de `Estado` con los cinco estados)
- [X] T047 [US1] Crear `Api/Modulos/Pagos/PagosSimuladosController.cs` (`[AllowAnonymous]` explícito, `[ApiExplorerSettings(IgnoreApi = true)]`): `POST api/dev/pagos-simulados` (`PagoSimuladoRequest(carritoId, estado, monto?, aprobadoEn?)` → registra el pago en `ProcesadorDePagosSimulado` con el total del carrito por defecto, invoca `ProcesarPagoCommandHandler` y responde `PagoSimuladoResponse(pagoId, estadoCarrito)`) y `GET api/dev/pagos-simulados/{preferenciaId}` (texto que explica cómo simular el pago). Un filtro `Api/Modulos/Pagos/SoloSimulacionAttribute.cs` responde `404` si el entorno no es Development ni Testing o si `MercadoPago:Modo` no es `Simulado`
- [X] T048 [US1] Registrar en `Api/Program.cs`: `ConfirmadorDePagos` (`Singleton`), `IniciarPagoCommandHandler` y `ProcesarPagoCommandHandler` (`Scoped`), `ProcesadorDePagosMercadoPago` (`Scoped`) cuando `MercadoPago:Modo == Real`
- [X] T049 [US1] Ejecutar `dotnet test`: los tests de US1 y los anteriores pasan *(verificado: 179 tests de integración en verde)*

**Checkpoint**: un comprador paga su carrito con el procesador simulado y queda con sus entradas.

---

## Phase 4: User Story 5 — Que nadie pueda fingir un pago (Priority: P1)

**Goal**: endpoint público de avisos protegido por la firma `x-signature`, que decide solo con datos consultados a Mercado Pago.

**Independent Test**: enviar avisos sin firma, con firma alterada y con otra clave → `401` sin cambios; un aviso firmado de un pago rechazado deja `Pagando`; uno firmado de un pago aprobado deja `Pagado`.

### Tests for User Story 5

- [X] T050 [P] [US5] Crear `IntTests/Pagos/VerificadorDeAvisosMercadoPagoTests.cs`: firma válida calculada con el manifest `id:{dataId};request-id:{requestId};ts:{ts};` y HMAC-SHA256 hex; `v1` alterado; otra clave; `x-signature` ausente, sin `ts` o sin `v1`; espacios alrededor de las partes del header; `data.id` alfanumérico en mayúsculas se compara en minúsculas; sin `x-request-id` el manifest omite esa parte
- [X] T051 [P] [US5] Crear `AppTests/Pagos/RecibirAvisoDePagoCommandHandlerTests.cs`: firma inválida → `FirmaInvalida` sin consultar nada; tipo distinto de `payment` → `Ignorado`; sin `data.id` → `Ignorado`; `payment` válido → invoca `ProcesarPago` con el `data.id`; `ProcesadorDePagosNoDisponibleException` → `ProcesadorNoDisponible`
- [X] T052 [P] [US5] Crear `IntTests/Pagos/AvisosMercadoPagoApiTests.cs` (firma calculada en el test con `TokensDePrueba.ClaveAvisosDePrueba`): sin firma, alterada u otra clave → `401` y el carrito sigue `Pagando`; firmado con pago aprobado (registrado en `factory.Pagos`) → `200` y `Pagado`; firmado con pago rechazado → `200` y sigue `Pagando`; `type=merchant_order` → `200` sin cambios; pago con referencia desconocida → `200`; procesador fallando → `500` y, al reenviar el mismo aviso sin falla, `200` y `Pagado`; el cuerpo del aviso diciendo "approved" con un pago rechazado en el simulador no cambia nada
- [X] T053 [P] [US5] Actualizar `IntTests/Carritos/EndpointsPublicosTests.cs`: `POST /api/pagos/mercadopago/avisos` responde sin token (no `401` por falta de autenticación cuando la firma es válida) y `POST /api/carritos/{id}/pago` exige el token del carrito; en una factory con entorno distinto de Development/Testing (o `Modo=Real` con opciones falsas), `/api/dev/pagos-simulados` responde `404`

### Implementation for User Story 5

- [X] T054 [US5] Crear `Infra/Pagos/VerificadorDeAvisosMercadoPago.cs` (`IVerificadorDeAvisos`, `Singleton`) según research.md Decisión 7, con `CryptographicOperations.FixedTimeEquals` sobre los bytes del hex y sin lanzar excepciones ante headers mal formados
- [X] T055 [US5] Crear `App/Pagos/Commands/RecibirAvisoDePagoCommand.cs` (`RecibirAvisoDePagoCommand(Firma, RequestId, DataId, Tipo)`, handler y enum `ResultadoAviso`: `FirmaInvalida`, `Ignorado`, `Procesado`, `ProcesadorNoDisponible`)
- [X] T056 [US5] Crear `Api/Modulos/Pagos/AvisosMercadoPagoController.cs`: `POST api/pagos/mercadopago/avisos`, `[AllowAnonymous]` explícito con comentario sobre la protección por firma; lee `x-signature` y `x-request-id` de los headers, `data.id` y `type` del query (`[FromQuery(Name = "data.id")]`) y, si `type` no viene en el query, del cuerpo JSON (`AvisoMercadoPagoRequest` con `type` y `data.id`, solo para eso); traduce `FirmaInvalida` → `401`, `ProcesadorNoDisponible` → `500`, el resto → `200`, sin lógica de negocio
- [X] T057 [US5] Registrar en `Api/Program.cs` `IVerificadorDeAvisos` y `RecibirAvisoDePagoCommandHandler`, y ejecutar `dotnet test` *(verificado: 179 tests de integración en verde)*

**Checkpoint**: el pago se confirma por el webhook y un aviso falso no tiene efecto.

---

## Phase 5: User Story 2 — Volver de Mercado Pago y saber en qué quedó mi compra (Priority: P1)

**Goal**: la consulta del carrito distingue `Pagando`, `Pagado` y `Expirado` y, en `Pagando`, verifica con Mercado Pago si el aviso todavía no llegó (FR-017b).

**Independent Test**: iniciar el pago, consultar (`Pagando` con `urlPago`), registrar un pago aprobado en el simulador sin procesarlo, consultar (`Pagado`), enviar después el aviso (sin duplicados); en otro carrito dejar vencer sin pago (`Expirado`).

### Tests for User Story 2

- [X] T058 [P] [US2] Ampliar `AppTests/Carritos/ObtenerCarritoQueryHandlerTests.cs`: carrito guardado en `Pagando` → llama a `BuscarPagosDelCarrito` y procesa con `ProcesarPago` cada pago aprobado que no esté en `IRegistroDePagos`; los ya procesados y los no aprobados no se procesan; `Activo`, `Pagado`, `Abandonado` → no llama al procesador; el procesador lanza o supera el timeout → responde el carrito sin error; token ajeno → `null` sin llamar al procesador
- [X] T059 [P] [US2] Crear `IntTests/Pagos/ConsultaDuranteElPagoTests.cs`: `Pagando` sin pago → `estado` `Pagando`, `pago.urlPago` y sin entradas; pago aprobado registrado en `factory.Pagos` sin aviso → el `GET` devuelve `Pagado` con entradas; después llega el aviso firmado del mismo pago → sin entradas nuevas, una sola fila en `PagosProcesados` y un solo `CarritoPagado` en el Outbox; procesador lento (`DemoraDeConsulta` mayor al timeout) o fallando → `200` con `Pagando`; vencido sin pago → `Expirado` y asientos de vuelta en el mapa; aprobado a tiempo y consultado después del vencimiento con asientos libres → `Pagado`

### Implementation for User Story 2

- [X] T060 [US2] En `App/Carritos/Queries/ObtenerCarritoQuery.cs` implementar la verificación de research.md Decisión 9: si el estado guardado es `Pagando`, `BuscarPagosDelCarrito` con un `CancellationTokenSource` enlazado y `CancelAfter(SegundosTimeoutConsulta)` (recibido como opción en el constructor), procesar cada pago aprobado pendiente con `ProcesarPagoCommandHandler`, capturar y registrar en el log `ProcesadorDePagosNoDisponibleException` y `OperationCanceledException` del timeout, y después leer y responder
- [X] T061 [US2] Actualizar el registro de `ObtenerCarritoQueryHandler` en `Api/Program.cs` con sus dependencias nuevas, la documentación XML de `Obtener` en `Api/Modulos/Carritos/CarritosController.cs` (estados y verificación en `Pagando`) y ejecutar `dotnet test` *(verificado: 179 tests de integración en verde)*

**Checkpoint**: el front puede esperar el resultado consultando el carrito, incluso sin webhook.

---

## Phase 6: User Story 3 — Recibir mis entradas por email (Priority: P2)

**Goal**: un único email con todas las entradas, enviado desde el Outbox con reintentos, sin deshacer la compra si falla. SMTP con Mailpit en desarrollo.

**Independent Test**: completar una compra con el simulador, procesar el Outbox y verificar un único email al email de entrega con todas las entradas; simular una falla del envío, verificar el reintento y que el carrito siguió `Pagado`.

### Tests for User Story 3

- [X] T062 [P] [US3] Crear `AppTests/Pagos/EnviarEmailDeEntregaTests.cs`: arma un `MensajeDeEmail` al email de entrega con asunto que nombra la película y la función, y un cuerpo con una línea por entrada (código, película, sucursal, sala, horario, asiento y precio); carrito con `EntregaEnviadaEn` → no envía; envío exitoso → marca `EntregaEnviadaEn`; el envío lanza → la excepción se propaga (la reintenta el Outbox) y no marca nada
- [X] T063 [P] [US3] Crear `IntTests/Pagos/EmailDeEntregaTests.cs`: compra de dos asientos + `factory.ProcesarOutbox()` → `factory.Email.Mensajes` tiene exactamente un mensaje a `ana@example.com` con los dos códigos; el mismo pago procesado dos veces (simulador y aviso) → un solo email; con un enviador que falla la primera vez (doble configurable en `IntTests/Invitados/EnviadorDeEmailEnMemoria.cs`: `FallarProximosEnvios(int)`), el primer `ProcesarOutbox` no envía, el carrito sigue `Pagado` y, tras `Reloj.Avanzar` más allá del `ProximoIntentoEn`, el segundo lo envía

### Implementation for User Story 3

- [X] T064 [P] [US3] Crear `App/Pagos/MensajeDeEntrega.cs` (construye asunto, texto plano y HTML simple a partir del `CarritoDto` con sus entradas) y ampliar `MensajeDeEmail` en `App/Compartido/Ports/IEnviadorDeEmail.cs` con un `CuerpoHtml` opcional; actualizar el comentario del puerto (ya lo usa la feature de pago)
- [X] T065 [US3] Crear `App/Pagos/Manejadores/EnviarEmailDeEntrega.cs` (`IManejadorDeMensaje`, `Tipo = "CarritoPagado"`): deserializa el evento, lee el carrito con `IConsultaCarritos`, si ya tiene `EntregaEnviadaEn` termina, envía con `IEnviadorDeEmail` y en una transacción corta llama a `Carrito.MarcarEntregaEnviada`
- [X] T066 [P] [US3] Crear `Infra/Email/OpcionesEmail.cs` (`Proveedor`, `Remitente`, `Smtp: Host, Puerto, Usuario, Clave, UsarTls`) e `Infra/Email/EnviadorDeEmailSmtp.cs` con MailKit (`SmtpClient` async, `SecureSocketOptions` según `UsarTls`, autenticación solo si hay usuario, cuerpo multipart texto + HTML). Mover `Infra/Invitados/EnviadorDeEmailRegistrador.cs` y `EnviadorDeEmailNoConfigurado.cs` a `Infra/Email/` (namespace `…Infrastructure.Email`) y actualizar el mensaje de error de `EnviadorDeEmailNoConfigurado` (configurar `Email:Proveedor=Smtp`)
- [X] T067 [US3] En `Api/Program.cs` reemplazar el registro actual de `IEnviadorDeEmail`: `Email:Proveedor == Smtp` → `EnviadorDeEmailSmtp` (con validación de `Host` al arrancar); `Registrador` en Development y Testing → `EnviadorDeEmailRegistrador`; en otro caso → `EnviadorDeEmailNoConfigurado`. Registrar `EnviarEmailDeEntrega` como `IManejadorDeMensaje` (`Scoped`) y ejecutar `dotnet test` *(verificado: 179 tests de integración en verde)*

**Checkpoint**: cada compra produce un email; en Development llega a Mailpit.

---

## Phase 7: User Story 4 — Que un pago tardío o incorrecto no se quede con el dinero (Priority: P2)

**Goal**: los pagos aprobados que no pueden acreditarse se devuelven completos, una sola vez, con reintentos y registro.

**Independent Test**: con el simulador, dejar vencer un carrito en `Pagando` y simular un pago aprobado después del vencimiento; verificar que no pasa a `Pagado`, que se pidió una devolución una sola vez y que queda `Completada` en `Devoluciones`. Repetir con monto distinto.

### Tests for User Story 4

- [X] T068 [P] [US4] Crear `AppTests/Pagos/EjecutarDevolucionTests.cs`: devolución `Pendiente` → llama a `Devolver(pagoId, devolucionId)` y la marca `Completada`; ya `Completada` → no llama; `Devolver` lanza → la excepción se propaga y la devolución sigue `Pendiente`; `AlAgotarIntentos` → la marca `Fallida` con el error
- [X] T069 [P] [US4] Crear `IntTests/Pagos/DevolucionesTests.cs`: (1) vence a las T, aprobado en T + 1 min → carrito `Expirado`, devolución `PagoTardio` y, tras `ProcesarOutbox`, una entrada en `factory.Pagos.Devoluciones` y estado `Completada`; (2) aprobado en T − 10 s y procesado en T + 20 s con asientos libres → `Pagado`, sin devolución; (3) igual pero con un asiento tomado por otro carrito creado después del vencimiento → `Expirado` y devolución `AsientosNoDisponibles`; (4) carrito `Pagado` con P1 y llega P2 aprobado → devolución `CarritoYaPagado`, carrito y entradas sin cambios; (5) monto distinto en carrito vigente → sigue `Pagando`, devolución `MontoDistinto`; (6) el aviso de un pago ya devuelto llega otra vez → ninguna devolución nueva; (7) el simulador falla en `Devolver` la primera vez → tras avanzar el reloj y reprocesar, una sola devolución en el simulador y `Completada`; (8) falla siempre → `Fallida` tras `MaximoIntentos`
- [X] T070 [US4] Extender `IntTests/Carritos/CarritoConcurrenteTests.cs` (concurrencia obligatoria): dos pagos aprobados distintos del mismo carrito procesados en paralelo → exactamente uno acreditado y el otro con devolución `CarritoYaPagado`, N entradas; un pago aprobado a tiempo de un carrito vencido procesado en paralelo con la creación de otro carrito que pide sus mismos asientos → nunca quedan a la vez entradas y un carrito que retiene el mismo asiento (o se acredita y la creación falla con `asientos-no-disponibles`, o la creación gana y el pago se devuelve con `AsientosNoDisponibles`)

### Implementation for User Story 4

- [X] T071 [US4] Crear `App/Pagos/Manejadores/EjecutarDevolucion.cs` (`IManejadorDeMensaje`, `Tipo = "DevolucionSolicitada"`): lee la `Devolucion` con `IRegistroDePagos`; si no está `Pendiente`, termina; llama a `IProcesadorDePagos.Devolver(pagoId, devolucion.Id)` fuera de la transacción; en una transacción corta, `Completar`. En `AlAgotarIntentos`, `Fallar` con el error y log de error con el id de la devolución y del pago
- [X] T072 [US4] Registrar `EjecutarDevolucion` como `IManejadorDeMensaje` (`Scoped`) en `Api/Program.cs` y ejecutar `dotnet test` *(verificado: 179 tests de integración en verde)*

**Checkpoint**: las cinco historias funcionan de punta a punta.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T073 [P] Actualizar `README.md`: feature 008 en la tabla de features; flujo de compra (carrito → email → iniciar pago → Mercado Pago → `Pagado` → email); endpoints y modalidades (iniciar pago: portador; webhook: público con firma; `/api/dev/pagos-simulados`: solo Development/Testing); configuración `MercadoPago`, `Outbox` y `Email` con los secretos por user-secrets; Mailpit (http://localhost:8025); credenciales y tarjetas de prueba; "Deuda técnica conocida" con las simplificaciones de plan.md (email at-least-once, sin chequeo de antigüedad del `ts`, sin barrido periódico, credenciales de producción no detectables por formato, eventos sin manejador marcados procesados)
- [X] T074 [P] Agregar una nota de cabecera a `specs/007-carrito-anonimo/contracts/carritos-api.md`: los estados `Pagando` y `Pagado`, el endpoint de pago y el error `carrito-en-pago` están en la [feature 008](../../008-pago-mercado-pago/contracts/pagos-api.md)
- [X] T075 [P] Agregar a `Api/CinemaSystemRandomPlay.Api.http` los ejemplos: iniciar el pago, consultar el carrito en `Pagando`, simular un pago aprobado y uno con monto distinto, y un aviso sin firma (`401`)
- [X] T076 Buscar restos con `grep -rniE "stripe|ListarActivosDeFuncion|Invitados\.EnviadorDeEmail|llegará con la feature de pago" src tests README.md docker-compose.yml` y corregir lo que no corresponda (las migraciones históricas quedan); verificar con `git grep -nE "APP_USR-|TEST-[0-9]"` que no hay credenciales en el repositorio
- [X] T077 Ejecutar `dotnet build` (sin advertencias nuevas; regenera `docs/openapi.json`, que debe incluir `POST /api/carritos/{carritoId}/pago` y el webhook, y no los endpoints de simulación) y `dotnet test` completos; anotar los conteos por proyecto frente a la línea base de T001 *(0 advertencias; `docs/openapi.json` incluye el pago y el webhook y no la simulación; 158 Domain, 103 Application y 179 Integración en verde, frente a 126/57/— de la línea base)*
- [X] T078 Recorrer quickstart.md, sección 2 (modo simulado), con `docker compose up --build` en un proyecto de compose descartable propio (`docker compose -p cinema-008-prueba …`, sin tocar los volúmenes existentes): compra, email en Mailpit, pago duplicado, monto distinto, pago tardío y `carrito-en-pago`. La sección 3 (modo real) la ejecuta el usuario con sus credenciales de prueba *(hecho con contenedores descartables cinema008-pg y cinema008-mailpit y la API en Development por `dotnet run`, sin tocar los contenedores del proyecto: compra, email único en Mailpit, segundo pago y monto distinto devueltos y completados, `carrito-en-pago`, aviso sin firma 401)*

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias. T002 (constitución) va antes de cualquier código.
- **Foundational (Phase 2)**: depende de Setup y bloquea todas las historias. Dentro de la fase: T006–T010 → T011–T013 → T014–T019 → T020–T021 → T022–T026 → T027.
- **US1 (Phase 3)**: depende de la Fase 2. Es el MVP.
- **US5 (Phase 4)**: depende de US1 (`ProcesarPagoCommandHandler` y carritos en `Pagando`).
- **US2 (Phase 5)**: depende de US1. Es independiente de US5, salvo un caso de T059 que envía un aviso firmado: si US5 no está hecha, ese caso usa el endpoint de simulación.
- **US3 (Phase 6)**: depende de US1 (evento `CarritoPagado`) y del procesador del Outbox (Fase 2).
- **US4 (Phase 7)**: depende de US1 (la decisión `Devolver` ya registra la `Devolucion`) y del procesador del Outbox.
- **Polish (Phase 8)**: depende de todas.

### Within Each User Story

- Los tests se escriben primero y deben fallar.
- Dominio → Application → Infrastructure → Api → registro en `Program.cs`.
- `CarritoConcurrenteTests.cs` se extiende en US1 y US4 de forma secuencial (mismo archivo).
- `Api/Program.cs` y `CarritosController.cs` se tocan en varias historias: no paralelizar esas tareas entre historias.

### Parallel Opportunities

- T003, T004 y T005 entre sí (después de T002).
- En la Fase 2: T006, T009, T011, T012, T013, T015, T016, T019, T020, T022, T023 y T024 en archivos distintos.
- Los tests marcados [P] de cada historia entre sí.
- US3 y US4 se pueden hacer en paralelo después de US1 (tocan manejadores distintos; coordinar `Program.cs`).
- T073, T074 y T075 entre sí.

---

## Parallel Example: User Story 1

```text
T029 DomainTests/Carritos/CarritoTests.cs (pago)
T030 DomainTests/Pagos/ConfirmadorDePagosTests.cs
T031 AppTests/Carritos/IniciarPagoCommandHandlerTests.cs
T032 AppTests/Pagos/ProcesarPagoCommandHandlerTests.cs
T034 IntTests/Pagos/MapeoMercadoPagoTests.cs
T035 IntTests/Pagos/IniciarPagoApiTests.cs
T036 IntTests/Pagos/PagoAcreditadoTests.cs
```

## Parallel Example: User Story 5

```text
T050 IntTests/Pagos/VerificadorDeAvisosMercadoPagoTests.cs
T051 AppTests/Pagos/RecibirAvisoDePagoCommandHandlerTests.cs
T052 IntTests/Pagos/AvisosMercadoPagoApiTests.cs
T053 IntTests/Carritos/EndpointsPublicosTests.cs
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Fase 1: rama, constitución, paquetes y configuración.
2. Fase 2: estados, tablas, migración, puertos, simulador y Outbox.
3. Fase 3: iniciar el pago y acreditarlo con el simulador, con la concurrencia probada.
4. Validar US1 por separado: un carrito se paga y sus asientos quedan vendidos.

### Incremental Delivery

1. US1 → pago acreditado (simulado y con el adaptador real).
2. US5 → webhook firmado: habilita el modo real con túnel.
3. US2 → consulta que espera el resultado, también sin webhook.
4. US3 → email de entrega (Mailpit).
5. US4 → devoluciones automáticas.
6. Polish → documentación, suite completa y recorrido del quickstart.

---

## Notes

- No hacer push ni abrir el PR hasta que el usuario lo pida. Mostrar el resultado de `dotnet build` y `dotnet test` antes.
- No borrar volúmenes de Docker ni datos locales: para el recorrido manual, usar un proyecto de compose descartable propio.
- Nunca escribir credenciales de Mercado Pago en archivos versionados; los tests no llaman a Mercado Pago real.
- La migración `AgregarPagoDeCarritos` no descarta datos: si el archivo generado incluye un `Drop`, revisarlo antes de seguir.
