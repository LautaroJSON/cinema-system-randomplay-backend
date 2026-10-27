---

description: "Task list for Precios de las Funciones"
---

# Tasks: Precios de las Funciones

**Input**: Design documents from `/specs/005-precios-funciones/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/precios-api.md, quickstart.md

**Tests**: Incluidos según la "Estrategia de tests" de plan.md (Principio V de la constitución): unit de Domain para `Dinero`, `Funcion`, `Reserva`/`ReservadorDeAsientos`; integración con Postgres real para persistencia y contratos. No se agregan tests de handlers ni de mapeos triviales.

**Organization**: Tareas agrupadas por user story. Rutas relativas a la raíz del repo.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede correr en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: US1, US2, US3 (según spec.md)

## Path Conventions

Monolito modular de 4 proyectos: `src/CinemaSystemRandomPlay.{Domain,Application,Infrastructure,Api}/` y `tests/CinemaSystemRandomPlay.{Domain.UnitTests,Application.UnitTests,IntegrationTests}/`.

---

## Phase 1: Setup

**Purpose**: Verificar el punto de partida

- [X] T001 Ejecutar `dotnet build` y `dotnet test` en la raíz del repo y confirmar que la base (features 002 a 004) compila y pasa antes de tocar nada

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Value Object `Dinero`, precio en `Funcion`, campo `PrecioUnitario` en `Reserva`, persistencia y migración. Ninguna user story puede empezar hasta terminar esta fase.

**⚠️ CRITICAL**: Los cambios de constructor de `Funcion` y de `Reserva.Crear` rompen la compilación de tests y sembrado hasta corregir todos los callers.

- [X] T002 [P] Crear enum `Moneda` (único valor `ARS`) en `src/CinemaSystemRandomPlay.Domain/Compartido/Moneda.cs`
- [X] T003 Crear `sealed record Dinero` en `src/CinemaSystemRandomPlay.Domain/Compartido/Dinero.cs`: `decimal Monto` + `Moneda Moneda`; `DomainException` si monto negativo o con más de 2 decimales (`decimal.Round(monto, 2) != monto`); normaliza escala a 2; fábrica `Dinero.Ars(decimal)`; `Multiplicar(int cantidad)` que rechaza cantidad negativa y es exacto (depende de T002)
- [X] T004 [P] Crear `DineroTests` en `tests/CinemaSystemRandomPlay.Domain.UnitTests/Compartido/DineroTests.cs`: negativo, más de 2 decimales (`10.005m`), cero válido, normalización de escala (`5500` y `5500.000` → `5500.00`), igualdad por valor, `Multiplicar` exacto (`7250.50 × 4 = 29002.00`) y con cantidad negativa (depende de T003)
- [X] T005 Agregar `Dinero PrecioEntrada` como último parámetro obligatorio del constructor de `Funcion` en `src/CinemaSystemRandomPlay.Domain/Funciones/Funcion.cs`: `DomainException` si es `null`, monto cero o moneda distinta de `ARS`. Sin método para cambiarlo (depende de T003)
- [X] T006 [P] Agregar casos de precio (null, cero, válido, moneda) y actualizar el constructor en `tests/CinemaSystemRandomPlay.Domain.UnitTests/Funciones/FuncionTests.cs` (depende de T005)
- [X] T007 Agregar `Dinero PrecioUnitario` a `Reserva` en `src/CinemaSystemRandomPlay.Domain/Reservas/Reserva.cs`: parámetro de `Reserva.Crear` que rechaza `null`, propiedad persistida sin mutador, y propiedad calculada `Total => PrecioUnitario.Multiplicar(Asientos.Count)` (depende de T003)
- [X] T008 Configurar `ComplexProperty(PrecioEntrada)` con `Monto` `numeric(12,2)` y `Moneda` como `varchar(3)` vía `HasConversion<string>()`, ambos NOT NULL, en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Funciones/FuncionEntityConfiguration.cs` (depende de T005)
- [X] T009 Configurar `ComplexProperty(PrecioUnitario)` igual que T008 e ignorar `Total` (no persistido) en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Reservas/ReservaEntityConfiguration.cs` (depende de T007)
- [X] T010 Generar la migración `AddPrecios` con `dotnet ef migrations add AddPrecios --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api` y editarla según research.md Decisión 6: columnas de `Funciones` con default temporal `5500.00`/`'ARS'` y luego quitar los defaults; columnas de `Reservas` nullable; `UPDATE "Reservas" r SET ... FROM "Funciones" f WHERE r."FuncionId" = f."Id"`; luego `NOT NULL` (depende de T008, T009)
- [X] T011 [P] Actualizar `src/CinemaSystemRandomPlay.Infrastructure/Persistence/DevDataSeeder.cs` para sembrar Centro Sala 1 a `5500.00`, Centro Sala 2 a `7250.50` y Norte Sala 1 a `6000.00` con `Dinero.Ars(...)` (depende de T005)
- [X] T012 [P] Crear `DineroDto(decimal Monto, string Moneda)` con `DesdeDominio(Dinero)` en `src/CinemaSystemRandomPlay.Application/Compartido/Dtos/DineroDto.cs` (depende de T003)
- [X] T013 [P] Crear `DineroResponse(string Monto, string Moneda)` con `DesdeDto(DineroDto)` en `src/CinemaSystemRandomPlay.Api/Compartido/DineroResponse.cs`; `Monto.ToString("0.00", CultureInfo.InvariantCulture)` y `Moneda` como texto (depende de T012)
- [X] T014 Corregir los callers rotos de los constructores de `Funcion` y `Reserva.Crear` en tests (pasar `Dinero.Ars(5500m)` o equivalente): `tests/CinemaSystemRandomPlay.Domain.UnitTests/Reservas/ReservaFactory.cs`, `ReservaTests.cs`, `ReservadorDeAsientosTests.cs`; `tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/*Tests.cs` y `Reservas/ReservasTestSupport.cs`; `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionRepositoryTests.cs`, `Reservas/ReservasApiFactory.cs`, `Reservas/ReservaConcurrenteTests.cs` (depende de T005, T007)
- [X] T015 Pasar `funcion.PrecioEntrada` a `Reserva.Crear` en `src/CinemaSystemRandomPlay.Domain/Reservas/ReservadorDeAsientos.cs` (la firma de `Reservar` no cambia) (depende de T007)
- [X] T016 Ejecutar `dotnet build` y `dotnet test`; todos los tests existentes (incluido `ReservaConcurrenteTests`) deben pasar con aserciones sin modificar (depende de T014, T015, T010, T011, T013)

**Checkpoint**: Dominio, persistencia y migración listos; las user stories pueden empezar.

---

## Phase 3: User Story 1 - Ver el precio de la entrada antes de reservar (Priority: P1) 🎯 MVP

**Goal**: Horarios de hoy y mapa de asientos informan `precioEntrada`, solo aditivo y público.

**Independent Test**: Consultar sin token los horarios de hoy y el mapa de asientos de funciones sembradas con precios distintos y verificar `precioEntrada` (`"5500.00"`, `"7250.50"`, `"6000.00"`, `"ARS"`) más todos los campos previos intactos.

### Tests for User Story 1

- [X] T017 [P] [US1] Agregar caso en `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/FuncionesControllerTests.cs`: dos funciones con `5500.00` y `7250.50`; cada horario informa `precioEntrada` con `monto` string de 2 decimales y `moneda` `"ARS"`; el monto con centavos vuelve exacto desde Postgres
- [X] T018 [P] [US1] Agregar caso en `tests/CinemaSystemRandomPlay.IntegrationTests/Funciones/MapaAsientosControllerTests.cs`: el mapa informa `precioEntrada` (`"6000.00"`/`"ARS"`) sin autenticación y los campos existentes siguen presentes

### Implementation for User Story 1

- [X] T019 [P] [US1] Agregar `DineroDto PrecioEntrada` al final de `HorarioFuncionDto` y `MapaAsientosDto` en `src/CinemaSystemRandomPlay.Application/Funciones/Dtos/HorarioFuncionDto.cs` y `MapaAsientosDto.cs`
- [X] T020 [US1] Mapear `DineroDto.DesdeDominio(funcion.PrecioEntrada)` en `src/CinemaSystemRandomPlay.Application/Funciones/Queries/ListarHorariosDisponiblesHoyQuery.cs` y `ObtenerMapaAsientosQuery.cs` (depende de T019)
- [X] T021 [P] [US1] Agregar `DineroResponse PrecioEntrada` al final de `HorarioFuncionResponse` y `MapaAsientosResponse` y mapearlo en su `DesdeDto` en `src/CinemaSystemRandomPlay.Api/Modulos/Funciones/Contracts/HorarioFuncionResponse.cs` y `MapaAsientosResponse.cs` (depende de T019)
- [X] T022 [US1] Ajustar las construcciones de los DTOs modificados en `tests/CinemaSystemRandomPlay.Application.UnitTests/Funciones/*Tests.cs` si no compilan, y correr `dotnet test` (depende de T020, T021)

**Checkpoint**: US1 funcional y verificable por sí sola (MVP).

---

## Phase 4: User Story 2 - La reserva congela el precio y calcula su total (Priority: P2)

**Goal**: Al crear una reserva se congela el precio unitario y se informa el total; el precio no cambia si cambia el de la función.

**Independent Test**: Reservar 3 asientos de una función a `5500.00` → `precioUnitario 5500.00`, `total 16500.00`; `UPDATE` del precio de la función; la reserva conserva los montos y una reserva nueva usa el precio nuevo.

### Tests for User Story 2

- [X] T023 [P] [US2] Agregar casos en `tests/CinemaSystemRandomPlay.Domain.UnitTests/Reservas/ReservaTests.cs` y `ReservadorDeAsientosTests.cs`: la reserva copia `funcion.PrecioEntrada`, `Total = precio × cantidad`, `Crear` rechaza precio `null`, el precio queda inmutable
- [X] T024 [P] [US2] Agregar casos en `tests/CinemaSystemRandomPlay.IntegrationTests/Reservas/ReservasControllerTests.cs`: crear 3 asientos a `5500.00` → `201` con `precioUnitario` y `total` (`"16500.00"`); 1 asiento a `7250.50`; precio congelado tras `UPDATE` directo del precio de la función y reserva nueva con el precio nuevo; un rechazo (`409`) mantiene el cuerpo previo sin montos

### Implementation for User Story 2

- [X] T025 [P] [US2] Agregar `Dinero PrecioUnitario` y `Dinero Total` al final del record `ReservaCreada` en `src/CinemaSystemRandomPlay.Domain/Reservas/Eventos/ReservaCreada.cs` (Decisión 8)
- [X] T026 [US2] Hacer que `Reserva.Crear` / `ReservadorDeAsientos` emitan `ReservaCreada` con `PrecioUnitario` y `Total` en `src/CinemaSystemRandomPlay.Domain/Reservas/Reserva.cs` (depende de T025)
- [X] T027 [P] [US2] Agregar `DineroDto PrecioUnitario` y `DineroDto Total` al final de `ReservaDto` en `src/CinemaSystemRandomPlay.Application/Reservas/Dtos/ReservaDto.cs` (con el mapeo desde el dominio)
- [X] T028 [US2] Mapear montos y evento en `src/CinemaSystemRandomPlay.Application/Reservas/Commands/ReservarAsientosCommand.cs` (depende de T026, T027)
- [X] T029 [US2] Agregar `PrecioUnitario` y `Total` al final de `ReservaResponse` y mapearlos con `DineroResponse.DesdeDto` en `src/CinemaSystemRandomPlay.Api/Modulos/Reservas/Contracts/ReservaResponse.cs` (depende de T027)
- [X] T030 [US2] Ajustar tests de Application que construyan `ReservaDto`/`ReservaCreada` en `tests/CinemaSystemRandomPlay.Application.UnitTests/Reservas/` y correr `dotnet test`, incluido `ReservaConcurrenteTests` (depende de T028, T029)

**Checkpoint**: US1 y US2 funcionan; la reserva congela el precio.

---

## Phase 5: User Story 3 - Ver los montos en mis reservas (Priority: P3)

**Goal**: Listar y consultar reservas informan `precioUnitario` y `total` en cualquier estado.

**Independent Test**: Crear reservas de 2 asientos a `5500.00` y 4 a `7250.50`; listar y consultar → `11000.00` y `29002.00`; cancelada/expirada conserva montos.

### Tests for User Story 3

- [X] T031 [P] [US3] Agregar casos en `tests/CinemaSystemRandomPlay.IntegrationTests/Reservas/ReservasControllerTests.cs`: listar informa montos correctos por reserva (`"11000.00"`, `"29002.00"`); consultar puntual informa montos; una reserva cancelada o expirada conserva los montos; campos previos intactos

### Implementation for User Story 3

- [X] T032 [P] [US3] Agregar `Dinero PrecioUnitario` a `ReservaLecturaDto` en `src/CinemaSystemRandomPlay.Application/Reservas/Ports/IConsultaReservas.cs` y hacer que `ReservaDto.DesdeLectura` calcule el total con la cantidad de asientos (`PrecioUnitario.Multiplicar(cantidad)`)
- [X] T033 [US3] Leer `PrecioUnitario` en `src/CinemaSystemRandomPlay.Infrastructure/Persistence/Reservas/ConsultaReservas.cs` y poblarlo en `ReservaLecturaDto` (depende de T032)
- [X] T034 [US3] Correr `dotnet test` y confirmar que T031 pasa y que los tests de 004 siguen verdes (depende de T033)

**Checkpoint**: Las tres user stories funcionan de forma independiente.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T035 Aplicar la migración a la base de desarrollo (`dotnet ef database update ...`) y ejecutar los pasos de verificación de `specs/005-precios-funciones/quickstart.md`, incluida la consulta de backfill (FR-012)
- [X] T036 [P] Actualizar `README.md` con la feature de precios (precio en horarios/mapa y montos en reservas) siguiendo el estilo de las secciones previas
- [X] T037 Revisar que no quede `float`/`double` en el manejo de montos y que ningún controller tenga lógica de montos nueva (Principio VIII); correr `dotnet test` completo una última vez

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** (bloquea todo) → **US1 (3)**, **US2 (4)**, **US3 (5)** → **Polish (6)**
- US1, US2 y US3 dependen solo de la fase 2 y se pueden hacer en paralelo, salvo que US3 (T032) reutiliza el mapeo de montos de `ReservaDto` de US2 (T027); hacer US2 antes de US3.

### Within Foundational

- T002 → T003 → (T004, T005, T007, T012) → T006, T008, T009 → T010; T011, T014, T015 tras T005/T007; T013 tras T012; T016 al final.

### Parallel Opportunities

- Fase 2: T002; luego T004, T012 en paralelo con T005; T006, T011, T013 [P].
- US1: T017, T018, T019, T021 en paralelo (archivos distintos).
- US2: T023, T024, T025, T027 en paralelo.

## Parallel Example: User Story 1

```bash
Task: "Caso de precio en horarios en tests/.../Funciones/FuncionesControllerTests.cs"
Task: "Caso de precio en mapa en tests/.../Funciones/MapaAsientosControllerTests.cs"
Task: "Agregar PrecioEntrada a HorarioFuncionDto y MapaAsientosDto en Application"
```

## Implementation Strategy

### MVP First (US1)

1. Setup → Foundational → US1.
2. **Validar**: horarios y mapa públicos informan el precio con el formato `{ "monto": "5500.00", "moneda": "ARS" }`.

### Incremental Delivery

1. Foundational listo → US1 (precio visible) → US2 (reserva congela precio y total) → US3 (montos en mis reservas) → Polish.
2. Cada story suma sin romper campos existentes (cambios solo aditivos).

## Notes

- Nunca punto flotante, ni en código ni en JSON: el monto sale siempre como string con 2 decimales.
- El test de concurrencia de reservas es obligatorio (constitución) y debe seguir pasando.
- Hacer commit por tarea o grupo lógico.
