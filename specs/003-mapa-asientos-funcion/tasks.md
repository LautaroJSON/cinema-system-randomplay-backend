---

description: "Task list for feature 003-mapa-asientos-funcion"
---

# Tasks: Mapa de Asientos de una Función

**Input**: Design documents from `/specs/003-mapa-asientos-funcion/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/mapa-asientos-api.md](./contracts/mapa-asientos-api.md), [quickstart.md](./quickstart.md)

**Tests**: Incluidos. El Principio V de la constitución trata el testing como "ciudadano de primera clase, NO NEGOCIABLE" y prohíbe validar solo con el provider InMemory de EF Core, igual que en las features 001 y 002.

**Organization**: Tareas agrupadas por historia de usuario (US1/US2, según prioridad P1/P2 de [spec.md](./spec.md)) para poder implementar y probar cada una de forma independiente.

## Path Conventions

Proyecto único .NET: `src/CinemaSystemRandomPlay.{Domain,Application,Infrastructure,Api}/`, `tests/CinemaSystemRandomPlay.{Domain,Application}.UnitTests/`, `tests/CinemaSystemRandomPlay.IntegrationTests/`.

---

## Phase 1: Setup

**Purpose**: Dependencias de paquetes necesarias para esta feature.

No hace falta ninguna tarea de Setup: EF Core, Npgsql, Testcontainers, `Microsoft.AspNetCore.Mvc.Testing`, NSubstitute y la cadena de conexión `CinemaDb` ya están en los `.csproj` y en `appsettings.Development.json` desde las features 001 y 002.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Disposición de la Sala (`FilaSala` + cambios en `Sala`), su persistencia y su seed. Las dos historias dependen de esto.

**⚠️ CRITICAL**: Ninguna historia de usuario puede empezar hasta completar esta fase.

- [X] T001 [P] Crear el Value Object `FilaSala` como `record` inmutable con `char Letra` y `int CantidadAsientos`. El constructor lanza `DomainException` si `Letra` no está entre `'A'` y `'Z'` o si `CantidadAsientos < 1`. Incluir un constructor privado sin parámetros para EF Core. Archivo: `src/CinemaSystemRandomPlay.Domain/Funciones/FilaSala.cs` (ver [data-model.md](./data-model.md), "FilaSala")
- [X] T002 [P] Test unitario de los invariantes de `FilaSala` (letra válida, letra minúscula, letra fuera de A–Z, cantidad 0 y negativa) en `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/FilaSalaTests.cs` (depende de T001)
- [X] T003 Modificar `Sala`:
  - Agregar la colección privada `List<FilaSala> _filas`, expuesta como `IReadOnlyList<FilaSala> Filas` ordenada por `Letra`.
  - Agregar la navegación de solo lectura `Sucursal? Sucursal { get; private set; }`.
  - Agregar la propiedad calculada `int TotalAsientos` (suma de `CantidadAsientos`).
  - Cambiar el constructor a `Sala(Guid id, Guid sucursalId, string nombre, IEnumerable<FilaSala> filas)`. Además de las validaciones actuales, debe lanzar `DomainException` si `filas` es nulo o vacío, o si hay letras repetidas.

  Archivo: `src/CinemaSystemRandomPlay.Domain/Funciones/Sala.cs` (depende de T001)
- [X] T004 Actualizar `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/SalaTests.cs` al nuevo constructor y agregar casos: sin filas → excepción; filas `null` → excepción; letras repetidas → excepción; `Filas` se expone ordenada aunque se pase desordenada; `TotalAsientos` suma bien con filas irregulares (depende de T003)
- [X] T005 [P] Actualizar la construcción de `Sala` en `tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/ListarHorariosDisponiblesHoyQueryHandlerTests.cs` al nuevo constructor, pasando al menos una `FilaSala` (depende de T003)
- [X] T006 Modificar `SalaEntityConfiguration`:
  - Reemplazar `HasOne<Sucursal>()` por `HasOne(s => s.Sucursal)`. Se mantienen la FK `SucursalId` y `DeleteBehavior.Restrict`, así que el esquema de esa relación no cambia.
  - Mapear `Filas` con `OwnsMany` a la tabla `SalaFilas`, usando el backing field `_filas`, FK `SalaId` y clave `(SalaId, Letra)`. `Letra` se mapea como `character(1)`.
  - Sembrar con `HasData` la disposición de las 4 Salas según la tabla de [data-model.md](./data-model.md), "Esquema":
    - `SalaCentro1Id`: A–H × 12.
    - `SalaCentro2Id`: A:8, B:10, C:12, D:12, E:14, F:14.
    - `SalaNorte1Id`: A–J × 10.
    - `SalaSur1Id`: A–E × 8.

  Archivo: `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/SalaEntityConfiguration.cs` (depende de T003; ver [research.md](./research.md), Decisiones 2, 7 y 8)
- [X] T007 Generar la migración `AddDisposicionSalas` con `dotnet ef migrations add AddDisposicionSalas --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api`. Revisarla antes de aplicarla: debe crear solo la tabla `SalaFilas` (PK compuesta, FK a `Salas` con cascade) y su seed, sin tocar las tablas existentes. Destino: `src/CinemaSystemRandomPlay.Infrastructure/Migrations/` (depende de T006) — migración `20260923201318_AddDisposicionSalas` revisada: solo `CreateTable`/`InsertData` (29 filas) de `SalaFilas`. La primera generación marcaba `Letra` como identity, así que se agregó `ValueGeneratedNever()` y se regeneró. `has-pending-model-changes`: sin cambios.
- [X] T008 Correr `dotnet build` y `dotnet test` completos para confirmar que el cambio de constructor de `Sala` y la migración no rompen nada de las features 001 y 002 (depende de T004, T005, T007) — ✅ build OK; suite completa en verde (Domain 43, Application 22, Integration 34), sin regresiones.

**Checkpoint**: La base compila, migra y pasa todos los tests existentes. Las Salas ya tienen disposición persistida.

---

## Phase 3: User Story 1 - Ver la disposición de la Sala de una función (Priority: P1) 🎯 MVP

**Goal**: Dado un `funcionId`, el Cliente ve las filas de la Sala con su cantidad de asientos, el total y los datos de contexto de la función. Se responde `404` si la función no existe o su película está inactiva, y `410` si la función ya comenzó.

**Independent Test**: `GET /api/funciones/{funcionId}/asientos` sin autenticación devuelve `200` con las filas reales de la Sala sembrada (por ejemplo, la Sala 2 de Centro con filas irregulares), `404` para un id inexistente y `410` para una función pasada, sin depender de US2.

### Tests for User Story 1 ⚠️

> Escribir estos tests primero y verificar que fallan antes de implementar.

- [X] T009 [P] [US1] Agregar a `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/FuncionTests.cs` los tests de `Funcion.YaComenzo(ahora)`: horario futuro → `false`, igual a `ahora` → `false`, pasado → `true` (mismo criterio que la feature 002) — ✅ verde
- [X] T010 [P] [US1] Test unitario de `ObtenerMapaAsientosQueryHandler` con NSubstitute (`IFuncionRepository`, `IPeliculaRepository`, `IReloj` fijo). Casos: — ✅ verde
  - Función inexistente → `FuncionNoEncontrada`.
  - Película `null` → `FuncionNoEncontrada`.
  - Película inactiva → `FuncionNoEncontrada`.
  - Función ya comenzada → `FuncionNoDisponible`.
  - Función válida → `Ok`, con filas ordenadas, `CantidadAsientos` correcta por fila, `TotalAsientos` y datos de contexto (título, sucursal, sala, hora).

  Archivo: `tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/ObtenerMapaAsientosQueryHandlerTests.cs`
- [X] T011 [P] [US1] Agregar a `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionRepositoryTests.cs` los tests de `FuncionRepository.ObtenerConSala()` contra Postgres real: carga `Sala`, `Sala.Filas` (con la disposición sembrada de la Sala 2 de Centro) y `Sala.Sucursal`; devuelve `null` para un id inexistente — ✅ verde
- [X] T012 [P] [US1] Test de integración del endpoint `GET /api/funciones/{funcionId}/asientos` vía `WebApplicationFactory` + Testcontainers. Casos: — ✅ verde (reutiliza `FuncionesApiFactory`)
  - `200` con las filas A–F irregulares de la Sala 2 de Centro y `totalAsientos: 70`.
  - `200` con los datos de contexto.
  - `404` para una función inexistente.
  - `404` para una función de película inactiva.
  - `410` para una función que empezó hace 10 minutos.

  Usar offsets en minutos, no en horas, para evitar el problema de medianoche detectado en la feature 002. Crear la factory `MapaAsientosApiFactory` siguiendo `FuncionesApiFactory`, o reutilizar esta última. Archivo: `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/MapaAsientosControllerTests.cs`

### Implementation for User Story 1

- [X] T013 [P] [US1] Agregar `public bool YaComenzo(DateTimeOffset ahora) => FechaHoraInicio < ahora;` en `src/CinemaSystemRandomPlay.Domain/Funciones/Funcion.cs`
- [X] T014 [P] [US1] Agregar `Task<Funcion?> ObtenerConSala(Guid funcionId, CancellationToken cancellationToken = default);` a `src/CinemaSystemRandomPlay.Domain/Funciones/IFuncionRepository.cs`
- [X] T015 [US1] Implementar `ObtenerConSala` en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/FuncionRepository.cs` con `Include(f => f.Sala).ThenInclude(s => s!.Sucursal)` y `FirstOrDefaultAsync`. `Filas` se carga sola por ser owned (depende de T014, T006)
- [X] T016 [P] [US1] Crear los DTOs en `src/CinemaSystemRandomPlay.Application/Funciones/Dtos/MapaAsientosDto.cs`:
  - `MapaAsientosDto(Guid FuncionId, DateTimeOffset HoraInicio, Guid PeliculaId, string PeliculaTitulo, Guid SucursalId, string SucursalNombre, Guid SalaId, string SalaNombre, int TotalAsientos, IReadOnlyList<FilaMapaDto> Filas)`
  - `FilaMapaDto(string Fila, int CantidadAsientos)`

  US2 les agrega los campos de disponibilidad.
