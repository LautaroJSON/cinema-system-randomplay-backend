# Implementation Plan: Horarios por Sucursal — Funciones de Hoy

**Branch**: `002-horarios-sucursal-hoy` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-horarios-sucursal-hoy/spec.md`

## Summary

Dada una `Pelicula` ya elegida, exponer de solo lectura (1) las `Sucursal`es con al menos una
`Funcion` disponible hoy para esa película, y (2) al elegir una de ellas, los horarios de hoy
disponibles (no pasados) en esa Sucursal, ordenados cronológicamente. `Sucursal`, `Sala` y `Funcion`
no tienen alta/administración en esta feature — se consumen como datos ya sembrados. Es una feature de
solo lectura y acceso público (sin autenticación), que no muta estado y por lo tanto no dispara
outbox. Se implementa como un nuevo módulo `Funciones` sobre la arquitectura hexagonal ya existente
(Domain/Application/Infrastructure/Api), reutilizando `IPeliculaRepository` del módulo `Catalogo` para
validar la película, y agregando un puerto `IReloj` (nuevo, exigido por el Principio IV) para resolver
"hoy"/"ahora" de forma testeable.

## Technical Context

**Language/Version**: C# 12 / .NET 10 (fijo por la constitución)

**Primary Dependencies**: ASP.NET Core (Controllers, mismo patrón que `Catalogo`), `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL` (ya en uso)

**Storage**: PostgreSQL (mismo motor y misma `CinemaDbContext` que `001-catalogo-peliculas`)

**Testing**: xUnit para Domain/Application; `Testcontainers.PostgreSql` + `Microsoft.AspNetCore.Mvc.Testing` en `IntegrationTests` para el repositorio y los endpoints contra un Postgres real

**Target Platform**: Linux server (API backend, sin frontend propio)

**Project Type**: web-service (backend puro, expone REST/JSON)

**Performance Goals**: sin objetivo de tiempo explícito en la spec (SC-001/SC-003 miden pasos, no latencia); volumen esperado bajo (dato sembrado, no producción) por lo que no se anticipan problemas de performance

**Constraints**: acceso de lectura público sin autenticación (FR-010); sin alta/edición/administración de `Sucursal`/`Sala`/`Funcion` (FR-011); controllers sin lógica de negocio; nullable habilitado; I/O async de punta a punta; sin efectos secundarios/outbox (no hay mutación de estado); "hoy"/"ahora" deben resolverse vía un puerto inyectable (Principio IV), no vía llamadas directas a la hora del sistema

**Scale/Scope**: dato sembrado de bajo volumen (unas pocas Sucursales/Salas/Funciones por película, ver Assumptions de la spec)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Arquitectura Hexagonal | PASS. `Sucursal`, `Sala`, `Funcion` y sus puertos viven en `Domain/Funciones`; casos de uso en `Application/Funciones`; EF Core en `Infrastructure`; `Api/Modulos/Funciones` solo traduce HTTP. |
| II. DDD Ligero — invariantes | PASS. `Sucursal`/`Sala`/`Funcion` son inválidos de construir (constructores rechazan nombres vacíos / FKs vacías con `DomainException`), aunque esta feature no los crea vía API — la invariante sostiene el tipo para cuando exista la futura feature de administración. |
| III. Bordes explícitos con DTOs | PASS. `SucursalConFuncionesHoyDto` y `HorarioFuncionDto`, mapeo manual explícito (sin AutoMapper), igual que en `Catalogo`. |
| IV. Inversión de dependencias | PASS, con una adición: se introduce `IReloj` (Domain/Compartido) para "hoy"/"ahora", implementado por `RelojSistema` en Infrastructure — ver [research.md](./research.md), Decisión 1. `IFuncionRepository` sigue el mismo patrón que `IPeliculaRepository`. |
| V. Testing no negociable | PASS. Unit tests de Domain (`Sucursal`/`Sala`/`Funcion`) y de Application (handlers, con `IReloj` fake para fijar "ahora" de forma determinística); integración contra Postgres real vía Testcontainers para `FuncionRepository` y los endpoints (no hay concurrencia crítica en esta feature de solo lectura, pero se evita igual el provider InMemory). |
| VI. Outbox + eventos | N/A. Feature de solo lectura, no hay cambios de estado que registrar. |
| VII. Auth explícita | PASS, con acción explícita requerida: los endpoints del nuevo `FuncionesController` deben marcarse `[AllowAnonymous]` de forma deliberada, igual que `Catalogo`. |
| VIII. Calidad de código | PASS. Nullable habilitado (ya en los `.csproj`), queries EF Core async, controllers delgados, sin `.Result`/`.Wait()`. |

Sin violaciones — no se requiere Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/002-horarios-sucursal-hoy/
├── plan.md               # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
│   └── funciones-api.md
└── tasks.md               # Phase 2 output (/speckit-tasks, no generado todavía)
```

### Source Code (repository root)

```text
src/CinemaSystemRandomPlay.Domain/
  Compartido/
    DomainException.cs      (ya existe)
    IReloj.cs                (nuevo — ver research.md, Decisión 1)
  Funciones/
    Sucursal.cs
    Sala.cs
    Funcion.cs
    IFuncionRepository.cs

src/CinemaSystemRandomPlay.Application/
  Funciones/
    Dtos/
      SucursalConFuncionesHoyDto.cs
      HorarioFuncionDto.cs
    Queries/
      ListarSucursalesConFuncionesHoyQuery.cs        (+ Handler: usa IPeliculaRepository + IFuncionRepository + IReloj)
      ListarHorariosDisponiblesHoyQuery.cs            (+ Handler: idem, valida además la Sucursal)

src/CinemaSystemRandomPlay.Infrastructure/
  Compartido/
    RelojSistema.cs           (implementa IReloj con DateTimeOffset.Now)
  Persistence/
    Funciones/
      SucursalEntityConfiguration.cs
      SalaEntityConfiguration.cs
      FuncionEntityConfiguration.cs
      FuncionRepository.cs    (implementa IFuncionRepository; ver research.md, Decisión 3 y 4)

src/CinemaSystemRandomPlay.Api/
  Modulos/Funciones/
    FuncionesController.cs
    Contracts/SucursalConFuncionesHoyResponse.cs, HorarioFuncionResponse.cs

tests/CinemaSystemRandomPlay.Domain.UnitTests/
  Funciones/SucursalTests.cs, SalaTests.cs, FuncionTests.cs

tests/CinemaSystemRandomPlay.Application.UnitTests/
  Funciones/ListarSucursalesConFuncionesHoyQueryHandlerTests.cs, ListarHorariosDisponiblesHoyQueryHandlerTests.cs
  (usan un IReloj fake/stub para fijar "ahora" — ver research.md, Decisión 1)

tests/CinemaSystemRandomPlay.IntegrationTests/
  Funciones/FuncionRepositoryTests.cs, FuncionesControllerTests.cs
```

**Structure Decision**: Mismo monolito modular de 4 proyectos que `001-catalogo-peliculas`
(Domain/Application/Infrastructure/Api), agregando el módulo `Funciones` junto a `Catalogo` — no se
agregan proyectos nuevos ni se tocan los `ProjectReference` existentes. La única dependencia cruzada
entre módulos es de lectura: `Application/Funciones` consume `Domain.Catalogo.IPeliculaRepository` ya
existente para validar la película (FR-012), sin que `Funciones` conozca detalles internos de
`Catalogo` más allá de ese puerto.

## Complexity Tracking

*Sin violaciones al Constitution Check — sección no aplica.*
