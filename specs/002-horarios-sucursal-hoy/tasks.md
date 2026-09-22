# Tasks: Horarios por Sucursal — Funciones de Hoy

**Input**: Design documents from `/specs/002-horarios-sucursal-hoy/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/funciones-api.md](./contracts/funciones-api.md), [quickstart.md](./quickstart.md)

**Tests**: Incluidos. El Principio V de la constitución trata el testing como "ciudadano de primera clase, NO NEGOCIABLE" y prohíbe validar con el provider InMemory de EF Core — se generan tareas de test explícitas para cumplirlo, igual que en `001-catalogo-peliculas`.

**Organization**: Tareas agrupadas por historia de usuario (US1/US2, según prioridad P1/P2 de [spec.md](./spec.md)) para poder implementar y probar cada una de forma independiente.

## Path Conventions

Proyecto único .NET: `src/CinemaSystemRandomPlay.{Domain,Application,Infrastructure,Api}/`, `tests/CinemaSystemRandomPlay.{Domain,Application}.UnitTests/`, `tests/CinemaSystemRandomPlay.IntegrationTests/`.

---

## Phase 1: Setup

**Purpose**: Dependencias de paquetes necesarias para esta feature.

No hace falta ninguna tarea de Setup: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing`, `NSubstitute` y la cadena de conexión `CinemaDb` ya fueron agregados en `001-catalogo-peliculas` y se reutilizan sin cambios (verificado en los `.csproj` y en `appsettings.Development.json`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entidades `Sucursal`/`Sala`/`Funcion`, el puerto `IReloj` y la persistencia base del módulo `Funciones` — las 2 historias de usuario dependen de esto.

**⚠️ CRITICAL**: Ninguna historia de usuario puede empezar hasta completar esta fase.

- [X] T001 [P] Crear puerto `IReloj` (`DateTimeOffset Ahora { get; }`) en `src/CinemaSystemRandomPlay.Domain/Compartido/IReloj.cs`
- [X] T002 [P] Crear entidad `Sucursal` (constructor valida `Nombre` no vacío, lanzando `DomainException`) en `src/CinemaSystemRandomPlay.Domain/Funciones/Sucursal.cs`
- [X] T003 [P] Crear entidad `Sala` (constructor valida `Nombre` no vacío y `SucursalId != Guid.Empty`, lanzando `DomainException`) en `src/CinemaSystemRandomPlay.Domain/Funciones/Sala.cs`
- [X] T004 Crear entidad `Funcion` (constructor valida `PeliculaId != Guid.Empty` y `SalaId != Guid.Empty`; propiedad de navegación de solo lectura `Sala? Sala`) en `src/CinemaSystemRandomPlay.Domain/Funciones/Funcion.cs` (depende de T003, referencia el tipo `Sala`)
- [X] T005 [P] Test unitario de Domain para los invariantes de `Sucursal` en `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/SucursalTests.cs` (depende de T002) — ✅ verde
- [X] T006 [P] Test unitario de Domain para los invariantes de `Sala` en `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/SalaTests.cs` (depende de T003) — ✅ verde
- [X] T007 [P] Test unitario de Domain para los invariantes de `Funcion` en `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/FuncionTests.cs` (depende de T004) — ✅ verde (10/10 en Domain.UnitTests)
- [X] T008 Crear puerto `IFuncionRepository` (`ListarSucursalesConFuncionesDisponibles`, `ExisteFuncionHoy`, `ListarHorariosDisponibles` — ver [data-model.md](./data-model.md) y [research.md](./research.md) Decisión 3) en `src/CinemaSystemRandomPlay.Domain/Funciones/IFuncionRepository.cs` (depende de T002, T003, T004)
- [X] T009 Agregar `DbSet<Sucursal> Sucursales`, `DbSet<Sala> Salas`, `DbSet<Funcion> Funciones` a `CinemaDbContext` en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/CinemaDbContext.cs` (depende de T002, T003, T004)
- [X] T010 [P] Crear `SucursalEntityConfiguration` (mapeo EF Core explícito) en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/SucursalEntityConfiguration.cs` (depende de T009) — incluye `HasData` con 3 Sucursales (ver research.md Decisión 2)
- [X] T011 [P] Crear `SalaEntityConfiguration` (FK a `Sucursal`) en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/SalaEntityConfiguration.cs` (depende de T009) — incluye `HasData` con 4 Salas
- [X] T012 [P] Crear `FuncionEntityConfiguration` (FK a `Pelicula` y a `Sala`) en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/FuncionEntityConfiguration.cs` (depende de T009)
- [X] T013 [P] Crear `RelojSistema` (implementa `IReloj` con `DateTimeOffset.Now`) en `src/CinemaSystemRandomPlay.Infrastructure/Compartido/RelojSistema.cs` (depende de T001)
- [X] T014 Generar la migración de EF Core para las tablas `Sucursales`/`Salas`/`Funciones`, con seed `HasData` para `Sucursal`/`Sala` (dato de referencia estático — `Funcion` NO se siembra en la migración, ver [research.md](./research.md) Decisión 2) en `src/CinemaSystemRandomPlay.Infrastructure/Migrations/` (depende de T010, T011, T012) — migración `AddFuncionesModule` generada y revisada
- [X] T015 Registrar `IReloj`→`RelojSistema` en el contenedor DI en `src/CinemaSystemRandomPlay.Api/Program.cs` (depende de T013)

**Checkpoint**: Con esto compilado y migrado, las 2 historias de usuario pueden empezar.

---

## Phase 3: User Story 1 - Ver Sucursales con funciones hoy para una película (Priority: P1) 🎯 MVP

**Goal**: Dada una película, el Cliente ve las Sucursales que tienen al menos una función disponible hoy para esa película.

**Independent Test**: `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy` sin autenticación devuelve solo las Sucursales con funciones hoy (y `[]` si no hay ninguna, `404` si la película no existe/está inactiva), sin depender de User Story 2.

### Tests for User Story 1 ⚠️

> Escribir estos tests primero, verificar que fallan antes de implementar.

- [X] T016 [P] [US1] Test unitario de Application para `ListarSucursalesConFuncionesHoyQueryHandler` (con Sucursales, sin Sucursales, película no encontrada/inactiva → `null`), mockeando `IPeliculaRepository`/`IFuncionRepository`/`IReloj` con NSubstitute (fijando "ahora" de forma determinística), en `tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/ListarSucursalesConFuncionesHoyQueryHandlerTests.cs` — ✅ 5/5 verde
- [X] T017 [P] [US1] Test de integración para `FuncionRepository.ListarSucursalesConFuncionesDisponibles()` contra Postgres real (Testcontainers) en `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionRepositoryTests.cs` — ✅ verde
- [X] T018 [P] [US1] Test de integración para `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy` (200 con datos, 200 con `[]`, 404 película inexistente/inactiva) vía `WebApplicationFactory` en `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionesControllerTests.cs` — ✅ verde (8/8 en `Funciones/`, 19/19 en toda la suite de integración, sin regresiones en `Catalogo`)

### Implementation for User Story 1

- [X] T019 [P] [US1] Crear `SucursalConFuncionesHoyDto` en `src/CinemaSystemRandomPlay.Application/Funciones/Dtos/SucursalConFuncionesHoyDto.cs`
- [X] T020 [US1] Implementar `ListarSucursalesConFuncionesHoyQuery` + Handler (valida película vía `IPeliculaRepository` — `null`/inactiva → `null`; usa `IFuncionRepository.ListarSucursalesConFuncionesDisponibles` + `IReloj` para "hoy"/"ahora"; mapea `Sucursal`→`SucursalConFuncionesHoyDto`) en `src/CinemaSystemRandomPlay.Application/Funciones/Queries/ListarSucursalesConFuncionesHoyQuery.cs` (depende de T008, T019)
- [X] T021 [P] [US1] Implementar `FuncionRepository.ListarSucursalesConFuncionesDisponibles()` (join `Funcion`→`Sala`→`Sucursal`, filtro por `PeliculaId` + `FechaHoraInicio` de hoy y no pasada, `Distinct`, orden alfabético por `Nombre`) en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/FuncionRepository.cs` (depende de T008, T014) — se agregó además un `HasConversion` UTC explícito en `FuncionEntityConfiguration.FechaHoraInicio` (Npgsql exige offset 0 para `timestamp with time zone`), no estaba previsto en el research.md original
- [X] T022 [P] [US1] Crear `SucursalConFuncionesHoyResponse` (contrato HTTP) con mapeo explícito desde el DTO en `src/CinemaSystemRandomPlay.Api/Modulos/Funciones/Contracts/SucursalConFuncionesHoyResponse.cs`
- [X] T023 [US1] Implementar `FuncionesController` con la acción `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy`, marcada `[AllowAnonymous]` de forma explícita, `404` si el handler devuelve `null`, en `src/CinemaSystemRandomPlay.Api/Modulos/Funciones/FuncionesController.cs` (depende de T020, T022)
- [X] T024 [US1] Registrar `IFuncionRepository`→`FuncionRepository` y `ListarSucursalesConFuncionesHoyQueryHandler` en el contenedor DI, en `src/CinemaSystemRandomPlay.Api/Program.cs` (depende de T021, T023)

**Checkpoint**: User Story 1 funcional y testeable de punta a punta (MVP).

---

## Phase 4: User Story 2 - Ver horarios de hoy en la Sucursal elegida (Priority: P2)

**Goal**: Al elegir una Sucursal de la lista de US1, el Cliente ve por defecto los horarios de hoy disponibles para esa película en esa Sucursal, ordenados cronológicamente.

**Independent Test**: `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` devuelve los horarios de hoy no pasados de una Sucursal que ya se sabe tiene funciones hoy (dato sembrado), y `404` para una Sucursal inválida para esa película, sin depender de que el listado de US1 se haya consultado antes en la misma request.

### Tests for User Story 2 ⚠️

- [X] T025 [P] [US2] Test unitario de Application para `ListarHorariosDisponiblesHoyQueryHandler` (con horarios, todos pasaron → `[]`, película no encontrada → `null`, Sucursal no válida para la película → `null`), mockeando `IPeliculaRepository`/`IFuncionRepository`/`IReloj` con NSubstitute, en `tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/ListarHorariosDisponiblesHoyQueryHandlerTests.cs` — ✅ 4/4 verde (9/9 en `Funciones/`, 14/14 en toda la suite de Application.UnitTests)
- [X] T026 [US2] Test de integración para `FuncionRepository.ExisteFuncionHoy()` y `ListarHorariosDisponibles()` contra Postgres real en `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionRepositoryTests.cs` (depende de T017, mismo archivo) — ✅ verde
- [X] T027 [US2] Test de integración para `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` (200 ordenado cronológicamente, 200 con `[]`, 404 película, 404 Sucursal no válida) en `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionesControllerTests.cs` (depende de T018, mismo archivo) — ✅ verde (26/26 en toda la suite de integración); se ajustaron los offsets de tiempo de horas a minutos en los tests contra el reloj real para evitar flakiness al cruzar medianoche (detectado en la primera corrida)

### Implementation for User Story 2

- [X] T028 [P] [US2] Crear `HorarioFuncionDto` en `src/CinemaSystemRandomPlay.Application/Funciones/Dtos/HorarioFuncionDto.cs`
- [X] T029 [US2] Implementar `ListarHorariosDisponiblesHoyQuery` + Handler (valida película igual que US1 → `null` si no encontrada/inactiva; usa `IFuncionRepository.ExisteFuncionHoy` para distinguir Sucursal no válida [FR-013, `null`] de Sucursal válida sin horarios [FR-009, `[]`]; usa `ListarHorariosDisponibles` + `IReloj`; mapea `Funcion`→`HorarioFuncionDto` incluyendo `Sala.Nombre`) en `src/CinemaSystemRandomPlay.Application/Funciones/Queries/ListarHorariosDisponiblesHoyQuery.cs` (depende de T008, T028)
- [X] T030 [US2] Extender `FuncionRepository` con `ExisteFuncionHoy()` y `ListarHorariosDisponibles()` (este último con `Include(f => f.Sala)`, orden cronológico ascendente por `FechaHoraInicio`) en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/FuncionRepository.cs` (depende de T021, mismo archivo) — implementado en la misma pasada que T021, ya estaba resuelto antes de llegar a esta fase
- [X] T031 [P] [US2] Crear `HorarioFuncionResponse` (contrato HTTP) en `src/CinemaSystemRandomPlay.Api/Modulos/Funciones/Contracts/HorarioFuncionResponse.cs`
- [X] T032 [US2] Agregar la acción `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` (`[AllowAnonymous]`, `404` si el handler devuelve `null`) en `FuncionesController` (depende de T023, T029, T031, mismo archivo)
- [X] T033 [US2] Registrar `ListarHorariosDisponiblesHoyQueryHandler` en el contenedor DI, en `src/CinemaSystemRandomPlay.Api/Program.cs` (depende de T024, T030, T032, mismo archivo)

**Checkpoint**: User Story 1 y 2 funcionan de forma independiente.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T034 [P] Ejecutar manualmente los 6 escenarios de [quickstart.md](./quickstart.md) de punta a punta contra Postgres local — ✅ ejecutado contra Postgres real (Docker, puerto 5433) con `dotnet run`: Sucursal Centro (función hoy no pasada) aparece en `sucursales-hoy` y en `horarios-hoy`; Sucursal Norte (función hoy ya pasada) NO aparece en `sucursales-hoy` pero `horarios-hoy` devuelve `200 []` (FR-009); Sucursal Sur (función de mañana) devuelve `404` en `horarios-hoy` (FR-013, nunca tuvo función hoy); película inexistente → `404` (FR-012); Sucursal con id inexistente → `404`. Contenedor y proceso detenidos y limpiados al terminar.
- [X] T035 Revisar que ambas acciones de `FuncionesController` estén marcadas `[AllowAnonymous]` de forma explícita (Principio VII de la constitución — ningún endpoint sin política declarada) — verificado con grep, ambas acciones (`ListarSucursalesConFuncionesHoy`, `ListarHorariosDisponiblesHoy`) la tienen
- [X] T036 [P] Confirmar que `NingunaFuncionDisponibleChecker` (módulo `Catalogo`, feature `001`) queda sin modificar en esta feature — su actualización para consultar la tabla real de `Funciones` es deuda técnica documentada en [research.md](./research.md) ("Nota fuera de alcance"), no parte de esta entrega — confirmado, el archivo no fue tocado en esta implementación

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin tareas — no hace falta nada nuevo.
- **Foundational (Phase 2)**: bloquea las 2 historias de usuario.
- **User Stories (Phase 3-4)**: ambas dependen de Foundational. US1 y US2 son conceptualmente independientes entre sí, pero **comparten archivos** (`FuncionRepository.cs`, `FuncionesController.cs`, `Program.cs`, y los archivos de test de integración), así que en la práctica conviene implementarlas en orden P1 → P2 para evitar conflictos de edición simultánea sobre el mismo archivo.
- **Polish (Phase 5)**: depende de que las historias que se quieran entregar estén completas.

### Parallel Opportunities

- T001, T002, T003 (Foundational, archivos distintos) en paralelo.
- T005, T006, T007 (tests de invariantes, archivos distintos) en paralelo entre sí una vez completas sus entidades respectivas.
- T010, T011, T012, T013 en paralelo (archivos distintos, todas dependen solo de T009/T001 ya completas).
- Dentro de US1: T016, T017, T018 (tests, archivos distintos) en paralelo; T019 y T021 en paralelo con T020 en cuanto T019 esté lista; T022 en paralelo con T019/T021.
- Dentro de US2: T025 en paralelo con el resto (archivo propio); T028 y T031 en paralelo.
- US1 y US2 pueden desarrollarse en paralelo por personas distintas **solo si** se coordina el orden de merge sobre `FuncionRepository.cs`, `FuncionesController.cs` y `Program.cs` (mismos archivos, ver nota arriba).

---

## Parallel Example: User Story 1

```bash
# Tests de US1 en paralelo (archivos distintos):
Task: "Test unitario ListarSucursalesConFuncionesHoyQueryHandler en tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/ListarSucursalesConFuncionesHoyQueryHandlerTests.cs"
Task: "Test de integración FuncionRepository.ListarSucursalesConFuncionesDisponibles en tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionRepositoryTests.cs"
Task: "Test de integración GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy en tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionesControllerTests.cs"

# DTO e implementación de repositorio de US1 en paralelo:
Task: "SucursalConFuncionesHoyDto en src/CinemaSystemRandomPlay.Application/Funciones/Dtos/SucursalConFuncionesHoyDto.cs"
Task: "FuncionRepository.ListarSucursalesConFuncionesDisponibles en src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/FuncionRepository.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 solamente)

1. Completar Phase 1: Setup (sin tareas)
2. Completar Phase 2: Foundational (crítico — bloquea todo)
3. Completar Phase 3: User Story 1
4. **Parar y validar**: correr `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy` a mano y los tests de US1
5. Esto ya es demostrable: "¿en qué Sucursales puedo ver esta película hoy?"

### Entrega incremental

1. Setup + Foundational → base lista (`Sucursal`/`Sala`/`Funcion`, `IReloj`)
2. User Story 1 → validar → demo (MVP: Sucursales con funciones hoy)
3. User Story 2 → validar → demo (horarios de hoy en la Sucursal elegida)

---

## Notes

- `[P]` = archivos distintos, sin dependencias pendientes entre sí.
- La etiqueta `[US1]`/`[US2]` traza cada tarea a su historia en [spec.md](./spec.md).
- Varias tareas de US2 modifican archivos creados en US1 (`FuncionRepository.cs`, `FuncionesController.cs`, `Program.cs`, `FuncionRepositoryTests.cs`, `FuncionesControllerTests.cs`) — están marcadas sin `[P]` y con su dependencia explícita por eso.
- Verificar que los tests fallan antes de implementar cada tarea de implementación correspondiente.
- Los tests unitarios de Application deben fijar "ahora" con un `IReloj` fake/stub (NSubstitute), nunca depender de la hora real del reloj del sistema (ver [research.md](./research.md), Decisión 1).
- No se generan tareas de outbox/SignalR/Stripe/auth JWT — esta feature es de solo lectura y pública (Principio VI no aplica, Principio VII solo exige el `[AllowAnonymous]` explícito).
- No se generan tareas de alta/edición/administración de `Sucursal`/`Sala`/`Funcion` — están explícitamente fuera de alcance (FR-011); sus datos se cargan con la migración (`HasData` para `Sucursal`/`Sala`) o manualmente (`Funcion`, ver [quickstart.md](./quickstart.md)).
