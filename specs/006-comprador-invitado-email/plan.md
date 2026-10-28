# Implementation Plan: Comprador Invitado con Email Verificado

> **Nota (feature 007):** reemplazada por el [carrito anónimo con retención corta](../007-carrito-anonimo/spec.md). La verificación por email, sus endpoints y el tope por email ya no existen. Este documento es el registro histórico de lo que se mergeó en la feature 006.

**Branch**: `006-comprador-invitado-email` | **Date**: 2026-10-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-comprador-invitado-email/spec.md`

## Summary

El comprador deja de ser un usuario autenticado con rol `Customer` y pasa a ser un **invitado identificado
por un email verificado**. El invitado pide un código de un solo uso para su email, lo ingresa y recibe una
credencial con vencimiento. Esa credencial es lo único que permite reservar, listar, consultar y cancelar, y
solo sobre las reservas de su email. El comportamiento de negocio de la feature 004 no cambia.

Enfoque técnico:
- **Credencial**: JWT HS256 emitido por la propia API y validado por un segundo esquema JwtBearer
  (`Invitado`), separado del esquema OIDC del personal. Sin tabla de credenciales.
- **Verificación**: agregado `VerificacionEmail` (código de 6 dígitos hasheado, vencimiento, máximo de
  intentos, una sola vigente por email por índice único parcial). Las operaciones se serializan por email con
  un bloqueo asesor de Postgres.
- **Identidad tipada**: Value Object `EmailComprador` (normaliza y valida). `Reserva.ClienteId` pasa a
  `Reserva.Comprador`; el tope de 10 asientos se cuenta por email normalizado.
- **Límites de abuso**: por email en base de datos (5/hora) y por origen con el rate limiter de ASP.NET Core
  (20/hora).
- **Email**: puerto `IEnviadorDeEmail`, con una implementación que registra el mensaje en Development y
  Testing. El envío ocurre después de confirmar la transacción.
- **Modalidades de acceso explícitas**: políticas `InvitadoVerificado` y `Personal`, `[AllowAnonymous]` en lo
  público y fallback cerrado. Desaparece la política `Customer`.
- **Migración**: descarta explícitamente las reservas de desarrollo (FR-021), con una guarda que impide
  hacerlo sobre una base con datos sin una decisión explícita.

Detalle de cada decisión en [research.md](./research.md).

## Technical Context

**Language/Version**: C# 12 / .NET 10 (fijo por la constitución)

**Primary Dependencies**: ASP.NET Core (Controllers, `Microsoft.AspNetCore.RateLimiting` incluido en el
framework), `Microsoft.EntityFrameworkCore` 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10 (ya en uso),
`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12 (ya en la Api). **Nueva** en `Infrastructure`:
`Microsoft.IdentityModel.JsonWebTokens` para emitir la credencial (ya llega como dependencia transitiva de
JwtBearer; se declara explícita al usarse fuera de la Api).

**Storage**: PostgreSQL, misma `CinemaDbContext`. Tabla nueva `VerificacionesEmail`; columna
`Reservas.ClienteId` pasa a `CompradorEmail`.

**Testing**: xUnit + NSubstitute para Domain/Application; `Testcontainers.PostgreSql` +
`Microsoft.AspNetCore.Mvc.Testing` para integración (ya en uso). Las credenciales de test se firman con la
`ClaveFirma` de test; el envío de email se reemplaza por un doble en memoria que expone el código.

**Target Platform**: Linux server (API backend, sin frontend propio)

**Project Type**: web-service (backend puro, REST/JSON)

**Performance Goals**: validar una credencial es una verificación de firma sin acceso a base de datos. Pedir
y confirmar un código son transacciones cortas (un bloqueo, un conteo o una lectura, una escritura).

**Constraints**: el código se usa una sola vez y vence (FR-004); máximo de intentos (FR-005); respuesta
idéntica exista o no el email (FR-003); la identidad sale solo de la credencial (FR-009); ninguna reserva
ajena es accesible conociendo su id o su email (FR-015); modalidad de acceso declarada en todo endpoint
(FR-017); controllers sin lógica; I/O async; "ahora" vía `IReloj`; ningún efecto externo dentro de una
transacción.

