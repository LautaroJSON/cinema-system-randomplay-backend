# Implementation Plan: Pago del Carrito con Mercado Pago (credenciales de prueba)

**Branch**: `008-pago-mercado-pago` | **Date**: 2026-11-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-pago-mercado-pago/spec.md`

## Summary

El comprador anónimo paga su carrito con Mercado Pago Checkout Pro, en el entorno de pruebas. Al iniciar el pago el
carrito pasa a `Pagando`, gana un minuto de retención (sin pasar del inicio de la función) y recibe la URL de una
preferencia que vence en el mismo instante que el carrito. Mercado Pago avisa por webhook; la API valida la firma,
consulta el pago y, si se aprobó a tiempo por el total exacto, marca el carrito `Pagado` y emite una entrada por asiento.
El email con las entradas sale por el Outbox. Los pagos que no pueden acreditarse se devuelven solos.

Enfoque técnico:
- **Puerto `IProcesadorDePagos`** con un adaptador real (SDK oficial `mercadopago-sdk`) y uno simulado en memoria, más
  `IVerificadorDeAvisos` para la firma HMAC del webhook.
- **Carrito**: estados `Pagando` y `Pagado`, vencimiento extendido una sola vez, entradas dentro del agregado y rechazo
  `carrito-en-pago` para cambios durante el pago.
- **Confirmación**: domain service `ConfirmadorDePagos` decide acreditar o devolver. Un único caso de uso
  `ProcesarPago` lo usan el webhook, la consulta del carrito y el simulador. Idempotencia con la tabla `PagosProcesados`
  (PK = id del pago) bajo el bloqueo de la función de la 007.
- **Sin red dentro de transacciones**: crear la preferencia y consultar el pago van antes de la transacción; el email y
  las devoluciones salen por el Outbox.
- **Procesador del Outbox**: `BackgroundService` con lease, reintentos con espera exponencial y manejadores por tipo.
  Hoy no existe: lo necesitan el email y las devoluciones.
- **Email** por SMTP con MailKit; Mailpit en `docker-compose`.

Detalle de cada decisión en [research.md](./research.md).

## Technical Context

**Language/Version**: C# 12 / .NET 10 (fijo por la constitución)

**Primary Dependencies**: ASP.NET Core, EF Core 10 + Npgsql 10 (existentes). Nuevas: `mercadopago-sdk` 3.7.1 y MailKit
4.x, ambas solo en `Infrastructure`.

**Storage**: PostgreSQL, misma `CinemaDbContext`. Columnas nuevas en `Carritos` y `OutboxMensajes`; tablas nuevas
`Entradas`, `PagosProcesados` y `Devoluciones`. Una migración, no destructiva.

**Testing**: xUnit + NSubstitute para Domain y Application; Testcontainers.PostgreSql + `Microsoft.AspNetCore.Mvc.Testing`
para integración, con `ProcesadorDePagosSimulado` y `EnviadorDeEmailEnMemoria`.

**Target Platform**: Linux server (API backend, sin frontend propio)

**Project Type**: web-service (backend puro, REST/JSON)

**Performance Goals**: iniciar el pago es una llamada a Mercado Pago más una transacción corta. Consultar un carrito en
`Pagando` suma una búsqueda en Mercado Pago con timeout de 3 s; el resto de las consultas no cambia. El webhook responde
tras una consulta a Mercado Pago y una transacción corta. El email llega en segundos (intervalo del Outbox: 2 s).

**Constraints**: ninguna llamada de red dentro de una transacción (Principio VI); idempotencia ante avisos duplicados y
concurrentes (FR-017); un asiento nunca vendido dos veces (bloqueo de la función + índice único en `Entradas`); la
decisión se toma con datos consultados a Mercado Pago, nunca con el cuerpo del aviso (FR-014); secretos fuera del
repositorio (FR-028); controllers sin lógica; I/O async; "ahora" vía `IReloj`.

**Scale/Scope**: 1 endpoint de portador nuevo, 1 público (webhook), 2 de desarrollo; 1 endpoint con comportamiento nuevo
(GET carrito). 1 agregado extendido, 1 entidad nueva en el agregado (`Entrada`), 1 domain service, 3 eventos, 2 puertos
nuevos (`IProcesadorDePagos`, `IVerificadorDeAvisos`), 1 interfaz de dominio (`IGeneradorDeCodigos`), 1 proceso en
background con 2 manejadores, 1 adaptador de email nuevo. 1 migración.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Arquitectura Hexagonal | PASS **con enmienda pendiente**. `Carrito`, `Entrada`, `ConfirmadorDePagos`, `PagoAprobado`, `IGeneradorDeCodigos` y los eventos van en `Domain`. Casos de uso y puertos (`IProcesadorDePagos`, `IVerificadorDeAvisos`, `IConsultaCarritos` extendido, `IRegistroDePagos`) en `Application`. SDK de Mercado Pago, MailKit, EF y el `BackgroundService` en `Infrastructure`. El principio nombra a Stripe como pasarela: se enmienda a Mercado Pago (ver Complexity Tracking). |
| II. DDD Ligero | PASS. Inicio del pago, vencimiento extendido una sola vez, bloqueo de cambios en `Pagando`, emisión de entradas y la decisión acreditar/devolver viven en `Carrito` y `ConfirmadorDePagos`. `PagoAprobado` es value object. Nombres en español. |
| III. Bordes con DTOs | PASS. `CarritoDto` suma `PagoDto` y `EntradaDto`; `Api` define `PagoIniciadoResponse`, `EntradaResponse`, `PagoSimuladoRequest/Response`, con mapeo manual. El webhook no expone nada del dominio. |
| IV. Inversión de dependencias | PASS. Procesador de pagos, verificador de avisos, email, generador de códigos y repositorios como interfaces. `Singleton`: `ProcesadorDePagosSimulado` (estado en memoria compartido), verificador, `ConfirmadorDePagos`, generador. `Scoped`: handlers, repositorios, `ProcesadorDePagosMercadoPago` (usa `IOptions`, sin estado). El `BackgroundService` crea un scope por mensaje. |
| V. Testing por criterio | PASS. Ver "Estrategia de tests". La feature toca la ocupación de asientos: se extiende el **test de concurrencia obligatorio contra Postgres real**. |
| VI. Outbox + eventos | PASS. `PagoIniciado`, `CarritoPagado` y `DevolucionSolicitada` se registran en la misma transacción que el cambio. Crear la preferencia y consultar el pago van **antes** de abrir la transacción; el email y la devolución salen del Outbox. Se implementa el publicador que faltaba desde la 004. |
| VII. Auth explícita | PASS. Iniciar el pago: portador del carrito. Webhook: `[AllowAnonymous]` explícito y protegido por firma. Endpoints de simulación: `[AllowAnonymous]` explícito y mapeados solo en Development/Testing. Fallback cerrado sin cambios. |
| VIII. Calidad de código | PASS. Nullable, todo async (incluidos SDK y SMTP), controllers que solo traducen headers/query a comandos. |

**Re-evaluación post-diseño (Phase 1)**: sin cambios. El webhook resuelve la firma en un puerto de `Application`
implementado en `Infrastructure`, así que el controller no contiene lógica. La única desviación es el nombre de la
pasarela en la constitución, resuelta con la enmienda como primera tarea.

## Estrategia de tests (Principio V)

| Qué | Tests | Justificación |
|---|---|---|
| `Carrito`: estado efectivo con `Pagando`/`Pagado`, `RetieneAsientosEn`, `VencimientoAlIniciarPago` (+1 min y tope en el inicio), `IniciarPago` (sin email, no activo, ya en pago, selección modificada, éxito), rechazo `EnPago` de reemplazar/abandonar/email, `MarcarPagado` (una entrada por asiento, datos copiados) | Unit, Domain, objetos reales | Transiciones e invariantes nuevas; un error en el vencimiento o en la retención es silencioso (SC-006). |
| `ConfirmadorDePagos`: cada regla y su orden (ya pagado, no en pago, monto, moneda, aprobado tarde, vencido con asientos tomados, vencido con asientos libres, vigente) | Unit, Domain | Es la regla de dinero: un bug cobra sin entregar o entrega sin cobrar (SC-002, SC-005). |
| Generador de códigos: formato, alfabeto, longitud | Unit, Domain/Infra | Simple pero el formato es contrato con boletería. |
| `IniciarPagoCommandHandler`: Mercado Pago falla → carrito sin cambios; ya en pago → misma URL sin llamar a Mercado Pago; carrera con otro inicio; token ajeno → no encontrado; la preferencia lleva vencimiento, ítems y exclusiones correctos | Unit, Application | Modifica estado y ordena un efecto de red fuera de la transacción. |
| `ProcesarPagoCommandHandler`: pago inexistente, referencia desconocida, no aprobado, ya procesado, acreditar (eventos y registro), devolver (devolución y evento) | Unit, Application | Orquesta la idempotencia y los efectos. |
| `ObtenerCarritoQueryHandler`: en `Pagando` busca y procesa pagos; Mercado Pago falla o tarda → responde igual; en otros estados no llama | Unit, Application | Comportamiento nuevo con manejo de fallas (FR-017b). |
| `VerificadorDeAvisosMercadoPago`: firma válida (vector de prueba de la documentación), alterada, otra clave, sin `ts`, sin `v1`, `data.id` en mayúsculas, partes ausentes | Unit, Infrastructure | Seguridad: un bug regala entradas o bloquea todos los avisos (US5). |
| `ProcesadorDePagosMercadoPago`: mapeo de la solicitud a `PreferenceRequest` (exclusiones, `expires`, `binary_mode`, `external_reference`) y de `Payment` a `PagoConsultado` | Unit, Infrastructure, sin red | El mapeo es donde se rompe la integración en silencio; no se llama a Mercado Pago en CI. |
| API: iniciar pago (200, idempotente, 409 sin email, 409 no activo, 503), `carrito-en-pago` en reemplazar/abandonar/email, GET con `pago` y `entradas`, 401/404 como en la 007 | Integración, API | Contrato HTTP y mapeo a `ProblemDetails`. |
| Webhook de punta a punta con firma real calculada en el test: 401 sin firma o alterada; 200 y `Pagado` con pago aprobado; rechazado deja `Pagando`; tipo distinto ignorado; 500 si el procesador falla | Integración, API | Recorre firma + consulta + transacción reales (US5). |
| Pago tardío, aprobado a tiempo con aviso tardío (asientos libres y tomados), monto distinto, segundo pago de un carrito pagado → devoluciones registradas una vez | Integración, API + Testcontainers | FR-015/FR-019 contra la ocupación real. |
| Mapa: `Pagando` vigente y `Pagado` no figuran disponibles; `Pagando` vencido sí | Integración, API | FR-010, FR-023 de punta a punta. |
| Procesador del Outbox: entrega, reintento con espera tras falla, marca fallido al agotar intentos, tipos sin manejador marcados procesados, dos instancias no toman el mismo mensaje (`SKIP LOCKED`) | Integración, Testcontainers | Concurrencia y lease solo se comportan bien contra Postgres real. |
| Email de entrega: un solo email con todas las entradas; falla del envío no deshace la compra; aviso duplicado no reenvía | Integración, Testcontainers + `EnviadorDeEmailEnMemoria` | FR-025, FR-026, SC-008. |
| Devolución: llama a `Devolver` con la clave de idempotencia; reintento tras falla; `Completada` una sola vez | Integración, Testcontainers | FR-021. |
| **Concurrencia (obligatorio)**: 10 avisos simultáneos del mismo pago → una acreditación, N entradas, un evento de email; dos pagos aprobados distintos simultáneos del mismo carrito → uno acredita y el otro se devuelve; aviso aprobado (carrito vencido) concurrente con una creación que pide sus asientos → nunca hay entradas y un carrito activo con el mismo asiento; dos inicios de pago simultáneos → `VenceEn` extendido una sola vez | Integración, Testcontainers | Principio V: la ocupación de asientos cambia; las carreras solo aparecen contra el motor real. |
| Migración `AgregarPagoDeCarritos` sobre una base con carritos de la 007 | Integración, Testcontainers | Verifica que no descarta datos y que los índices únicos se crean. |
| **No se testea**: DTOs, requests/responses, registro en el DI, clases de opciones, `EnviadorDeEmailSmtp` (lo prueba el quickstart contra Mailpit), endpoint de simulación por separado (lo usan los demás tests), la integración real con Mercado Pago en CI (requiere credenciales y túnel; se valida con el quickstart) | — | Código trivial, cubierto de punta a punta o dependiente de un servicio externo que no debe correr en CI. |

## Project Structure

### Documentation (this feature)

```text
specs/008-pago-mercado-pago/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── pagos-api.md
├── checklists/requirements.md
└── tasks.md             # lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── CinemaSystemRandomPlay.Domain/
│   ├── Carritos/
│   │   ├── Carrito.cs                       # + IniciarPago, MarcarPagado, Entradas, estados nuevos
│   │   ├── EstadoCarrito.cs                 # + Pagando, Pagado
│   │   ├── Entrada.cs                       # nuevo
│   │   ├── ResultadoInicioPago.cs           # nuevo
│   │   ├── ResultadoAbandono.cs / ResultadoEmailEntrega.cs / AsignadorDeAsientos.cs  # + EnPago
│   │   ├── ICarritoRepository.cs            # ListarQueRetienenAsientos
│   │   └── Eventos/ (+ PagoIniciado, CarritoPagado)
│   └── Pagos/                               # nuevo
│       ├── ConfirmadorDePagos.cs            # + DecisionDePago, MotivoDevolucion
│       ├── PagoAprobado.cs
│       ├── IGeneradorDeCodigos.cs
│       └── Eventos/DevolucionSolicitada.cs
├── CinemaSystemRandomPlay.Application/
│   ├── Carritos/
│   │   ├── Commands/IniciarPagoCommand.cs   # nuevo
│   │   ├── Queries/ObtenerCarritoQuery.cs   # verifica pagos en Pagando
│   │   ├── Dtos/ (CarritoDto + PagoDto, EntradaDto)
│   │   └── ResultadoOperacionCarrito.cs     # + EmailEntregaFaltante, CarritoEnPago, CarritoModificado, PagoNoDisponible
│   ├── Pagos/                               # nuevo
│   │   ├── Commands/ (ProcesarPagoCommand, RecibirAvisoDePagoCommand)
│   │   ├── Manejadores/ (EnviarEmailDeEntrega, EjecutarDevolucion)
│   │   ├── MensajeDeEntrega.cs              # arma asunto y cuerpo del email
│   │   └── Ports/ (IProcesadorDePagos, IVerificadorDeAvisos, IRegistroDePagos, IManejadorDeMensaje)
│   └── Compartido/Ports/IEnviadorDeEmail.cs # sin cambios
├── CinemaSystemRandomPlay.Infrastructure/
│   ├── Pagos/                               # nuevo
│   │   ├── OpcionesMercadoPago.cs
│   │   ├── ProcesadorDePagosMercadoPago.cs
│   │   ├── ProcesadorDePagosSimulado.cs
│   │   ├── VerificadorDeAvisosMercadoPago.cs
│   │   └── GeneradorDeCodigosCrockford.cs
│   ├── Outbox/ProcesadorDeOutbox.cs         # nuevo (BackgroundService) + OpcionesOutbox
│   ├── Email/ (EnviadorDeEmailSmtp, OpcionesEmail)        # nuevo; los EnviadorDeEmail* de Invitados/ se mueven acá
│   ├── Carritos/CarritosRetienenAsientos.cs # filtro Pagando/Pagado
│   ├── Persistence/
│   │   ├── Carritos/ (CarritoEntityConfiguration + Entradas, CarritoRepository, ConsultaCarritos)
│   │   ├── Pagos/ (PagoProcesado, Devolucion, configuraciones, RegistroDePagos)
│   │   └── Outbox/ (OutboxMensaje + columnas de reintento)
│   └── Migrations/<ts>_AgregarPagoDeCarritos.cs
└── CinemaSystemRandomPlay.Api/
    ├── Modulos/Carritos/ (CarritosController: POST pago; Contracts: PagoResponse, EntradaResponse)
    ├── Modulos/Pagos/AvisosMercadoPagoController.cs       # nuevo, público
    ├── Modulos/Pagos/PagosSimuladosController.cs          # nuevo, solo Development/Testing
    ├── Program.cs, appsettings*.json, CinemaSystemRandomPlay.Api.http