- [X] T017 [US1] Implementar `src/CinemaSystemRandomPlay.Application/Funciones/Queries/ObtenerMapaAsientosQuery.cs` con:
  - `ObtenerMapaAsientosQuery(Guid FuncionId)`.
  - El resultado explícito `ObtenerMapaAsientosResultado`, con un enum `EstadoMapaAsientos { Ok, FuncionNoEncontrada, FuncionNoDisponible }` y un `MapaAsientosDto? Mapa`, más factories estáticas por caso.
  - `ObtenerMapaAsientosQueryHandler`, con los pasos 1–3 del caso de uso de [data-model.md](./data-model.md) y mapeo manual de `Funcion`/`Sala`/`Pelicula` a `MapaAsientosDto`.

  (depende de T013, T014, T016)
- [X] T018 [P] [US1] Crear el contrato HTTP en `src/CinemaSystemRandomPlay.Api/Modulos/Funciones/Contracts/MapaAsientosResponse.cs`: `MapaAsientosResponse` y `FilaMapaResponse`, con `DesdeDto` explícito, siguiendo el patrón de `HorarioFuncionResponse` (depende de T016)
- [X] T019 [US1] Crear `src/CinemaSystemRandomPlay.Api/Modulos/Funciones/MapaAsientosController.cs`:
  - `[ApiController]` con `[Route("api/funciones/{funcionId:guid}/asientos")]`.
  - Acción `[HttpGet]` marcada `[AllowAnonymous]` explícitamente.
  - Traduce el resultado sin lógica de negocio: `Ok` → `200` con `MapaAsientosResponse`, `FuncionNoEncontrada` → `NotFound()`, `FuncionNoDisponible` → `StatusCode(StatusCodes.Status410Gone)`.

  (depende de T017, T018)
