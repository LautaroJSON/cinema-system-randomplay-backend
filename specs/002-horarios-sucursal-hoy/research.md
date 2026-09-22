# Research: Horarios por Sucursal — Funciones de Hoy

No quedaron `NEEDS CLARIFICATION` en el Technical Context del plan (el stack está fijado por la
constitución y ya se resolvieron en `/speckit-clarify`). Este documento registra las decisiones de
diseño que sí requerían resolución antes de Phase 1.

## Decisión 1: Introducir un puerto `IReloj` para "hoy"/"ahora"

**Decision**: Se define `IReloj` en `Domain/Compartido` (`DateTimeOffset Ahora { get; }`), implementado
en `Infrastructure/Compartido/RelojSistema.cs` (`DateTimeOffset.Now`). Los query handlers de esta
feature reciben `IReloj` por DI y lo usan para calcular `ahora` (filtrar funciones ya pasadas) y `hoy`
(la fecha del día actual), en vez de llamar a `DateTime.Now`/`DateTimeOffset.Now` directamente.

**Rationale**: El Principio IV de la constitución exige que "proveedor de fecha/hora" se consuma vía
interfaz — esta es la primera feature del proyecto cuya lógica depende del tiempo actual, así que el
puerto no existía todavía. Sin él, los handlers y sus tests unitarios quedarían acoplados a la hora
real del reloj del sistema, haciendo no determinísticos los escenarios de "función ya pasada" (User
Story 2, Acceptance Scenario 2) — un test unitario no podría fijar "ahora" de forma confiable.

**Alternatives considered**:
- Usar `DateTimeOffset.Now` directo en los handlers → rechazado: viola el Principio IV explícitamente
  y hace los tests de "función ya pasada"/"todas pasaron" no determinísticos o frágiles (dependientes
  de la hora real en que corre el test).
- `TimeProvider` (abstracción nativa de .NET) → considerado, pero se prefiere `IReloj` propio para
  mantener consistencia con el resto de los puertos del proyecto (nombrados en español, ej.
  `IPeliculaRepository`, `IFuncionAvailabilityChecker`) y para no atarse a los matices de zona
  horaria de `TimeProvider` sin necesidad real todavía.

## Decisión 2: Cómo sembrar `Sucursal`/`Sala`/`Funcion` sin que queden desactualizadas

**Decision**: `Sucursal` y `Sala` (datos de referencia estáticos, sin fecha) se siembran con
`HasData` en la migración de EF Core, igual que cualquier dato de catálogo fijo. `Funcion` (que sí
depende de "hoy") **no** se siembra con fechas fijas en la migración — se documenta en
[quickstart.md](./quickstart.md) como inserts manuales usando expresiones relativas a la fecha actual
(`CURRENT_DATE`, `CURRENT_DATE + 1`), igual que hoy se hace para `Pelicula` (ver
`specs/001-catalogo-peliculas/quickstart.md`, "no hay todavía un endpoint de administración").

**Rationale**: La spec no pide construir infraestructura de seeding (sería scope creep sobre una
feature de "puramente consulta/lectura", y el alcance explícitamente excluye alta/administración de
estas tres entidades — FR-011). Sembrar `Funcion` con timestamps fijos en una migración haría que la
demo dejara de tener "funciones hoy" al día siguiente de aplicar la migración, rompiendo el propósito
mismo de la feature. Usar expresiones relativas a la fecha del momento en que se ejecuta el insert
evita ese problema sin construir un seeder en background.

**Alternatives considered**:
- `HasData` con timestamps fijos para `Funcion` → rechazado: se desactualiza al día siguiente,
  inutilizando la demo/QA manual de la feature sin re-aplicar datos.
- Un `IHostedService`/seeder en background que recalcule `Funcion`s "de hoy" en cada arranque →
  rechazado: es funcionalidad de administración/generación de datos no pedida por la spec (que
  explícitamente dice que el alta de `Funcion` queda fuera de alcance); se puede introducir en una
  futura feature de administración si hace falta.