tests/
├── CinemaSystemRandomPlay.Domain.UnitTests/ (Carritos/, Pagos/)
├── CinemaSystemRandomPlay.Application.UnitTests/ (Carritos/, Pagos/)
└── CinemaSystemRandomPlay.IntegrationTests/ (Pagos/, Outbox/, Carritos/ adaptados)

docker-compose.yml                           # + mailpit; api con Email__Proveedor=Smtp
```

**Structure Decision**: se conserva la estructura hexagonal de 4 capas y los proyectos de test. Se agrega el módulo
`Pagos` en cada capa. Las entradas viven dentro del agregado `Carrito`, porque nacen y se validan con él. Los
adaptadores de email salen de `Invitados/` (nombre heredado de la 006) a `Email/`.

## Documentación a actualizar

- `.specify/memory/constitution.md`: enmienda v4.1.0 (Mercado Pago en lugar de Stripe; procesador del Outbox existente).
- `README.md`: feature, flujo de compra, endpoints, modalidades de acceso, configuración de Mercado Pago y email,
  Mailpit, credenciales de prueba y "Deuda técnica conocida".
- `specs/007-carrito-anonimo/contracts/carritos-api.md`: nota de cabecera que apunta a la 008 para los estados nuevos.
- `CinemaSystemRandomPlay.Api.http`: iniciar pago, simular pago y aviso sin firma.
- `docs/openapi.json`: se regenera en el build.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| La constitución v4.0.1 nombra a Stripe como pasarela (Principio I, IV y Restricciones de Plataforma) | El usuario eligió Mercado Pago (Checkout Pro, credenciales de prueba, mercado argentino) | Mantener Stripe contradice la spec. Se enmienda la constitución (v4.1.0, MINOR) como primera tarea, antes de tocar código. |

Simplificaciones conscientes, que se documentan en el PR:
- La entrega del email es at-least-once: si el proceso cae entre que el servidor SMTP acepta el email y se marca
  `EntregaEnviadaEn`, se puede reenviar una vez. Las devoluciones no tienen este hueco gracias a la clave de idempotencia
  de Mercado Pago.
- No se verifica la antigüedad del `ts` del aviso: un reenvío solo dispara otra consulta idempotente.
- No hay barrido periódico de carritos en `Pagando`: si el aviso no llega y nadie consulta el carrito, el pago se procesa
  cuando llegue un aviso o una consulta posterior (clarificación 2).
- No se puede distinguir por formato una credencial de prueba de una de producción: el resguardo es la configuración por
  secretos y la documentación.
- El procesador del Outbox es por instancia, con lease para tolerar varias; los eventos sin manejador (004/007) se marcan
  procesados sin acción hasta la feature de SignalR.