- [X] T020 [US1] Registrar `ObtenerMapaAsientosQueryHandler` como `Scoped` en la sección `// Funciones` de `src/CinemaSystemRandomPlay.Api/Program.cs` (depende de T017)

**Checkpoint**: User Story 1 funciona y se puede probar de punta a punta (MVP): el front ya puede dibujar la sala de una función.

---

## Phase 4: User Story 2 - Ver qué asientos están disponibles (Priority: P2)

**Goal**: La respuesta incluye, por fila, los números de asiento disponibles y la cantidad total de disponibles. Lo que no figura se considera ocupado. La ocupación sale del puerto `IOcupacionAsientos`, que por ahora no ocupa nada.

**Independent Test**: Con el stub, `GET /api/funciones/{funcionId}/asientos` devuelve cada fila con `asientosDisponibles` de 1 a `cantidadAsientos` y `cantidadDisponibles == totalAsientos`. El caso con asientos ocupados se prueba en el handler con un `IOcupacionAsientos` falso.

### Tests for User Story 2 ⚠️

- [X] T021 [P] [US2] Test unitario del Value Object `Asiento`: fila válida; fila fuera de A–Z → excepción; número < 1 → excepción; igualdad por valor (`new Asiento('C', 5) == new Asiento('C', 5)`) y uso como clave de `HashSet`. Archivo: `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/AsientoTests.cs`
- [X] T022 [P] [US2] Agregar a `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/SalaTests.cs` los tests de `Sala.AsientosDisponibles(ocupados)`: — ✅ verde
  - Sin ocupados → todas las filas completas.
  - Con C5 y C6 ocupados → no aparecen en la fila C y el resto queda intacto.
  - Fila entera ocupada → la fila aparece con lista vacía.
  - Ocupado que no pertenece a la Sala (fila Z, o número mayor a la cantidad de la fila) → se ignora.
  - Resultado ordenado por fila y número.
