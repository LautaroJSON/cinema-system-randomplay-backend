# Implementation Plan: Mapa de Asientos de una Función

**Branch**: `003-mapa-asientos-funcion` | **Date**: 2026-09-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-mapa-asientos-funcion/spec.md`

## Summary

Dado un `funcionId`, exponer de solo lectura la disposición de la `Sala` de esa función (filas y
cantidad de asientos por fila) junto con los asientos **disponibles**. Lo que no figura como disponible
está ocupado. La disposición se modela como una colección owned de `FilaSala` dentro del agregado
`Sala`, y se agrega el Value Object `Asiento` que exige la constitución. La ocupación se obtiene a
través de un puerto nuevo, `IOcupacionAsientos`, que en esta feature tiene un stub que no ocupa nada:
la feature de Reservas lo va a reemplazar sin cambiar el contrato. La resta "disposición menos
ocupados" vive en `Sala`. Es una feature pública, sin mutación de estado ni outbox, que se suma al
módulo `Funciones` existente.

## Technical Context

**Language/Version**: C# 12 / .NET 10 (fijo por la constitución)

**Primary Dependencies**: ASP.NET Core (Controllers), `Microsoft.EntityFrameworkCore` 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10 (ya en uso)

**Storage**: PostgreSQL, misma `CinemaDbContext`. Nueva tabla `SalaFilas` (owned de `Sala`).

**Testing**: xUnit + NSubstitute para Domain/Application; `Testcontainers.PostgreSql` + `Microsoft.AspNetCore.Mvc.Testing` para integración (ya en uso)

**Target Platform**: Linux server (API backend, sin frontend propio)

**Project Type**: web-service (backend puro, REST/JSON)

**Performance Goals**: sin objetivo de latencia en la spec (SC-001 mide pasos). Una consulta devuelve como mucho 26 filas, con arreglos de enteros: payload chico.

**Constraints**: acceso público (FR-014); solo lectura, sin seleccionar ni reservar (FR-015); sin ABM de disposición (FR-016); agregar Reservas no debe cambiar el contrato (FR-013); controllers sin lógica; I/O async; "ahora" vía `IReloj`

**Scale/Scope**: 4 Salas sembradas, de 40 a 100 asientos cada una

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Arquitectura Hexagonal | PASS. `Asiento`, `FilaSala` y los cambios en `Sala`/`Funcion` van en `Domain/Funciones`; el caso de uso y el puerto `IOcupacionAsientos` en `Application/Funciones`; EF Core y el stub en `Infrastructure`; el controller solo traduce HTTP. |
| II. DDD Ligero | PASS. `Asiento` es el Value Object que exige la constitución (inmutable, valida sus argumentos). `Sala` protege sus invariantes (al menos una fila, sin letras repetidas) y concentra la regla de disponibilidad (`AsientosDisponibles`). `Funcion.YaComenzo` pasa al dominio. El caso de uso devuelve un resultado explícito de error en vez de `null` o excepciones (research.md, Decisión 6). |
| III. Bordes con DTOs | PASS. `MapaAsientosDto`/`FilaMapaDto` en Application y `MapaAsientosResponse`/`FilaMapaResponse` en Api, con mapeo manual. |
| IV. Inversión de dependencias | PASS. Nuevo puerto `IOcupacionAsientos` con stub en Infrastructure (research.md, Decisión 3). Se reutilizan `IFuncionRepository` (con un método nuevo), `IPeliculaRepository` e `IReloj`. Todo registrado como `Scoped` en `Program.cs`. |
| V. Testing | PASS. Unit tests de dominio sobre objetos reales, unit tests del handler con fakes y tests de integración contra Postgres real. No hay concurrencia en esta feature; los tests de reserva concurrente corresponden a la 004. |
| VI. Outbox + eventos | N/A. Solo lectura. |
| VII. Auth explícita | PASS, con acción requerida: el endpoint se marca `[AllowAnonymous]` explícitamente. |
| VIII. Calidad de código | PASS. Nullable habilitado, queries async, controller que solo traduce el resultado a `200`/`404`/`410`. |

Sin violaciones: no hace falta Complexity Tracking.

**Re-evaluación post-diseño (Phase 1)**: sin cambios. El diseño de [data-model.md](./data-model.md) y
[contracts/](./contracts/mapa-asientos-api.md) no introduce dependencias hacia afuera desde Domain ni
Application, ni lógica en el controller, ni efectos externos. El cambio de firma del constructor de
`Sala` refuerza el Principio II: ya no se puede crear una Sala sin disposición.

## Project Structure

### Documentation (this feature)

```text
specs/003-mapa-asientos-funcion/
├── plan.md               # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
│   └── mapa-asientos-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks, todavía no generado)
```

### Source Code (repository root)

```text
src/CinemaSystemRandomPlay.Domain/
  Funciones/
    Asiento.cs                 (nuevo: Value Object)
    FilaSala.cs                (nuevo: Value Object)
    Sala.cs                    (modificado: Filas, Sucursal, TotalAsientos, AsientosDisponibles)
    Funcion.cs                 (modificado: YaComenzo)
    IFuncionRepository.cs      (modificado: ObtenerConSala)

src/CinemaSystemRandomPlay.Application/
  Funciones/
    Ports/
      IOcupacionAsientos.cs    (nuevo)
    Dtos/
      MapaAsientosDto.cs       (nuevo, incluye FilaMapaDto)
    Queries/
      ObtenerMapaAsientosQuery.cs   (nuevo: query + handler + resultado con tres casos)

src/CinemaSystemRandomPlay.Infrastructure/
  Funciones/
    SinAsientosOcupados.cs     (nuevo: stub de IOcupacionAsientos)
  Persistence/Funciones/
    SalaEntityConfiguration.cs (modificado: OwnsMany Filas + HasData, HasOne(s => s.Sucursal))
    FuncionRepository.cs       (modificado: ObtenerConSala)
  Migrations/
    <timestamp>_AddDisposicionSalas.cs   (nuevo)

src/CinemaSystemRandomPlay.Api/
  Modulos/Funciones/
    MapaAsientosController.cs  (nuevo: GET api/funciones/{funcionId:guid}/asientos)
    Contracts/MapaAsientosResponse.cs   (nuevo, incluye FilaMapaResponse)
  Program.cs                   (modificado: registra IOcupacionAsientos y el handler)

tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/
  AsientoTests.cs, FilaSalaTests.cs    (nuevos)
  SalaTests.cs, FuncionTests.cs        (modificados)

tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/
  ObtenerMapaAsientosQueryHandlerTests.cs      (nuevo)
  ListarHorariosDisponiblesHoyQueryHandlerTests.cs   (modificado: nuevo constructor de Sala)

tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/
  FuncionRepositoryTests.cs            (modificado: ObtenerConSala)
  MapaAsientosControllerTests.cs       (nuevo)
```

**Structure Decision**: Mismo monolito modular de 4 proyectos. La feature se agrega al módulo
`Funciones` existente porque trabaja sobre `Funcion` y `Sala`: no se crean proyectos ni módulos
nuevos. El endpoint va en un controller propio (`MapaAsientosController`) porque su ruta
(`api/funciones/{funcionId}`) no comparte prefijo con la de `FuncionesController`
(`api/funciones/peliculas/{peliculaId}`). El puerto `IOcupacionAsientos` queda en
`Application/Funciones/Ports`, igual que `Application/Catalogo/Ports`, listo para que la feature de
Reservas lo implemente.

## Complexity Tracking

*Sin violaciones al Constitution Check: la sección no aplica.*