## Decisión 3: Cómo resolver "Sucursal no válida para esta película" (FR-013) sin un repositorio de `Sucursal` separado

**Decision**: `IFuncionRepository` expone `ExisteFuncionHoy(peliculaId, sucursalId, hoy)` — verdadero
si existe **al menos una** `Funcion` hoy para esa combinación, pasada o no. El handler de horarios la
usa así: si es `false` → la Sucursal no es válida para esa película hoy (FR-013, 404); si es `true`
pero `ListarHorariosDisponibles` devuelve una lista vacía → la Sucursal es válida pero sus funciones
de hoy ya pasaron (FR-009, 200 con `[]`).

**Rationale**: Evita crear un repositorio de `Sucursal` solo para chequear existencia — la pregunta de
negocio real ("¿esta Sucursal tiene o tuvo programación hoy para esta película?") ya está contenida en
la tabla `Funciones`, que es la fuente de verdad para esta feature. Si el `sucursalId` no existe en
absoluto, tampoco hay ninguna `Funcion` que lo referencie, así que el mismo chequeo cubre ambos casos
(id inexistente e id existente pero sin relación) sin lógica adicional.

**Alternatives considered**:
- `ISucursalRepository.ObtenerPorId` + chequeo de funciones por separado → rechazado: dos idas a la
  base de datos y dos conceptos ("¿existe la Sucursal?" y "¿tiene funciones hoy?") para responder una
  sola pregunta de negocio que ya cubre el enfoque de una sola query.

## Decisión 4: `Funcion.Sala` como navegación de solo lectura

**Decision**: `Funcion` (Domain) expone una propiedad de navegación `Sala? Sala { get; private set; }`,
poblada por `Infrastructure` vía `Include(f => f.Sala)` cuando el repositorio necesita el nombre de la
Sala para armar `HorarioFuncionDto`. No es una propiedad requerida por ninguna invariante de dominio,
solo una comodidad de lectura para este módulo de consulta.

**Rationale**: `Sala` es un aggregate simple (nombre, sin lógica propia) dentro del mismo módulo
`Funciones`; exponerla como navegación evita una segunda consulta manual o un tipo de proyección
intermedio solo para llevar el nombre de la Sala desde Infrastructure hasta el DTO de Application.

**Alternatives considered**:
- Proyección SQL directa a un tipo anónimo/`record` de Infrastructure → rechazado: la app ya tiene el
  patrón establecido (Principio III) de que el repositorio devuelve entidades de Domain y el mapeo a
  DTO ocurre en Application; introducir un tipo de proyección paralelo sería inconsistente con
  `PeliculaRepository`.

## Decisión 5: Alcance de testing de integración

**Decision**: Igual que en `001-catalogo-peliculas` (Decisión 4 de su research.md), se usa
`Testcontainers.PostgreSql` para los tests de integración de `FuncionRepository` y de los endpoints
del nuevo controller, aunque no hay concurrencia crítica en esta feature de solo lectura.

**Rationale**: Consistencia con el Principio V ("no confiar únicamente en el provider InMemory") y con
la restricción de plataforma de usar el mismo motor en dev/test/prod. Además es el único modo
confiable de validar `EF.Functions`/comparaciones de fecha (`CURRENT_DATE`, filtros por
`FechaHoraInicio`) contra el comportamiento real de Postgres.

**Alternatives considered**: EF Core InMemory provider — descartado por la misma razón que en la
feature anterior.

## Nota fuera de alcance: el stub de `Catalogo`

`NingunaFuncionDisponibleChecker` (Infrastructure/Catalogo, feature `001`) sigue devolviendo `false`
siempre para `IFuncionAvailabilityChecker`, aunque esta feature ya crea la tabla real de `Funciones`.
Actualizarlo para consultar `Funciones` de verdad **no** está pedido por esta spec (que es puramente
sobre Sucursales/horarios de hoy, no sobre el marcado "sin funciones disponibles" del Catálogo) y se
deja documentado aquí como deuda técnica conocida para una decisión explícita en una futura feature,
en vez de tocarlo de forma implícita como efecto colateral.
