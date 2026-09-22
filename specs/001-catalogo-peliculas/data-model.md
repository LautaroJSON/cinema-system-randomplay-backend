# Data Model: Catálogo de Películas

## Pelicula (entidad de Domain)

Representa una película del catálogo. Es una entidad ya persistida para efectos de esta feature (su creación/edición es responsabilidad de una feature de administración futura, fuera de alcance).

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | Identidad del agregado. |
| `Titulo` | `string` | No vacío/whitespace, longitud acotada. No es clave única de negocio (pueden existir dos películas activas con el mismo título — Edge Case de la spec). |
| `DuracionMinutos` | `int` | Positivo (> 0). |
| `Clasificacion` | `Clasificacion` (VO/enum) | Valor cerrado, ej. `ATP`, `Mas13`, `Mas16`, `Mas18`. |
| `Sinopsis` | `string` | Puede ser vacía; se muestra solo en el detalle, no en el listado. |
| `Activa` | `bool` | Solo las películas con `Activa == true` son visibles en el Catálogo (FR-002). |

**Invariantes de construcción** (para cuando la feature de administración exista — esta feature no crea `Pelicula`s, pero el tipo debe sostenerlas para no ser un modelo anémico):
- El constructor lanza `DomainException` si `Titulo` es vacío/whitespace.
- El constructor lanza `DomainException` si `DuracionMinutos <= 0`.

## Relación de solo lectura con Funcion (no se modela como entidad en esta feature)

Esta feature **no** define la entidad `Funcion` (pertenece a una feature futura). Solo necesita, para cada `Pelicula`, un booleano derivado: *¿tiene al menos una función programada en alguna Sucursal?* Se obtiene a través del puerto `IFuncionAvailabilityChecker` (ver [research.md](./research.md), Decisión 1), no mediante una relación de EF Core real todavía.

## DTOs (Application → Api, borde explícito)

### `PeliculaListItemDto`
| Campo | Origen |
|---|---|
| `Id` | `Pelicula.Id` |
| `Titulo` | `Pelicula.Titulo` |
| `DuracionMinutos` | `Pelicula.DuracionMinutos` |
| `Clasificacion` | `Pelicula.Clasificacion` (como string) |
| `SinFuncionesDisponibles` | `!IFuncionAvailabilityChecker.TieneFuncionesProgramadas(Pelicula.Id)` |

### `PeliculaDetalleDto`
Todos los campos de `PeliculaListItemDto` más:
| Campo | Origen |
|---|---|
| `Sinopsis` | `Pelicula.Sinopsis` |

## Estado / transiciones

Esta feature no muta estado (es de solo lectura). `Pelicula.Activa` puede cambiar por acción de otra feature (administración de catálogo) mientras el usuario navega — ver Acceptance Scenario 2 de User Story 2 (detalle de una película desactivada mientras se estaba viendo): al recargar el detalle, si `Activa == false`, el caso de uso debe responder "no disponible" (ej. 404) en vez de devolver datos obsoletos.
