# Implementation Plan: Carrito Anónimo con Retención Corta

**Branch**: `007-carrito-anonimo` | **Date**: 2026-10-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-carrito-anonimo/spec.md`

## Summary

El comprador deja de identificarse con un email verificado (feature 006) y pasa a ser **anónimo**. Al elegir
asientos obtiene un **carrito** que los retiene 5 minutos y un **token** que es su única credencial. Con el
token puede ver el carrito, reemplazar la selección de forma atómica sin extender el vencimiento, abandonarlo
o indicar el email de entrega, que no se verifica. El acaparamiento se acota con un tope por carrito, un tope
global de asientos retenidos por función y un límite de creación por origen, más un punto de extensión para
CAPTCHA. Se retira toda la verificación por email de la 006.

Enfoque técnico:
- **Agregado `Carrito`**: reemplaza a `Reserva` en todas las capas, con vencimiento fijo, reemplazo por
  diferencia, abandono y email de entrega. El domain service `AsignadorDeAsientos` aplica disponibilidad y
  tope global.
- **Concurrencia**: todo cambio de ocupación toma el bloqueo pesimista de la fila de la función, el mismo de
  la 004, antes de leer carritos. Así el reemplazo es atómico y dos carritos nunca ganan el mismo asiento.
- **Token**: JWT HS256 del esquema `Invitado` de la 006, con el id del carrito como claim. Política
  `PortadorDeCarrito`. 401 sin token válido y 404 con un token ajeno.
- **Defensas**: tope global en el dominio, límite por origen con el rate limiter de la 006 sobre la creación
  y puerto `IDesafioAntiBots` aprobado por defecto.
- **Migración**: una sola, que descarta las reservas con la guarda explícita de la 006, elimina
  `Reservas`, `ReservaAsientos` y `VerificacionesEmail` y crea `Carritos` y `CarritoAsientos`.

Detalle de cada decisión en [research.md](./research.md).

## Technical Context

**Language/Version**: C# 12 / .NET 10 (fijo por la constitución)

**Primary Dependencies**: ASP.NET Core (Controllers, `Microsoft.AspNetCore.RateLimiting`),
`Microsoft.AspNetCore.Authentication.JwtBearer` 10, `Microsoft.IdentityModel.JsonWebTokens` (ya en
`Infrastructure`), EF Core 10 + Npgsql 10. Sin dependencias nuevas.

**Storage**: PostgreSQL, misma `CinemaDbContext`. Tablas nuevas `Carritos` y `CarritoAsientos`. Se eliminan
`Reservas`, `ReservaAsientos` y `VerificacionesEmail`.

**Testing**: xUnit + NSubstitute para Domain y Application; Testcontainers.PostgreSql +
`Microsoft.AspNetCore.Mvc.Testing` para integración (ya en uso).

**Target Platform**: Linux server (API backend, sin frontend propio)

**Project Type**: web-service (backend puro, REST/JSON)

**Performance Goals**: cada operación de carrito es una transacción corta: un bloqueo de fila, una lectura de
los carritos activos de la función por índice y una escritura. Validar el token es una verificación de firma,
sin acceso a la base.

**Constraints**: reemplazo atómico y todo o nada (FR-008, FR-009); vencimiento fijo (FR-006); un asiento en a
lo sumo un carrito activo, también con concurrencia (FR-012); tope global exacto con concurrencia (FR-022);
404 indistinguible con un token ajeno (FR-019); ningún efecto externo dentro de una transacción (el desafío
anti-bots va antes de abrirla); controllers sin lógica; I/O async; "ahora" vía `IReloj`.

**Scale/Scope**: 5 endpoints nuevos y 6 eliminados (4 de reservas y 2 de verificación). 1 agregado renombrado
y rediseñado, 1 domain service, 3 eventos, 3 puertos nuevos o renombrados (`IEmisorDeTokenDeCarrito`,
`IDesafioAntiBots`, `IConsultaCarritos`) y 1 movido (`IEnviadorDeEmail`). 1 migración.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Arquitectura Hexagonal | PASS. `Carrito`, `ReglasCarrito`, `AsignadorDeAsientos`, `ICarritoRepository` y los eventos van en `Domain`. Los casos de uso y los puertos `IEmisorDeTokenDeCarrito`, `IDesafioAntiBots`, `IConsultaCarritos` e `IEnviadorDeEmail` van en `Application`. JWT, EF y las implementaciones de los puertos van en `Infrastructure`. JwtBearer y el rate limiter quedan en `Api`. |
| II. DDD Ligero | PASS. Vencimiento fijo, reemplazo por diferencia, transiciones y "solo activo" viven en `Carrito`. Disponibilidad entre carritos y tope global, en `AsignadorDeAsientos`. `ReglasCarrito` no se puede construir inválida. Nombres en español. |
| III. Bordes con DTOs | PASS. Los casos de uso devuelven `CarritoDto`, y `Api` define `CrearCarritoRequest`, `ReemplazarAsientosRequest`, `EmailEntregaRequest`, `CarritoResponse` y `CarritoCreadoResponse`, con mapeo manual. |
| IV. Inversión de dependencias | PASS. Token, desafío, consultas, email y repositorio se consumen como interfaces. `Scoped` para lo que usa `CinemaDbContext`; `Singleton` para reglas, emisor de token y desafío desactivado (sin estado). |
| V. Testing por criterio | PASS. Ver "Estrategia de tests". El **test de integración de concurrencia contra Postgres real** se mantiene y suma los casos de reemplazo y de dos carritos que disputan un asiento. |
| VI. Outbox + eventos | PASS. `CarritoCreado`, `SeleccionReemplazada` y `CarritoAbandonado` se registran en la misma transacción. El desafío anti-bots (posible llamada de red) se evalúa **antes** de abrirla. El email ya no viaja en el outbox. La desviación de la 004 (publicador del outbox pendiente) no cambia. |
| VII. Auth explícita (v4.0.0) | PASS. Implementa el principio: comprador anónimo, token no adivinable por carrito, 404 con token ajeno, email solo de entrega, tope por carrito, por origen y por función, punto de extensión CAPTCHA, modalidades públicas, de portador y de personal declaradas, y fallback cerrado. |
| VIII. Calidad de código | PASS. Nullable habilitado, todo async y controllers que solo traducen. |

**Re-evaluación post-diseño (Phase 1)**: sin cambios. El límite por origen queda en el borde por ser una
defensa de infraestructura y no una regla de negocio, con sus limitaciones documentadas (por proceso y
dependiente de `ForwardedHeaders`). Ninguna desviación nueva.

## Estrategia de tests (Principio V)

| Qué | Tests | Justificación |
|---|---|---|
| `Carrito`: vencimiento fijo, estado efectivo, reemplazo por diferencia (comunes intactos, `VenceEn` igual), abandono, email solo en activo | Unit, Domain, objetos reales | Invariantes y transiciones; un bug en el vencimiento o el reemplazo sería silencioso (SC-002, SC-003). |
| `ReglasCarrito` (validación y cálculo del tope con redondeo y mínimo 1) | Unit, Domain | Cálculo con bordes (impares, salas chicas). |
| `AsignadorDeAsientos` (orden de validación, propios disponibles, tope excluyendo al propio carrito, no activo) | Unit, Domain (se adaptan los de `ReservadorDeAsientos`) | Regla que cruza carritos; es el corazón de FR-010 y FR-022. |
| `CrearCarrito` (desafío antes de la transacción, rechazo sin efectos, evento, token emitido) | Unit, Application | Modifica estado y ordena efectos (Principio VI). |
| `ReemplazarSeleccion`, `AbandonarCarrito` e `IndicarEmailEntrega` (token ajeno → no encontrado sin tocar el repositorio, no activo, eventos) | Unit, Application | Modifican estado; la comparación de ids es seguridad. |
| `EmailComprador` | Unit, Domain (existente) | Se conserva sin cambios. |
| API: crear sin credencial, consultar, reemplazar, abandonar y email; códigos de error del contrato; `Location` | Integración, API | Contrato HTTP; mapeo de resultados a `ProblemDetails`. |
| Token: sin él, vencido, alterado, otra clave, tipo incorrecto, token del personal → `401`; token de otro carrito → `404` idéntico a inexistente | Integración, API | Un bug aquí expone carritos ajenos (SC-005). |
| Mapa refleja carritos activos, reemplazos, abandonos y vencimientos | Integración, API (se adapta `MapaConReservasTests`) | FR-014 de punta a punta. |
| Tope global por función (rechazo, liberación al abandonar o vencer, reemplazo en el tope) | Integración, API | Requiere la suma real sobre la base. |
| Límite por origen (`429` + `Retry-After`, solo en creación) | Integración, API | Configuración del middleware: un error lo desactiva sin que se note. |
| **Concurrencia (obligatorio)**: 20 creaciones simultáneas sobre un asiento → 1 gana; creaciones solapadas sin doble retención; **dos carritos que reemplazan en paralelo hacia el mismo asiento libre** → uno gana y el otro conserva su selección; **reemplazo concurrente con una creación que pide un asiento viejo** → nunca dos carritos con el mismo asiento y el carrito queda con la selección vieja o la nueva completa; dos reemplazos paralelos del mismo carrito → sin asientos de ambos; creaciones simultáneas cerca del tope → nunca se supera | Integración, Testcontainers | Principio V: obligatorio sin excepción. Las carreras solo aparecen contra el motor real. |
| Repositorio: persistencia del reemplazo por diferencia y del email | Integración, Testcontainers | Mapeo de la colección propia; EF puede fallar con claves duplicadas en seguimiento. |
| Migración `ReemplazarReservasPorCarritos` (guarda sin variable, descarte con variable, tablas nuevas, verificaciones eliminadas) | Integración, Testcontainers | Es destructiva: un bug perdería datos en silencio. |
| Endpoints retirados (`/api/reservas`, `/api/verificaciones-email`) → `404` | Integración, API (en los tests de modalidades) | Evita que quede expuesto algo del modelo viejo. |
| **No se testea**: DTOs y requests/responses, registro en el DI, clases de opciones, `DesafioAntiBotsDesactivado` (devuelve `true`), `EnviadorDeEmail*` (sin uso en esta feature), `EmisorDeTokenDeCarritoJwt` por separado (lo cubren los tests de API), configuración, README y specs | — | Código trivial o cubierto de punta a punta. |

Se eliminan los tests de la 006 que prueban comportamiento retirado: verificación de email (Domain,
Application e Integración), límite por email, tope por comprador, listar mis reservas y
`ReservasInvitadoTests`. `MigracionCompradorEmailTests` se conserva porque prueba una migración histórica que
sigue en la cadena.

## Project Structure

### Documentation (this feature)

```text
specs/007-carrito-anonimo/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── carritos-api.md
├── checklists/requirements.md
└── tasks.md             # lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── CinemaSystemRandomPlay.Domain/
│   ├── Carritos/                                   # reemplaza a Reservas/
│   │   ├── Carrito.cs
│   │   ├── EstadoCarrito.cs
│   │   ├── ReglasCarrito.cs
│   │   ├── AsignadorDeAsientos.cs                  # + ResultadoSeleccion
│   │   ├── ResultadoAbandono.cs / ResultadoEmailEntrega.cs
│   │   ├── ICarritoRepository.cs
│   │   └── Eventos/ (CarritoCreado, SeleccionReemplazada, CarritoAbandonado)
│   ├── Compradores/EmailComprador.cs               # se conserva
│   └── Verificaciones/                             # se elimina
├── CinemaSystemRandomPlay.Application/
│   ├── Carritos/                                   # reemplaza a Reservas/
│   │   ├── Commands/ (CrearCarrito, ReemplazarSeleccion, AbandonarCarrito, IndicarEmailEntrega)
│   │   ├── Queries/ObtenerCarritoQuery.cs
│   │   ├── Dtos/ (CarritoDto, AsientoPedidoDto, FuncionCarritoDto)
│   │   └── Ports/ (IConsultaCarritos, IEmisorDeTokenDeCarrito, IDesafioAntiBots)
│   ├── Compartido/Ports/IEnviadorDeEmail.cs        # movido y genérico
│   └── Verificaciones/                             # se elimina
├── CinemaSystemRandomPlay.Infrastructure/
│   ├── Invitados/ (OpcionesInvitados, EmisorDeTokenDeCarritoJwt, DesafioAntiBotsDesactivado, EnviadorDeEmail*)
│   ├── Carritos/CarritosRetienenAsientos.cs        # reemplaza a ReservasRetienenAsientos
│   ├── Persistence/Carritos/ (CarritoEntityConfiguration, CarritoRepository, ConsultaCarritos)
│   ├── Persistence/Verificaciones/                 # se elimina
│   └── Migrations/<ts>_ReemplazarReservasPorCarritos.cs
└── CinemaSystemRandomPlay.Api/
    ├── Autenticacion/ (Politicas: PortadorDeCarrito, ClaimsPrincipalExtensions: ObtenerCarritoId)
    ├── Modulos/Carritos/ (CarritosController, Contracts/)
    ├── Modulos/Reservas/ y Modulos/Verificaciones/ # se eliminan
    ├── Program.cs, appsettings.json, CinemaSystemRandomPlay.Api.http