**Scale/Scope**: 2 endpoints nuevos (públicos), 4 endpoints de reservas que cambian de política, 1 agregado
nuevo, 1 Value Object nuevo, 3 puertos nuevos y 1 migración.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Arquitectura Hexagonal | PASS. `EmailComprador`, `VerificacionEmail` e `IVerificacionEmailRepository` van en `Domain`. Casos de uso y los puertos `IEmisorDeCredencial`, `IEnviadorDeEmail` y `ICodigosDeVerificacion` en `Application`. JWT, HMAC, el bloqueo asesor y el registrador de email en `Infrastructure`. JwtBearer y el rate limiter solo en `Api`. `Domain` y `Application` no conocen `ClaimsPrincipal`, `ILogger` ni `IOptions`. |
| II. DDD Ligero | PASS. "Un código se usa una vez", "vence" y "se invalida tras N intentos" viven en `VerificacionEmail`; la normalización y validación del email en `EmailComprador`; el tope por comprador sigue en el domain service `ReservadorDeAsientos`. Ninguna se puede construir inválida; los resultados son explícitos. Nombres en español. |
| III. Bordes con DTOs | PASS. Los casos de uso reciben y devuelven records de `Application`; `Api` define `SolicitarVerificacionRequest`, `ConfirmarVerificacionRequest` y `CredencialResponse` con mapeo manual. `ReservaResponse` no cambia. |
| IV. Inversión de dependencias | PASS. Todo servicio externo (email, firma de credenciales, hashing del código, repositorio) es una interfaz. Registro con el DI nativo: `Scoped` para lo que comparte `CinemaDbContext`; `Singleton` para las opciones y el generador de códigos, que no tienen estado. |
| V. Testing por criterio | PASS. Ver "Estrategia de tests". Mantiene y adapta el **test de integración de concurrencia contra Postgres real**. |
| VI. Outbox + eventos | PASS. `ReservaCreada` y `ReservaCancelada` siguen escribiéndose en el outbox dentro de la misma transacción (la desviación de la feature 004 sobre el publicador no cambia). El email **no** pasa por el outbox, porque guardaría el código en claro; se envía **después** de confirmar la transacción, nunca dentro de ella. Ver research.md, Decisión 7. |
| VII. Auth explícita | PASS. Es la feature que implementa el Principio VII v3.0.0: invitado con email verificado, credencial verificable, el rol `Customer` eliminado, OIDC reservado al personal, modalidad declarada por endpoint y fallback cerrado. |
| VIII. Calidad de código | PASS. Nullable habilitado, todo async, los controllers solo traducen HTTP a casos de uso y resultados a `ProblemDetails`. |

**Re-evaluación post-diseño (Phase 1)**: sin cambios. [data-model.md](./data-model.md) mantiene las reglas
en `Domain`. Se evitó poner el límite por origen en el dominio: es una defensa de borde, y se documentan sus
limitaciones (por proceso, y dependiente de `ForwardedHeaders` detrás de un proxy).
Ninguna desviación nueva respecto de la constitución.

## Estrategia de tests (Principio V)

| Qué | Tests | Justificación |
|---|---|---|
| `EmailComprador` (normalización, validación, igualdad) | Unit, Domain | Regla no trivial que sostiene el tope y la propiedad; un bug sería silencioso (SC-006). |
| `VerificacionEmail` (estados, intentos, vencimiento, uso único) | Unit, Domain, objetos reales | Transiciones de estado críticas para la seguridad (SC-004). |
| `ReservadorDeAsientos` y `Reserva` con `EmailComprador` | Unit, Domain (se adaptan los de la 004) | Cambia la identidad contra la que se cuenta el tope. |
| `SolicitarVerificacionEmail` | Unit, Application | Modifica estado: invalida la vigente, aplica el límite por email, respuesta idéntica, envío después de confirmar. |
| `ConfirmarVerificacionEmail` | Unit, Application | Modifica estado: cada resultado (correcto, incorrecto, vencido, usado, invalidado) y que solo el correcto emite credencial. |
| Casos de uso de reservas con email (reservar, cancelar, listar, obtener) | Unit, Application (se adaptan) | Cambia la identidad pasada a casos de uso que modifican estado. |
| Índice único de una vigente, bloqueo por email y conteo de ventana | Integración, Testcontainers | Unicidad y concurrencia: exigen Postgres real, no InMemory. |
| Credencial: sin ella, vencida, alterada, firmada por otra clave, token del personal; `401` en reservas; endpoints públicos siguen públicos | Integración, API | Un bug aquí expone reservas ajenas; solo se prueba bien con el pipeline real. |
| Aislamiento entre emails (listar, consultar y cancelar lo ajeno da `404`) | Integración, API | SC-003: debe ser indistinguible de "no existe". |
| Flujo completo: pedir código, leerlo del doble, confirmar, reservar | Integración, API | Prueba que emisor y validador comparten configuración. |
| Límite por origen (`429`) | Integración, API | Configuración del middleware: un error lo desactiva sin que nadie lo note. |
| Migración que descarta reservas y su guarda | Integración, Testcontainers | Es destructiva: un bug perdería datos o dejaría reservas sin dueño. |
| Concurrencia de reservas sobre el mismo asiento | Integración, Testcontainers (se adapta) | Obligatorio sin excepción. |
| **No se testea**: DTOs y requests/responses, registro en el DI, clases de opciones, `EnviadorDeEmailRegistrador` y `EnviadorDeEmailNoConfigurado`, `EmisorDeCredencialJwt` por separado, configuración y README | — | Código trivial o cubierto por los tests de integración de arriba. |

