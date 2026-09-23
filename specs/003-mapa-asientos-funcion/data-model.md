# Data Model: Mapa de Asientos de una Función

## Asiento (Value Object, nuevo, Domain/Funciones)

Posición física dentro de una Sala. Inmutable, con igualdad por valor.

| Campo | Tipo | Reglas |
|---|---|---|
| `Fila` | `char` | Letra mayúscula `A`–`Z`. |
| `Numero` | `int` | Mayor o igual a 1. |

**Invariantes de construcción**: lanza `DomainException` si `Fila` no está entre `A` y `Z`, o si
`Numero < 1`. No valida contra una Sala concreta: que el asiento exista en una Sala lo decide `Sala`.

**Igualdad**: dos `Asiento` con misma `Fila` y `Numero` son iguales (se implementa como `record`). Es
el tipo que usa `IOcupacionAsientos` y que usará `Reserva` en la feature 004.

## FilaSala (Value Object, nuevo, parte del agregado Sala)

Una fila de la disposición física de una Sala.

| Campo | Tipo | Reglas |
|---|---|---|
| `Letra` | `char` | Letra mayúscula `A`–`Z`. |
| `CantidadAsientos` | `int` | Mayor o igual a 1. Los asientos de la fila son `1..CantidadAsientos` (FR-003). |

**Invariantes de construcción**: lanza `DomainException` si `Letra` no está entre `A` y `Z`, o si
`CantidadAsientos < 1`.

**Persistencia**: colección owned de `Sala` en la tabla `SalaFilas` (`SalaId`, `Letra`,
`CantidadAsientos`), con clave primaria `(SalaId, Letra)`. Ver [research.md](./research.md),
Decisión 2.

## Sala (aggregate root, Domain/Funciones, se modifica)

Campos existentes (`Id`, `SucursalId`, `Nombre`) sin cambios. Se agregan:

| Campo | Tipo | Reglas |
|---|---|---|
| `Filas` | `IReadOnlyList<FilaSala>` | Al menos una fila. Letras sin repetir (FR-004). Se expone ordenada por `Letra` (FR-002). |
| `Sucursal` | `Sucursal?` | Navegación de solo lectura, cargada por Infrastructure con `Include` cuando se necesita el nombre. No forma parte de las invariantes. Ver research.md, Decisión 7. |

**Invariantes de construcción** (se agregan a las existentes): el constructor pasa a recibir las
filas y lanza `DomainException` si la colección es nula o vacía, o si dos filas tienen la misma
`Letra`.

**Comportamiento nuevo**:
- `TotalAsientos`: suma de `CantidadAsientos` de todas las filas.
- `AsientosDisponibles(IEnumerable<Asiento> ocupados)`: por cada fila, en orden alfabético, devuelve la
  `Letra`, la `CantidadAsientos` y los números de asiento que **no** están en `ocupados`, ordenados de
  menor a mayor. Los ocupados que no pertenecen a la Sala se ignoran (FR-007). Ver research.md,
  Decisión 4.

**Impacto en código existente**: el constructor cambia de firma, así que se ajustan los tests que
construyen `Sala` (`SalaTests`, `ListarHorariosDisponiblesHoyQueryHandlerTests`). El `HasData` de
`SalaEntityConfiguration` sigue funcionando porque usa objetos anónimos y no el constructor.

## Funcion (aggregate root, Domain/Funciones, se modifica)

Sin cambios de campos ni de esquema. Se agrega:

- `YaComenzo(DateTimeOffset ahora)`: `true` si `FechaHoraInicio < ahora`. Es el mismo criterio que
  usa la feature 002 para considerar disponible una función (`FechaHoraInicio >= ahora`), ahora
  explícito en el dominio. Decide el `410 Gone` (FR-010).

## Sucursal y Pelicula (sin cambios)

Solo aportan datos de contexto: `Sucursal.Nombre` (vía `Sala.Sucursal`) y `Pelicula.Titulo` (vía
`IPeliculaRepository.ObtenerPorId`, que también cubre el caso de película inactiva de FR-009).

## Puertos

| Puerto | Capa | Cambio |
|---|---|---|
| `IFuncionRepository` | Domain/Funciones | Nuevo método `ObtenerConSala(Guid funcionId)`: devuelve la `Funcion` con `Sala`, `Sala.Filas` y `Sala.Sucursal` cargadas, o `null` si no existe. |
| `IOcupacionAsientos` | Application/Funciones/Ports | **Nuevo.** `ObtenerOcupados(Guid funcionId)` devuelve un `IReadOnlySet<Asiento>`. En esta feature lo implementa `SinAsientosOcupados` (siempre vacío). Ver research.md, Decisión 3. |
| `IPeliculaRepository` | Domain/Catalogo | Sin cambios; se reutiliza `ObtenerPorId`. |
| `IReloj` | Domain/Compartido | Sin cambios; se reutiliza `Ahora`. |

## Caso de uso: ObtenerMapaAsientosQuery

Entrada: `FuncionId`. Salida: un resultado con tres casos (research.md, Decisión 6).

1. `IFuncionRepository.ObtenerConSala(funcionId)`. Si es `null` → **FuncionNoEncontrada**.
2. `IPeliculaRepository.ObtenerPorId(funcion.PeliculaId)`. Si es `null` o no está activa →
   **FuncionNoEncontrada**.
3. `funcion.YaComenzo(reloj.Ahora)`. Si es `true` → **FuncionNoDisponible**.
4. `IOcupacionAsientos.ObtenerOcupados(funcionId)` → `sala.AsientosDisponibles(ocupados)` → **Ok**
   con `MapaAsientosDto`.

`MapaAsientosDto` contiene: `FuncionId`, `HoraInicio`, `PeliculaId`, `PeliculaTitulo`, `SucursalId`,
`SucursalNombre`, `SalaId`, `SalaNombre`, `TotalAsientos`, `CantidadDisponibles` y `Filas` (lista de
`FilaMapaDto`: `Fila`, `CantidadAsientos`, `AsientosDisponibles`). El mapeo es manual y explícito
(Principio III).

## Relaciones

```text
Pelicula (Catalogo, 1) ──< (N) Funcion >── (1) Sala >── (1) Sucursal
                                              │
                                              └──< (N) FilaSala   (owned, tabla SalaFilas)

Asiento: Value Object derivado de FilaSala (no se persiste en esta feature)
```

## Esquema: migración `AddDisposicionSalas`

- Nueva tabla `SalaFilas`: `SalaId uuid` (FK a `Salas`, cascade), `Letra character(1)`,
  `CantidadAsientos integer`. PK `(SalaId, Letra)`.
- `HasData` con la disposición de las 4 Salas existentes (research.md, Decisión 8):

| Sala | Disposición | Total |
|---|---|---|
| Centro — Sala 1 | A–H, 12 asientos cada una | 96 |
| Centro — Sala 2 | A:8, B:10, C:12, D:12, E:14, F:14 (filas irregulares) | 70 |
| Norte — Sala 1 | A–J, 10 asientos cada una | 100 |
| Sur — Sala 1 | A–E, 8 asientos cada una | 40 |
