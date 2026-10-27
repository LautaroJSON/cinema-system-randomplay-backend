# Implementation Plan: Precios de las Funciones

**Branch**: `005-precios-funciones` | **Date**: 2026-10-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-precios-funciones/spec.md`

## Summary

Cada función pasa a tener un precio por entrada en ARS, que se informa en los horarios de hoy y en el
mapa de asientos. Al crearse, una reserva congela ese precio como precio unitario y deriva su total
(precio × asientos). Crear, listar y consultar reservas informan ambos montos. Todos los cambios de
contrato son aditivos y no hay endpoints nuevos.

Enfoque técnico:
- Value Object `Dinero` (`decimal` + enum `Moneda`) en `Domain/Compartido`: inmutable, sin negativos ni
  más de 2 decimales, con `Multiplicar`.
- `Funcion.PrecioEntrada` obligatorio y mayor que cero. `Reserva.PrecioUnitario` se persiste y
  `Reserva.Total` se calcula.
- El precio que congela la reserva es el de la fila de la función bloqueada con `FOR UPDATE` (feature
  004), así que un cambio de precio concurrente no puede producir una reserva incoherente.
- EF Core `ComplexProperty` con `numeric(12,2)`. La migración `AddPrecios` asigna un precio a las
  funciones existentes y copia el precio de la función a las reservas existentes.
- El monto se expone como texto con 2 decimales (`"5500.00"`) mediante un mapeo explícito en la Api.

Detalle de cada decisión en [research.md](./research.md).

## Technical Context

**Language/Version**: C# 12 / .NET 10 (fijo por la constitución)

**Primary Dependencies**: ASP.NET Core (Controllers), `Microsoft.EntityFrameworkCore` 10.0.12 +
`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 (ya en uso). Sin dependencias nuevas.

**Storage**: PostgreSQL, misma `CinemaDbContext`. Columnas nuevas en `Funciones` y `Reservas`, sin
tablas nuevas.

**Testing**: xUnit + NSubstitute para Domain/Application; `Testcontainers.PostgreSql` +
`Microsoft.AspNetCore.Mvc.Testing` para integración (ya en uso).

**Target Platform**: Linux server (API backend, sin frontend propio)

**Project Type**: web-service (backend puro, REST/JSON)

**Performance Goals**: sin metas nuevas. El precio viaja en la misma fila de la función que ya se lee,
sin consultas adicionales (SC-002).

**Constraints**: nunca punto flotante, en el código ni en el JSON (FR-002, FR-013); cambios de contrato
solo aditivos (FR-007, FR-010); precio congelado en la reserva (FR-009); el test de concurrencia de
reservas sigue pasando (constitución, puerta de CI 3).

**Scale/Scope**: 1 Value Object y 1 enum nuevos, 2 agregados modificados, 5 respuestas ampliadas en 3
endpoints de lectura y 1 de escritura, 1 migración.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Arquitectura Hexagonal | PASS. `Dinero` y `Moneda` en `Domain/Compartido`, solo BCL. `DineroDto` en Application. El mapeo EF (`ComplexProperty`, `numeric(12,2)`) y la migración en Infrastructure. El formato de texto del monto solo en Api. |
| II. DDD Ligero | PASS. `Dinero` es el Value Object que la constitución nombra: inmutable, valida sus argumentos y usa `decimal`. "Precio > 0 y en ARS" vive en `Funcion`. "El precio se congela" vive en `Reserva` (lo recibe en `Crear` y no hay mutador). El total es una propiedad del agregado. Nombres en español. |
| III. Bordes con DTOs | PASS. `DineroDto` en Application y `DineroResponse` en Api, con mapeo explícito en `DesdeDto`. No se usa un `JsonConverter` global (research.md, Decisión 7). |
| IV. Inversión de dependencias | PASS. Sin puertos ni registros nuevos. `IConsultaReservas` amplía su DTO de lectura. |
| V. Testing por criterio | PASS. Ver "Estrategia de tests". La feature toca la reserva de asientos: el test de concurrencia existente debe seguir pasando. |
| VI. Outbox + eventos | PASS. `ReservaCreada` suma precio y total al payload, que se sigue escribiendo en la misma transacción. Mantiene la desviación ya documentada en la feature 004 (sin publicador). |
| VII. Auth explícita | PASS. Sin endpoints nuevos ni cambios de política. Los públicos siguen `[AllowAnonymous]`. |
| VIII. Calidad de código | PASS. Sin lógica nueva en controllers (solo `DesdeDto`). Nullable habilitado; I/O sin cambios. |

