# Tasks: Catálogo de Películas

**Input**: Design documents from `/specs/001-catalogo-peliculas/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/catalogo-api.md](./contracts/catalogo-api.md)

**Tests**: Incluidos. El Principio V de la constitución trata el testing como "ciudadano de primera clase, NO NEGOCIABLE" y prohíbe validar con el provider InMemory de EF Core — se generan tareas de test explícitas para cumplirlo.

**Organization**: Tareas agrupadas por historia de usuario (US1/US2/US3, según prioridad P1/P2/P3 de [spec.md](./spec.md)) para poder implementar y probar cada una de forma independiente.

## Path Conventions

Proyecto único .NET: `src/CinemaSystemRandomPlay.{Domain,Application,Infrastructure,Api}/`, `tests/CinemaSystemRandomPlay.{Domain,Application}.UnitTests/`, `tests/CinemaSystemRandomPlay.IntegrationTests/`.

---

## Phase 1: Setup

**Purpose**: Dependencias de paquetes necesarias para esta feature (los `ProjectReference` entre los 4 proyectos ya existen, verificado en los `.csproj`).

- [X] T001 [P] Agregar `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` y `Npgsql.EntityFrameworkCore.PostgreSQL` en `src/CinemaSystemRandomPlay.Infrastructure/CinemaSystemRandomPlay.Infrastructure.csproj`
- [X] T002 [P] Agregar `Testcontainers.PostgreSql` y `Microsoft.AspNetCore.Mvc.Testing` en `tests/CinemaSystemRandomPlay.IntegrationTests/CinemaSystemRandomPlay.IntegrationTests.csproj`
- [X] T003 [P] Agregar `NSubstitute` en `tests/CinemaSystemRandomPlay.Application.UnitTests/CinemaSystemRandomPlay.Application.UnitTests.csproj`
- [X] T004 [P] Agregar la cadena de conexión `CinemaDb` (Postgres local) en `src/CinemaSystemRandomPlay.Api/appsettings.Development.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entidad `Pelicula`, su puerto de repositorio y la persistencia base — las 3 historias de usuario dependen de esto.

**⚠️ CRITICAL**: Ninguna historia de usuario puede empezar hasta completar esta fase.

- [X] T005 Crear `DomainException` en `src/CinemaSystemRandomPlay.Domain/Compartido/DomainException.cs`
- [X] T006 [P] Crear enum `Clasificacion` (ATP, Mas13, Mas16, Mas18) en `src/CinemaSystemRandomPlay.Domain/Catalogo/Clasificacion.cs`
- [X] T007 Crear entidad `Pelicula` (constructor valida `Titulo` no vacío y `DuracionMinutos > 0`, lanzando `DomainException`) en `src/CinemaSystemRandomPlay.Domain/Catalogo/Pelicula.cs` (depende de T005, T006)
- [X] T008 [P] Test unitario de Domain para los invariantes de `Pelicula` en `tests/CinemaSystemRandomPlay.Domain.UnitTests/Catalogo/PeliculaTests.cs` (depende de T007)
- [X] T009 Crear puerto `IPeliculaRepository` (`ListarActivas`, `ObtenerPorId`) en `src/CinemaSystemRandomPlay.Domain/Catalogo/IPeliculaRepository.cs` (depende de T007)
- [X] T010 Crear `CinemaDbContext` con `DbSet<Pelicula>` en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/CinemaDbContext.cs` (depende de T007)
- [X] T011 Crear `PeliculaEntityConfiguration` (mapeo EF Core explícito) en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Catalogo/PeliculaEntityConfiguration.cs` (depende de T010)
- [X] T012 Generar la migración inicial de EF Core para la tabla `Peliculas` en `src/CinemaSystemRandomPlay.Infrastructure/Migrations/` (depende de T011) — requirió agregar `Microsoft.EntityFrameworkCore.Design` también al proyecto `Api` (startup project), no solo a `Infrastructure`, para que `dotnet ef` funcione
- [X] T013 Registrar `CinemaDbContext` con el proveedor Npgsql y la cadena de conexión en `src/CinemaSystemRandomPlay.Api/Program.cs` (depende de T010, T004)

**Checkpoint**: Con esto compilado y migrado, las 3 historias de usuario pueden empezar.

---

## Phase 3: User Story 1 - Ver el listado de películas en cartelera (Priority: P1) 🎯 MVP

**Goal**: El Cliente entra a "Catálogo" y ve todas las películas activas con título, duración, clasificación y si tienen o no funciones disponibles.

