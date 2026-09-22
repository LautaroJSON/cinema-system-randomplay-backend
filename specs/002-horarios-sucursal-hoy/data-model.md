# Data Model: Horarios por Sucursal — Funciones de Hoy

## Sucursal (aggregate root, Domain/Funciones)

Representa una ubicación física del cine. Dato de referencia sembrado (FR-011); esta feature no lo
crea ni edita.

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | Identidad del agregado. |
| `Nombre` | `string` | No vacío/whitespace, longitud acotada (ej. 150). |

**Invariantes de construcción**: el constructor lanza `DomainException` si `Nombre` es vacío/whitespace.

## Sala (aggregate root, Domain/Funciones)

Representa una sala dentro de una `Sucursal` donde se proyecta una `Funcion`. Dato de referencia
sembrado (FR-011).

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | Identidad del agregado. |
| `SucursalId` | `Guid` | FK a `Sucursal`. Requerido (`!= Guid.Empty`). |
| `Nombre` | `string` | No vacío/whitespace, longitud acotada (ej. 50). Ej. "Sala 1". |

**Invariantes de construcción**: el constructor lanza `DomainException` si `Nombre` es
vacío/whitespace o si `SucursalId == Guid.Empty`.

## Funcion (aggregate root, Domain/Funciones)

Representa una proyección programada de una `Pelicula` en una `Sala`, en una fecha y hora
determinadas. Es la entidad central de esta feature — se consulta de solo lectura, no se crea ni edita
(FR-011).

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | Identidad del agregado. |
| `PeliculaId` | `Guid` | FK a `Pelicula` (módulo `Catalogo`, entidad ya existente). Requerido. |
| `SalaId` | `Guid` | FK a `Sala`. Requerido. |
| `FechaHoraInicio` | `DateTimeOffset` | Momento en que empieza la función. Requerido. |
| `Sala` | `Sala?` | Navegación de solo lectura, poblada por `Infrastructure` vía `Include` cuando se necesita el nombre de la Sala (ver [research.md](./research.md), Decisión 4). No es parte de las invariantes de construcción. |

**Invariantes de construcción**: el constructor lanza `DomainException` si `PeliculaId == Guid.Empty`
o `SalaId == Guid.Empty`.

**"Disponible hoy"** no es un campo persistido — es un criterio de consulta calculado en el momento:
`FechaHoraInicio.Date == hoy` (según `IReloj`) **y** `FechaHoraInicio >= ahora` (según `IReloj`). Ver
[research.md](./research.md), Decisión 1.

## Pelicula (referencia de solo lectura, módulo `Catalogo` — no se modifica en esta feature)

Esta feature no agrega ni cambia atributos de `Pelicula`. Solo la usa como punto de partida (su `Id`
ya elegido por el Cliente) y para el chequeo de existencia/estado activo (FR-012), reutilizando
`IPeliculaRepository.ObtenerPorId` ya existente del módulo `Catalogo`.

## Relaciones

```text
Pelicula (Catalogo, 1) ──< (N) Funcion (Funciones) >── (1) Sala (Funciones) >── (1) Sucursal (Funciones)
```

- Una `Pelicula` puede tener muchas `Funcion`es (en cualquier `Sucursal`/`Sala`/fecha).
- Una `Sala` pertenece a una única `Sucursal`; una `Sucursal` puede tener muchas `Sala`s.
- Una `Funcion` pertenece a una única `Sala` (y transitivamente a una única `Sucursal`).

## DTOs (Application → Api, borde explícito)

### `SucursalConFuncionesHoyDto`

| Campo | Origen |
|---|---|
| `Id` | `Sucursal.Id` |
| `Nombre` | `Sucursal.Nombre` |

Producido por `ListarSucursalesConFuncionesHoyQueryHandler`. `null` (la colección completa) significa
"película no encontrada/inactiva" (FR-012, 404); una lista vacía significa "película válida, sin
funciones hoy" (FR-003, 200 con `[]`).

### `HorarioFuncionDto`

| Campo | Origen |
|---|---|
| `FuncionId` | `Funcion.Id` |
| `HoraInicio` | `Funcion.FechaHoraInicio` |
| `SalaId` | `Funcion.SalaId` |
| `SalaNombre` | `Funcion.Sala.Nombre` |

Producido por `ListarHorariosDisponiblesHoyQueryHandler`. `null` significa "película no encontrada, o
Sucursal no válida para esa película" (FR-012/FR-013, 404); una lista vacía significa "Sucursal válida,
pero sin horarios disponibles hoy" (FR-009, 200 con `[]`).

## Estado / transiciones

Esta feature no muta estado (solo lectura). El único "estado" relevante es temporal, no persistido: a
medida que avanza `IReloj.Ahora`, una `Funcion` que estaba disponible pasa a no estarlo sin que ninguna
fila cambie — es el criterio de consulta (ver arriba) el que la excluye, no una actualización de datos.