**Re-evaluación post-diseño (Phase 1)**: sin cambios. [data-model.md](./data-model.md) deja todas las
reglas de montos en Domain. [contracts/precios-api.md](./contracts/precios-api.md) solo agrega campos.
No hay desviaciones nuevas.

## Estrategia de tests (Principio V)

| Qué | ¿Se testea? | Tipo | Justificación |
|---|---|---|---|
| `Dinero` (negativo, más de 2 decimales, cero válido, normalización de escala, igualdad por valor, `Multiplicar` exacto y con cantidad negativa) | Sí | Unit de Domain | Invariantes y cálculo de un Value Object de dinero. Un error de centavos es silencioso y caro. |
| `Funcion` con precio (null, cero, válido) | Sí | Unit de Domain | Regla de negocio nueva (FR-003). Barato. |
| `Reserva` / `ReservadorDeAsientos` congelan el precio de la función y calculan el total | Sí | Unit de Domain con objetos reales | Cálculo y regla central de la feature (FR-008). |
| Persistencia de montos (`7250.50` vuelve exacto desde Postgres; precio congelado tras un `UPDATE` del precio de la función) | Sí | Integración, Postgres real, vía HTTP | Mapeo `numeric(12,2)` y FR-009. El provider InMemory no reflejaría la precisión del motor. |
| Contratos: `precioEntrada` en horarios y mapa, `precioUnitario` y `total` en crear, listar y consultar, con `monto` como string de 2 decimales | Sí | Integración vía HTTP (casos nuevos en los tests existentes) | Es el contrato público y la decisión de formato de la clarificación (FR-013). Un número en lugar de string rompería al cliente sin error visible. |
| Campos existentes sin cambios | Sí, implícito | Los tests de integración existentes de 002 a 004 siguen pasando sin modificar sus aserciones | Verifica que el cambio es aditivo (FR-007, FR-010). |
| Test de concurrencia de reservas | Sí (existente) | Integración, Postgres real | Obligatorio por constitución: la feature toca la reserva. Solo se ajusta la construcción de la función. |
| Handlers de Application (`ListarHorariosDisponiblesHoy`, `ObtenerMapaAsientos`, `ReservarAsientos`, `ListarMisReservas`) | No (nuevos casos) | — | Solo copian el precio al DTO, un mapeo 1 a 1 ya cubierto por la integración. Los tests existentes se ajustan al nuevo constructor de `Funcion`. |
| `DineroDto`, `DineroResponse`, `DesdeDto`, configuración de EF | No | — | Mapeos triviales, cubiertos por los tests de contrato. |
| Backfill de la migración (`UPDATE ... FROM`) | No (verificación manual) | — | Solo afecta a la base de desarrollo, se ejecuta una vez y las bases de test nacen vacías. Se verifica con la consulta de [quickstart.md](./quickstart.md). |
| `DevDataSeeder` con precios | No | — | Tooling de desarrollo, sin lógica. |

## Project Structure

### Documentation (this feature)

```text
specs/005-precios-funciones/
├── plan.md               # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
│   └── precios-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks, todavía no generado)
```

### Source Code (repository root)

