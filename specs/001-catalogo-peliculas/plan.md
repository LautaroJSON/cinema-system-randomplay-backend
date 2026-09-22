# Implementation Plan: Catálogo de Películas

**Branch**: `001-catalogo-peliculas` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-catalogo-peliculas/spec.md`

## Summary

Exponer un catálogo de lectura de `Pelicula`s: listado de películas activas (con filtro opcional por título) y detalle de una película puntual, marcando visualmente las que todavía no tienen ninguna `Funcion` programada. Es una feature de solo lectura, de acceso público (sin autenticación), que no muta estado — por lo tanto no dispara outbox ni efectos secundarios. Se implementa con la arquitectura hexagonal ya definida (Domain/Application/Infrastructure/Api), dentro del módulo `Catalogo`, sobre EF Core + PostgreSQL.

## Technical Context

**Language/Version**: C# 12 / .NET 10 (fijo por la constitución)

**Primary Dependencies**: ASP.NET Core (Controllers, ya scaffolded en `Api`), `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL`

**Storage**: PostgreSQL (mismo motor en dev/test/prod, por restricción de plataforma de la constitución)

**Testing**: xUnit (ya presente en los 3 proyectos de test) para Domain/Application; `Testcontainers.PostgreSql` + `Microsoft.AspNetCore.Mvc.Testing` en `IntegrationTests` para validar el repositorio y los endpoints contra un Postgres real

**Target Platform**: Linux server (API backend, sin frontend propio)

**Project Type**: web-service (backend puro, expone REST/JSON)

**Performance Goals**: listado completo visible en <2s (SC-001)

**Constraints**: acceso de lectura público sin autenticación (FR-007); controllers sin lógica de negocio; nullable habilitado; I/O async de punta a punta; sin efectos secundarios/outbox (no hay mutación de estado en esta feature)

**Scale/Scope**: catálogo de tamaño moderado (decenas de películas activas, por Assumptions de la spec)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Arquitectura Hexagonal | PASS. `Pelicula` y su puerto de repositorio viven en `Domain/Catalogo`; casos de uso en `Application/Catalogo`; EF Core y el chequeo de disponibilidad de funciones en `Infrastructure`; `Api/Modulos/Catalogo` solo traduce HTTP. |
| II. DDD Ligero — invariantes | PASS. `Pelicula` mantiene sus invariantes de construcción (título no vacío, duración positiva) aunque esta feature no la crea — las ejercitará la futura feature de administración; acá se consume como entidad ya persistida. |
| III. Bordes explícitos con DTOs | PASS. `PeliculaListItemDto` y `PeliculaDetalleDto`, mapeo manual explícito (sin AutoMapper por convención implícita). |
| IV. Inversión de dependencias | PASS. `IPeliculaRepository` (Domain) + `IFuncionAvailabilityChecker` (Application, puerto de orquestación) resueltos vía DI nativo. |
| V. Testing no negociable | PASS. Unit tests de Domain/Application; integración contra Postgres real vía Testcontainers para el repositorio y los endpoints (no hay concurrencia crítica en esta feature de solo lectura, pero se evita igual el provider InMemory). |
| VI. Outbox + eventos | N/A. Feature de solo lectura, no hay cambios de estado que registrar. |
| VII. Auth explícita | PASS, con acción explícita requerida: los endpoints de Catálogo deben marcarse `[AllowAnonymous]` de forma deliberada (no simplemente omitir `[Authorize]`), para cumplir "los endpoints públicos se marcan de forma explícita". |
| VIII. Calidad de código | PASS. Nullable habilitado (ya en los `.csproj`), queries EF Core async, controllers delgados. |

Sin violaciones — no se requiere Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/001-catalogo-peliculas/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── catalogo-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks, no generado todavía)
```

### Source Code (repository root)

```text
src/CinemaSystemRandomPlay.Domain/
  Compartido/
    Entity.cs, AggregateRoot.cs, DomainException.cs
  Catalogo/
    Pelicula.cs
    Clasificacion.cs           (VO simple o enum, ej. ATP/+13/+16/+18)
    IPeliculaRepository.cs

src/CinemaSystemRandomPlay.Application/
  Catalogo/
    Dtos/
      PeliculaListItemDto.cs
      PeliculaDetalleDto.cs
    Queries/
      ListarPeliculasQuery.cs        (+ Handler: filtro opcional por título)
      ObtenerDetallePeliculaQuery.cs (+ Handler)
    Ports/
      IFuncionAvailabilityChecker.cs  (puerto: ¿existe alguna Funcion para esta Pelicula?)

src/CinemaSystemRandomPlay.Infrastructure/
  Persistence/
    CinemaDbContext.cs
    Catalogo/
      PeliculaEntityConfiguration.cs
      PeliculaRepository.cs
  Catalogo/
    NingunaFuncionDisponibleChecker.cs   (implementación stub: siempre "sin funciones", hasta que exista la feature Funciones — ver research.md)

src/CinemaSystemRandomPlay.Api/
  Modulos/Catalogo/
    PeliculasController.cs
    Contracts/PeliculaListItemResponse.cs, PeliculaDetalleResponse.cs

tests/CinemaSystemRandomPlay.Domain.UnitTests/
  Catalogo/PeliculaTests.cs

tests/CinemaSystemRandomPlay.Application.UnitTests/
  Catalogo/ListarPeliculasQueryHandlerTests.cs, ObtenerDetallePeliculaQueryHandlerTests.cs

tests/CinemaSystemRandomPlay.IntegrationTests/
  Catalogo/PeliculaRepositoryTests.cs, PeliculasControllerTests.cs
```

**Structure Decision**: Monolito modular de 4 proyectos ya existente (Domain/Application/Infrastructure/Api), subdividido por módulo (`Catalogo` para esta feature). No se agregan proyectos nuevos. Verificado directamente en los `.csproj`: `Application→Domain`, `Infrastructure→Application` y `Api→Application,Infrastructure` ya están cableados — no hace falta ninguna tarea de wiring de `ProjectReference`.

## Complexity Tracking

*Sin violaciones al Constitution Check — sección no aplica.*