- [X] T023 [US2] Extender `tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/ObtenerMapaAsientosQueryHandlerTests.cs` con un `IOcupacionAsientos` falso (NSubstitute). Casos: — ✅ verde (Application 22/22)
  - Sin ocupados → `CantidadDisponibles == TotalAsientos`.
  - Con ocupados → faltan de su fila y `CantidadDisponibles` baja.
  - Todos ocupados → `CantidadDisponibles == 0` y las filas siguen presentes.
  - Ajustar la construcción del handler en los tests de T010.

  (depende de T010, mismo archivo)
- [X] T024 [US2] Extender `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/MapaAsientosControllerTests.cs`: en la respuesta `200`, cada fila trae `asientosDisponibles` igual a `[1..cantidadAsientos]`, y `cantidadDisponibles` es igual a `totalAsientos` y a la suma de los largos de las listas (depende de T012, mismo archivo) — ✅ verde

### Implementation for User Story 2

- [X] T025 [P] [US2] Crear el Value Object `Asiento` como `record` con `char Fila` e `int Numero`. El constructor lanza `DomainException` si `Fila` no está entre `'A'` y `'Z'` o si `Numero < 1`. Archivo: `src/CinemaSystemRandomPlay.Domain/Funciones/Asiento.cs`
- [X] T026 [US2] Agregar a `src/CinemaSystemRandomPlay.Domain/Funciones/Sala.cs` el método `IReadOnlyList<FilaDisponibilidad> AsientosDisponibles(IEnumerable<Asiento> ocupados)`:
  - `FilaDisponibilidad` es un `record` de dominio con `char Letra`, `int CantidadAsientos` e `IReadOnlyList<int> NumerosDisponibles`. Puede ir en el mismo archivo o en `FilaDisponibilidad.cs`.
  - Recorre las filas en orden. En cada una devuelve los números de `1..CantidadAsientos` que no están ocupados.
  - Ignora los ocupados que no pertenecen a la Sala.

  (depende de T025; ver [research.md](./research.md), Decisión 4)