tests/
├── CinemaSystemRandomPlay.Domain.UnitTests/Carritos/
├── CinemaSystemRandomPlay.Application.UnitTests/Carritos/
└── CinemaSystemRandomPlay.IntegrationTests/ (Carritos/, Compartido/TokensDePrueba)
```

**Structure Decision**: se conserva la estructura hexagonal de 4 capas y los proyectos de test. El módulo
`Reservas` se renombra a `Carritos` en cada capa y el módulo `Verificaciones` desaparece. `Invitados` (en
`Infrastructure`) conserva la configuración del esquema de token, porque la clave sigue siendo
`Invitados:ClaveFirma`.

## Documentación a actualizar

- `README.md`: features, flujo de uso, tabla de endpoints, modalidades de acceso, configuración, migración y
  "Deuda técnica conocida" (sin verificación por email; límite por origen por proceso; carritos viejos sin
  purga).
- `specs/004-reserva-asientos-funcion/` (spec, plan y contracts): la nota de cabecera pasa a apuntar a la 007
  (carrito anónimo con token) en lugar de la verificación por email.
- `specs/006-comprador-invitado-email/` (spec, plan y contracts): nota de cabecera "reemplazada por la
  feature 007". No se reescribe: es el registro histórico de lo que se mergeó.
- `CinemaSystemRandomPlay.Api.http`: ejemplos del carrito.

## Complexity Tracking

Sin violaciones de la constitución que justificar.

Simplificaciones conscientes, que se documentan en el PR:
- El límite por origen es por proceso (heredado de la 006).
- Los carritos expirados y abandonados no se purgan.
- No hay recuperación de un token perdido: el carrito vence solo.
- `IEnviadorDeEmail` queda registrado sin uso hasta la feature de pago.