**Independent Test**: `GET /api/catalogo/peliculas` sin autenticación devuelve las películas activas (y `[]` si no hay ninguna), sin depender de Sucursales/Funciones/Reservas.

### Tests for User Story 1 ⚠️

> Escribir estos tests primero, verificar que fallan antes de implementar.

- [X] T014 [P] [US1] Test unitario de Application para `ListarPeliculasQueryHandler` (sin filtro), mockeando `IPeliculaRepository`/`IFuncionAvailabilityChecker` con NSubstitute, en `tests/CinemaSystemRandomPlay.Application.UnitTests/Catalogo/ListarPeliculasQueryHandlerTests.cs` — ✅ 2/2 verde
- [X] T015 [P] [US1] Test de integración para `PeliculaRepository.ListarActivas()` contra Postgres real (Testcontainers) en `tests/CinemaSystemRandomPlay.IntegrationTests/Catalogo/PeliculaRepositoryTests.cs` — escrito y compila; no ejecutado en esta sesión (Docker no disponible en el entorno)
- [X] T016 [P] [US1] Test de integración para `GET /api/catalogo/peliculas` (200 con datos, 200 con lista vacía) vía `WebApplicationFactory` en `tests/CinemaSystemRandomPlay.IntegrationTests/Catalogo/PeliculasControllerTests.cs` — escrito y compila (junto con `CatalogoApiFactory.cs`); no ejecutado en esta sesión (Docker no disponible)

### Implementation for User Story 1

- [X] T017 [P] [US1] Crear puerto `IFuncionAvailabilityChecker` en `src/CinemaSystemRandomPlay.Application/Catalogo/Ports/IFuncionAvailabilityChecker.cs`
- [X] T018 [P] [US1] Crear `PeliculaListItemDto` en `src/CinemaSystemRandomPlay.Application/Catalogo/Dtos/PeliculaListItemDto.cs`
- [X] T019 [US1] Implementar `ListarPeliculasQuery` + Handler (mapea `Pelicula`→`PeliculaListItemDto`, resuelve `SinFuncionesDisponibles`) en `src/CinemaSystemRandomPlay.Application/Catalogo/Queries/ListarPeliculasQuery.cs` (depende de T009, T017, T018)
- [X] T020 [US1] Implementar `PeliculaRepository.ListarActivas()` en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Catalogo/PeliculaRepository.cs` (depende de T009, T011) — se implementó junto con `ObtenerPorId` (T030) en el mismo archivo por ser trivial
- [X] T021 [P] [US1] Implementar `NingunaFuncionDisponibleChecker` (stub, siempre devuelve "sin funciones" — ver [research.md](./research.md) Decisión 1) en `src/CinemaSystemRandomPlay.Infrastructure/Catalogo/NingunaFuncionDisponibleChecker.cs` (depende de T017)
- [X] T022 [P] [US1] Crear `PeliculaListItemResponse` (contrato HTTP) con mapeo explícito desde el DTO en `src/CinemaSystemRandomPlay.Api/Modulos/Catalogo/Contracts/PeliculaListItemResponse.cs`
- [X] T023 [US1] Implementar `PeliculasController` con la acción `GET /api/catalogo/peliculas`, marcada `[AllowAnonymous]` de forma explícita, en `src/CinemaSystemRandomPlay.Api/Modulos/Catalogo/PeliculasController.cs` (depende de T019, T022) — de paso se eliminó el `WeatherForecastController`/`WeatherForecast.cs` de plantilla, ya sin uso
- [X] T024 [US1] Registrar `IPeliculaRepository`→`PeliculaRepository` e `IFuncionAvailabilityChecker`→`NingunaFuncionDisponibleChecker` en el contenedor DI, en `src/CinemaSystemRandomPlay.Api/Program.cs` (depende de T020, T021, T023)

**Checkpoint**: User Story 1 funcional y testeable de punta a punta (MVP).

---

## Phase 4: User Story 2 - Ver el detalle de una película (Priority: P2)

**Goal**: Desde el listado, el Cliente selecciona una película y ve título, duración, clasificación y sinopsis.

**Independent Test**: `GET /api/catalogo/peliculas/{id}` devuelve el detalle completo de una película activa, y `404` para una inexistente o desactivada.

### Tests for User Story 2 ⚠️

- [X] T025 [P] [US2] Test unitario de Application para `ObtenerDetallePeliculaQueryHandler` (encontrada, inactiva→no encontrada, inexistente) en `tests/CinemaSystemRandomPlay.Application.UnitTests/Catalogo/ObtenerDetallePeliculaQueryHandlerTests.cs` — ✅ 5/5 verde (junto con T014)
- [X] T026 [US2] Test de integración para `PeliculaRepository.ObtenerPorId()` en `tests/CinemaSystemRandomPlay.IntegrationTests/Catalogo/PeliculaRepositoryTests.cs` (depende de T015, mismo archivo) — escrito y compila; no ejecutado (Docker no disponible)
- [X] T027 [US2] Test de integración para `GET /api/catalogo/peliculas/{id}` (200 detalle, 404 inactiva, 404 inexistente) en `tests/CinemaSystemRandomPlay.IntegrationTests/Catalogo/PeliculasControllerTests.cs` (depende de T016, mismo archivo) — escrito y compila; no ejecutado (Docker no disponible)

### Implementation for User Story 2

- [X] T028 [P] [US2] Crear `PeliculaDetalleDto` en `src/CinemaSystemRandomPlay.Application/Catalogo/Dtos/PeliculaDetalleDto.cs`
- [X] T029 [US2] Implementar `ObtenerDetallePeliculaQuery` + Handler (devuelve `null` si no existe o `Activa == false`) en `src/CinemaSystemRandomPlay.Application/Catalogo/Queries/ObtenerDetallePeliculaQuery.cs` (depende de T009, T017, T028)
- [X] T030 [US2] Implementar `PeliculaRepository.ObtenerPorId(id)` en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Catalogo/PeliculaRepository.cs` (depende de T020, mismo archivo) — ya estaba implementado junto con T020
- [X] T031 [P] [US2] Crear `PeliculaDetalleResponse` (contrato HTTP) en `src/CinemaSystemRandomPlay.Api/Modulos/Catalogo/Contracts/PeliculaDetalleResponse.cs`
- [X] T032 [US2] Agregar la acción `GET /api/catalogo/peliculas/{id}` (`[AllowAnonymous]`, `404` si el handler devuelve `null`) en `PeliculasController` (depende de T023, T029, T031, mismo archivo)