- [X] T027 [P] [US2] Crear el puerto `IOcupacionAsientos` con `Task<IReadOnlySet<Asiento>> ObtenerOcupados(Guid funcionId, CancellationToken cancellationToken = default);` en `src/CinemaSystemRandomPlay.Application/Funciones/Ports/IOcupacionAsientos.cs` (depende de T025)
- [X] T028 [P] [US2] Crear el stub `SinAsientosOcupados : IOcupacionAsientos`, que siempre devuelve un conjunto vacío. Su XML doc comment debe explicar que es temporal hasta la feature de Reservas, igual que `NingunaFuncionDisponibleChecker`. Archivo: `src/CinemaSystemRandomPlay.Infrastructure/Funciones/SinAsientosOcupados.cs` (depende de T027; ver [research.md](./research.md), Decisión 3)
- [X] T029 [US2] Extender los DTOs en `src/CinemaSystemRandomPlay.Application/Funciones/Dtos/MapaAsientosDto.cs`: agregar `int CantidadDisponibles` a `MapaAsientosDto` e `IReadOnlyList<int> AsientosDisponibles` a `FilaMapaDto` (depende de T016, mismo archivo)
- [X] T030 [US2] Extender `ObtenerMapaAsientosQueryHandler` en `src/CinemaSystemRandomPlay.Application/Funciones/Queries/ObtenerMapaAsientosQuery.cs`:
  - Inyectar `IOcupacionAsientos`.
  - Paso 4 del caso de uso: `ObtenerOcupados` → `sala.AsientosDisponibles(ocupados)`.
  - Mapear `FilaDisponibilidad` a `FilaMapaDto`.
  - Calcular `CantidadDisponibles` como la suma de los largos de las listas.

  (depende de T017, T026, T027, T029)
- [X] T031 [US2] Extender `MapaAsientosResponse`/`FilaMapaResponse` con `cantidadDisponibles` y `asientosDisponibles`, y su mapeo en `DesdeDto`, en `src/CinemaSystemRandomPlay.Api/Modulos/Funciones/Contracts/MapaAsientosResponse.cs` (depende de T018, T029)
- [X] T032 [US2] Registrar `IOcupacionAsientos` → `SinAsientosOcupados` como `Scoped` en `src/CinemaSystemRandomPlay.Api/Program.cs` (depende de T020, T028, mismo archivo)

**Checkpoint**: User Stories 1 y 2 completas. La respuesta cumple el contrato completo de [contracts/mapa-asientos-api.md](./contracts/mapa-asientos-api.md).

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T033 Correr `dotnet test` completo (Domain, Application e Integration) y confirmar que no hay regresiones en Catálogo ni en Horarios — ✅ 99/99 (Domain 43, Application 22, Integration 34)
- [X] T034 [P] Ejecutar a mano los escenarios 1–6 de [quickstart.md](./quickstart.md) contra el Postgres local: Sala uniforme, Sala irregular, todos disponibles, contexto, `404` y `410` — ✅ contra `cinema-postgres` (docker compose) + `dotnet run`: migración aplicada (29 filas en `SalaFilas`); Sala 1 Centro 8×12 = 96 completa; Sala 2 Centro irregular = 70 completa; contexto correcto y `funcionId` obtenido desde `horarios-hoy`; id inexistente → 404; función de hace 1 h → 410 (fila de prueba borrada después); id no-GUID → 404.
- [X] T035 [P] Verificar con grep que la acción de `MapaAsientosController` está marcada `[AllowAnonymous]` (Principio VII) y que el controller no contiene lógica de negocio (Principio VIII)
- [X] T036 [P] Agregar el request de ejemplo `GET {{host}}/api/funciones/{funcionId}/asientos` en `src/CinemaSystemRandomPlay.Api/CinemaSystemRandomPlay.Api.http`
- [X] T037 [P] Documentar el nuevo endpoint en la sección de endpoints de `README.md`, incluyendo la regla para el front: "lo que no está en `asientosDisponibles` está ocupado"

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin tareas.
- **Foundational (Phase 2)**: bloquea las dos historias. T008 es la puerta: todo compila y los tests existentes siguen en verde.
- **US1 (Phase 3)**: depende de Foundational.
- **US2 (Phase 4)**: depende de Foundational y, en la práctica, de US1, porque extiende el handler, los DTOs, el response, el `Program.cs` y los archivos de test que crea US1. La lógica de dominio de US2 (T021, T022, T025, T026) sí puede hacerse en paralelo con US1.
- **Polish (Phase 5)**: depende de las historias que se quieran entregar.