## Project Structure

### Documentation (this feature)

```text
specs/006-comprador-invitado-email/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── verificacion-email-api.md
│   └── reservas-api.md
└── tasks.md             # lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── CinemaSystemRandomPlay.Domain/
│   ├── Compradores/
│   │   └── EmailComprador.cs                       # nuevo
│   ├── Verificaciones/                             # nuevo
│   │   ├── VerificacionEmail.cs
│   │   ├── EstadoVerificacion.cs
│   │   ├── ResultadoVerificacion.cs
│   │   └── IVerificacionEmailRepository.cs
│   └── Reservas/
│       ├── Reserva.cs                              # ClienteId -> Comprador
│       ├── ReservadorDeAsientos.cs                 # tope por Comprador
│       ├── ReglasReserva.cs                        # MaximoAsientosPorComprador
│       └── Eventos/ReservaCreada.cs                # Comprador
├── CinemaSystemRandomPlay.Application/
│   ├── Verificaciones/                             # nuevo
│   │   ├── Commands/SolicitarVerificacionEmailCommand.cs
│   │   ├── Commands/ConfirmarVerificacionEmailCommand.cs
│   │   └── Ports/ (IEmisorDeCredencial, IEnviadorDeEmail, ICodigosDeVerificacion)
│   └── Reservas/                                   # comandos y consultas reciben EmailComprador
├── CinemaSystemRandomPlay.Infrastructure/
│   ├── Invitados/                                  # nuevo
│   │   ├── OpcionesInvitados.cs
│   │   ├── EmisorDeCredencialJwt.cs
│   │   ├── CodigosDeVerificacionHmac.cs
│   │   ├── EnviadorDeEmailRegistrador.cs
│   │   └── EnviadorDeEmailNoConfigurado.cs
│   ├── Persistence/
│   │   ├── Verificaciones/ (VerificacionEmailEntityConfiguration, VerificacionEmailRepository)
│   │   ├── Reservas/ (ReservaEntityConfiguration, ConsultaReservas: por email)
│   │   └── DevDataSeeder.cs
│   └── Migrations/ (AddCompradorEmailEnReservas, AddVerificacionesEmail)
└── CinemaSystemRandomPlay.Api/
    ├── Autenticacion/ (Politicas, EsquemasDeAutenticacion, ClaimsPrincipalExtensions: email)
    ├── Modulos/Verificaciones/
    │   ├── VerificacionesEmailController.cs        # nuevo, público
    │   └── Contracts/
    ├── Modulos/Reservas/ReservasController.cs      # política InvitadoVerificado
    ├── Program.cs
    └── appsettings*.json

tests/
├── CinemaSystemRandomPlay.Domain.UnitTests/ (Compradores, Verificaciones, Reservas)
├── CinemaSystemRandomPlay.Application.UnitTests/ (Verificaciones, Reservas)
└── CinemaSystemRandomPlay.IntegrationTests/ (Invitados, Reservas, TokensDePrueba, factories)
```

**Structure Decision**: se conserva la estructura hexagonal existente de 4 capas y los proyectos de test
actuales. Las reglas de la verificación forman un módulo propio (`Verificaciones`) y no se mezclan con
`Reservas`, porque tienen su propio agregado y ciclo de vida. `Compradores` aloja solo el Value Object
compartido por las dos.

## Complexity Tracking

Sin violaciones de la constitución que justificar.

Se agrega un **segundo esquema de autenticación** (`Invitado`) junto al del personal. No es una violación:
es la forma de separar las dos poblaciones que exige el Principio VII, y reemplaza (no se suma a) el
mecanismo de la feature 004. Se descartó reusar un solo esquema con dos emisores porque un error de
configuración dejaría a un invitado pasar por personal ([research.md](./research.md), Decisión 1).