**Checkpoint**: User Story 1 y 2 funcionan de forma independiente.

---

## Phase 5: User Story 3 - Buscar películas por título (Priority: P3)

**Goal**: El Cliente filtra el listado por coincidencia parcial de título.

**Independent Test**: `GET /api/catalogo/peliculas?titulo=<substring>` devuelve solo las coincidencias, case-insensitive.

### Tests for User Story 3 ⚠️

- [X] T033 [P] [US3] Test unitario de Application: `ListarPeliculasQueryHandler` filtra por título parcial case-insensitive, en `tests/CinemaSystemRandomPlay.Application.UnitTests/Catalogo/ListarPeliculasQueryHandlerTests.cs` (depende de T014, mismo archivo) — ✅ 6/6 verde
- [X] T034 [US3] Test de integración: `PeliculaRepository.ListarActivas(titulo)` filtra correctamente contra Postgres real, en `tests/CinemaSystemRandomPlay.IntegrationTests/Catalogo/PeliculaRepositoryTests.cs` (depende de T015/T026, mismo archivo) — escrito y compila; no ejecutado (Docker no disponible)
- [X] T035 [US3] Test de integración: `GET /api/catalogo/peliculas?titulo=` devuelve coincidencias y `[]` cuando no hay match, en `tests/CinemaSystemRandomPlay.IntegrationTests/Catalogo/PeliculasControllerTests.cs` (depende de T016/T027, mismo archivo) — escrito y compila; no ejecutado (Docker no disponible)

### Implementation for User Story 3