```text
src/CinemaSystemRandomPlay.Domain/
  Compartido/
    Dinero.cs                      (nuevo: Value Object)
    Moneda.cs                      (nuevo: enum)
  Funciones/
    Funcion.cs                     (modificado: PrecioEntrada)
  Reservas/
    Reserva.cs                     (modificado: PrecioUnitario, Total)
    ReservadorDeAsientos.cs        (modificado: pasa funcion.PrecioEntrada a Crear)
    Eventos/ReservaCreada.cs       (modificado: PrecioUnitario, Total)

src/CinemaSystemRandomPlay.Application/
  Compartido/Dtos/
    DineroDto.cs                   (nuevo)
  Funciones/
    Dtos/HorarioFuncionDto.cs, MapaAsientosDto.cs         (modificados: PrecioEntrada)
    Queries/ListarHorariosDisponiblesHoyQuery.cs,
            ObtenerMapaAsientosQuery.cs                   (modificados: mapean el precio)
  Reservas/
    Dtos/ReservaDto.cs             (modificado: PrecioUnitario, Total)
    Ports/IConsultaReservas.cs     (modificado: ReservaLecturaDto.PrecioUnitario)
    Commands/ReservarAsientosCommand.cs   (modificado: mapea montos y el evento)

src/CinemaSystemRandomPlay.Infrastructure/
  Persistence/
    Funciones/FuncionEntityConfiguration.cs     (modificado: ComplexProperty PrecioEntrada)
    Reservas/ReservaEntityConfiguration.cs      (modificado: ComplexProperty PrecioUnitario)
    Reservas/ConsultaReservas.cs                (modificado: lee PrecioUnitario)
    DevDataSeeder.cs                            (modificado: precios sembrados)
  Migrations/
    <timestamp>_AddPrecios.cs      (nuevo: columnas, default temporal y backfill de reservas)

src/CinemaSystemRandomPlay.Api/
  Compartido/
    DineroResponse.cs              (nuevo: monto como string "0.00", InvariantCulture)
  Modulos/Funciones/Contracts/
    HorarioFuncionResponse.cs, MapaAsientosResponse.cs   (modificados: PrecioEntrada al final)
  Modulos/Reservas/Contracts/
    ReservaResponse.cs             (modificado: PrecioUnitario, Total al final)

tests/CinemaSystemRandomPlay.Domain.UnitTests/
  Compartido/DineroTests.cs        (nuevo)
  Funciones/FuncionTests.cs        (modificado: casos de precio y nuevo constructor)
  Reservas/ReservaTests.cs, ReservadorDeAsientosTests.cs, ReservaFactory.cs
                                   (modificados: precio congelado y total)

tests/CinemaSystemRandomPlay.Application.UnitTests/
  Funciones/*Tests.cs, Reservas/ReservasTestSupport.cs   (modificados: nuevo constructor de Funcion)

tests/CinemaSystemRandomPlay.IntegrationTests/
  Funciones/FuncionesControllerTests.cs        (modificado: precioEntrada en horarios)
  Funciones/MapaAsientosControllerTests.cs     (modificado: precioEntrada en el mapa)
  Reservas/ReservasControllerTests.cs          (modificado: montos en crear/listar/consultar,
                                                precio congelado tras cambiar el de la función)
  Funciones/FuncionRepositoryTests.cs, Reservas/ReservasApiFactory.cs, Reservas/ReservaConcurrenteTests.cs
                                               (modificados: nuevo constructor de Funcion)
```

**Structure Decision**: Mismo monolito modular de 4 proyectos, sin módulos nuevos. `Dinero` va en
`Compartido` en cada capa (Domain, Application y Api) porque lo usan `Funciones` y `Reservas` y lo va a
usar la feature de pagos. Los cambios restantes extienden los módulos `Funciones` y `Reservas`
existentes.

## Complexity Tracking

Sin violaciones nuevas a la constitución. La desviación del Principio VI de la feature 004 (outbox sin
publicador) sigue vigente y no cambia con esta feature.
