# Research: Catálogo de Películas

No quedaron `NEEDS CLARIFICATION` en el Technical Context del plan (el stack está fijado por la constitución y las decisiones de persistencia/testing ya se habían tomado en la sesión de diseño previa). Este documento registra las decisiones de diseño que sí requerían resolución.

## Decisión 1: Cómo resolver "sin funciones disponibles" sin que exista todavía la feature Funciones

**Decision**: Se define un puerto `IFuncionAvailabilityChecker` en `Application/Catalogo/Ports`, con una única operación (ej. `TieneFuncionesProgramadas(peliculaId)`). Por ahora, la implementación en `Infrastructure` es un **stub explícito** que siempre devuelve `false` (ninguna película tiene funciones todavía).

**Rationale**: La spec (Assumptions) ya documenta esta regla: *"mientras esa feature no exista, se asume que ninguna película tiene funciones y todas se muestran con esa marca"*. Modelarlo como puerto permite que, cuando se implemente la feature `Funciones`, solo haga falta reemplazar la implementación de Infrastructure (una consulta real contra la tabla de Funciones) sin tocar Domain/Application/Api — coherente con el Principio IV (Inversión de Dependencias).

**Alternatives considered**:
- Crear ya una tabla `Funciones` mínima solo para este chequeo → rechazado: es scope creep de una feature que no está diseñada todavía (fecha/hora, Sala, Sucursal, etc.), y la spec ya autoriza el default "ninguna tiene funciones".
- Hardcodear `false` directo en el use case de Application (sin puerto) → rechazado: acopla Application a una decisión que en realidad es de disponibilidad de datos, y obliga a tocar Application (no solo Infrastructure) el día que la feature Funciones exista.

## Decisión 2: Controllers vs Minimal APIs para los endpoints de Catálogo

**Decision**: Controllers (`PeliculasController`), consistente con el `Api` ya scaffolded (`AddControllers()`/`MapControllers()` en `Program.cs`).

**Rationale**: El proyecto ya está en modo Controllers por defecto; introducir Minimal APIs para un solo módulo generaría dos estilos mezclados sin necesidad. Es un cambio de sintaxis del borde HTTP, no afecta la arquitectura hexagonal de abajo.

**Alternatives considered**: Minimal APIs — descartado por consistencia con el scaffold existente, no por una razón técnica de esta feature puntual.

## Decisión 3: Cómo implementar la búsqueda por título (User Story 3)

**Decision**: Filtro `WHERE Titulo ILIKE '%{termino}%'` a nivel de EF Core/Postgres (búsqueda case-insensitive por coincidencia parcial), expuesto como query string opcional (`?titulo=`) sobre el mismo endpoint de listado, no como un endpoint separado.

**Rationale**: Es la forma más simple que cumple FR-006 sin introducir infraestructura de búsqueda adicional (ej. full-text search, Elasticsearch) que el volumen de datos (decenas de películas, según Assumptions) no justifica.

**Alternatives considered**: Búsqueda full-text de Postgres (`tsvector`) — descartada por sobre-ingeniería para el volumen esperado; se puede migrar a eso después sin cambiar el contrato del endpoint si hiciera falta.

## Decisión 4: Alcance de testing de integración

**Decision**: Se usa `Testcontainers.PostgreSql` para los tests de integración de esta feature (repositorio y endpoints), aunque no es el escenario de concurrencia crítica que la constitución exige explícitamente (eso aplica a la reserva de asientos, todavía no construida).

**Rationale**: Coherente con el principio general de la constitución de "no confiar únicamente en el provider InMemory de EF Core" y con la restricción de plataforma de usar el mismo motor en dev/test/prod. Además valida que el mapeo EF Core (`PeliculaEntityConfiguration`) y las queries (filtro por título, join/checker de disponibilidad) funcionan contra el motor real.

**Alternatives considered**: EF Core InMemory provider — descartado explícitamente por la constitución para no naturalizar su uso en el proyecto, incluso en features sin concurrencia crítica.