- [X] T036 [US3] Extender `ListarPeliculasQuery`/Handler para aceptar un parámetro opcional `Titulo` en `src/CinemaSystemRandomPlay.Application/Catalogo/Queries/ListarPeliculasQuery.cs` (depende de T019, mismo archivo)
- [X] T037 [US3] Extender `PeliculaRepository.ListarActivas` para aceptar `titulo?` y aplicar `ILIKE '%...%'` en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Catalogo/PeliculaRepository.cs` (depende de T020/T030, mismo archivo) — usa `EF.Functions.ILike` de Npgsql
- [X] T038 [US3] Agregar el query param `titulo` a la acción de listado en `PeliculasController` en `src/CinemaSystemRandomPlay.Api/Modulos/Catalogo/PeliculasController.cs` (depende de T023/T032, mismo archivo)

**Checkpoint**: Las 3 historias de usuario funcionan, cada una independientemente verificable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T039 [P] Ejecutar manualmente los 4 escenarios de [quickstart.md](./quickstart.md) de punta a punta contra Postgres local — parcial: se verificó que la app levanta y que el pipeline completo (routing → DI → EF Core → Npgsql) se ejecuta correctamente (`dotnet run` + request real, stack trace confirma que llega hasta `PeliculaRepository.ListarActivas`); no se pudo validar contra un Postgres real porque este entorno no tiene Docker/Postgres disponible — pendiente de correr localmente por el usuario
- [X] T040 Revisar que ambas acciones del `PeliculasController` estén marcadas `[AllowAnonymous]` de forma explícita (Principio VII de la constitución — ningún endpoint sin política declarada) — verificado con grep, ambas acciones (`Listar`, `ObtenerDetalle`) la tienen

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias, se puede arrancar ya.
- **Foundational (Phase 2)**: depende de Setup — bloquea las 3 historias de usuario.
- **User Stories (Phase 3-5)**: todas dependen de Foundational. US1, US2 y US3 son conceptualmente independientes entre sí, pero **comparten archivos** (`ListarPeliculasQuery.cs`, `PeliculaRepository.cs`, `PeliculasController.cs`, y los archivos de test de integración), así que en la práctica conviene implementarlas en orden P1 → P2 → P3 para evitar conflictos de edición simultánea sobre el mismo archivo.
- **Polish (Phase 6)**: depende de que las historias que se quieran entregar estén completas.

### Parallel Opportunities

- T001-T004 (Setup) en paralelo.
- T006 y T008 en paralelo respecto a otras tareas de su fase (no dependen del mismo archivo que T007/T009-T013 en el momento en que se lanzan, salvo las dependencias explícitas anotadas).
- Dentro de US1: T014, T015, T016 (tests, archivos distintos) en paralelo; T017, T018 en paralelo; T021, T022 en paralelo.
- Dentro de US2: T025 en paralelo con el resto (archivo propio); T028 y T031 en paralelo.
- US2 y US3 pueden desarrollarse en paralelo por personas distintas **solo si** se coordina el orden de merge sobre `PeliculaRepository.cs` y `PeliculasController.cs` (mismo archivo, ver nota arriba).

---

## Parallel Example: User Story 1

```bash
# Tests de US1 en paralelo (archivos distintos):
Task: "Test unitario ListarPeliculasQueryHandler en tests/CinemaSystemRandomPlay.Application.UnitTests/Catalogo/ListarPeliculasQueryHandlerTests.cs"
Task: "Test de integración PeliculaRepository.ListarActivas en tests/CinemaSystemRandomPlay.IntegrationTests/Catalogo/PeliculaRepositoryTests.cs"
Task: "Test de integración GET /api/catalogo/peliculas en tests/CinemaSystemRandomPlay.IntegrationTests/Catalogo/PeliculasControllerTests.cs"

# Puertos/DTOs de US1 en paralelo:
Task: "IFuncionAvailabilityChecker en src/CinemaSystemRandomPlay.Application/Catalogo/Ports/IFuncionAvailabilityChecker.cs"
Task: "PeliculaListItemDto en src/CinemaSystemRandomPlay.Application/Catalogo/Dtos/PeliculaListItemDto.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 solamente)

1. Completar Phase 1: Setup
2. Completar Phase 2: Foundational (crítico — bloquea todo)
3. Completar Phase 3: User Story 1
4. **Parar y validar**: correr `GET /api/catalogo/peliculas` a mano y los tests de US1
5. Esto ya es demostrable: catálogo público con marca de "sin funciones disponibles"

### Entrega incremental

1. Setup + Foundational → base lista
2. User Story 1 → validar → demo (MVP)
3. User Story 2 → validar → demo (detalle de película)
4. User Story 3 → validar → demo (búsqueda)

---

## Notes

- `[P]` = archivos distintos, sin dependencias pendientes entre sí.
- La etiqueta `[US1]`/`[US2]`/`[US3]` traza cada tarea a su historia en [spec.md](./spec.md).
- Varias tareas de US2/US3 modifican archivos creados en US1 (`PeliculaRepository.cs`, `PeliculasController.cs`, `ListarPeliculasQueryHandlerTests.cs`) — están marcadas sin `[P]` y con su dependencia explícita por eso.
- Verificar que los tests fallan antes de implementar cada tarea de implementación correspondiente.
- No se generan tareas de outbox/SignalR/Stripe/auth JWT — esta feature es de solo lectura y pública (Principio VI no aplica, Principio VII solo exige el `[AllowAnonymous]` explícito).