### Dentro de cada fase

- Tests primero, verificando que fallan, después la implementación.
- Domain → Application → Infrastructure → Api → DI.

### Parallel Opportunities

- Foundational: T001 y T002 en paralelo con el análisis de T003; T004 y T005 en paralelo una vez hecho T003.
- US1: los tests T009–T012 son archivos distintos y van en paralelo. T013, T014 y T016 también van en paralelo; T018 en cuanto termine T016.
- US2: T021, T022 y T025 en paralelo, y también se pueden adelantar mientras se hace US1. T027 y T028 van en paralelo después de T025.
- Polish: T034–T037 en paralelo.

---

## Parallel Example: User Story 1

```bash
# Tests de US1 (archivos distintos):
Task: "Tests de Funcion.YaComenzo en tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/FuncionTests.cs"
Task: "Tests de ObtenerMapaAsientosQueryHandler en tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/ObtenerMapaAsientosQueryHandlerTests.cs"
Task: "Tests de FuncionRepository.ObtenerConSala en tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionRepositoryTests.cs"
Task: "Tests del endpoint en tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/MapaAsientosControllerTests.cs"

# Piezas independientes de US1:
Task: "Funcion.YaComenzo en src/CinemaSystemRandomPlay.Domain/Funciones/Funcion.cs"
Task: "IFuncionRepository.ObtenerConSala en src/CinemaSystemRandomPlay.Domain/Funciones/IFuncionRepository.cs"
Task: "MapaAsientosDto en src/CinemaSystemRandomPlay.Application/Funciones/Dtos/MapaAsientosDto.cs"
```

## Parallel Example: User Story 2

```bash
# Dominio de US2 (se puede adelantar durante US1):
Task: "Asiento en src/CinemaSystemRandomPlay.Domain/Funciones/Asiento.cs"
Task: "Tests de Asiento en tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/AsientoTests.cs"

# Después de Asiento:
Task: "IOcupacionAsientos en src/CinemaSystemRandomPlay.Application/Funciones/Ports/IOcupacionAsientos.cs"
Task: "SinAsientosOcupados en src/CinemaSystemRandomPlay.Infrastructure/Funciones/SinAsientosOcupados.cs"
```

---

## Implementation Strategy

### MVP First (solo User Story 1)

1. Phase 2: Foundational. Validar con T008 que no se rompió nada.
2. Phase 3: User Story 1.
3. **Parar y validar**: `GET /api/funciones/{funcionId}/asientos` devuelve la sala y responde `404`/`410` según corresponda.
4. Ya es demostrable: el front puede dibujar la sala de la función elegida.

### Entrega incremental

1. Foundational → las Salas tienen disposición persistida.
2. US1 → mapa de la sala con contexto.
3. US2 → asientos disponibles por fila. El contrato queda completo y listo para que la feature de Reservas enchufe su implementación de `IOcupacionAsientos`.

---

## Notes

- `[P]` = archivos distintos, sin dependencias pendientes entre sí.
- La etiqueta `[US1]`/`[US2]` vincula cada tarea con su historia en [spec.md](./spec.md).
- Varias tareas de US2 modifican archivos creados en US1 (`ObtenerMapaAsientosQuery.cs`, `MapaAsientosDto.cs`, `MapaAsientosResponse.cs`, `Program.cs` y los tests del handler y del controller). Por eso no llevan `[P]` y declaran su dependencia.
- Los tests de Application fijan "ahora" con un `IReloj` falso. Los de integración contra el reloj real usan offsets en minutos.
- No se generan tareas de outbox, SignalR, Stripe ni auth JWT, porque la feature es de solo lectura y pública. Tampoco de selección o reserva de asientos (FR-015) ni de ABM de disposición (FR-016).
- Quedan fuera de alcance, como deuda documentada en [research.md](./research.md): el stub `NingunaFuncionDisponibleChecker` del Catálogo y la configuración de CORS.
