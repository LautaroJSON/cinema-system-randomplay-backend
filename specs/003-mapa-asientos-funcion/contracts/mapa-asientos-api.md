# Contrato de API: Mapa de Asientos de una Función

El endpoint es de acceso público (sin autenticación, FR-014) y se marca explícitamente como anónimo,
como exige el Principio VII de la constitución.

## `GET /api/funciones/{funcionId}/asientos`

Devuelve la disposición de la Sala de la función y los asientos **disponibles** para esa función
(User Stories 1 y 2). Los asientos ocupados **no** se envían: todo asiento de la disposición que no
figure en `asientosDisponibles` de su fila se considera ocupado, y el front lo muestra como tal
(FR-005, FR-006).

`funcionId` es el `funcionId` que devuelve
`GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` (feature 002).

### 200 OK

```json
{
  "funcionId": "guid",
  "horaInicio": "2026-09-23T18:30:00-03:00",
  "peliculaId": "guid",
  "peliculaTitulo": "string",
  "sucursalId": "guid",
  "sucursalNombre": "string",
  "salaId": "guid",
  "salaNombre": "string",
  "totalAsientos": 70,
  "cantidadDisponibles": 68,
  "filas": [
    {
      "fila": "A",
      "cantidadAsientos": 8,
      "asientosDisponibles": [1, 2, 3, 4, 5, 6, 7, 8]
    },
    {
      "fila": "B",
      "cantidadAsientos": 10,
      "asientosDisponibles": [1, 2, 3, 4, 7, 8, 9, 10]
    }
  ]
}
```

En este ejemplo, B5 y B6 están ocupados porque no figuran en la lista de la fila B. Mientras no exista
la feature de Reservas, todas las listas vienen completas (FR-013).

**Reglas del body**:
- `filas` incluye **todas** las filas de la Sala, ordenadas alfabéticamente por `fila` (FR-002).
  Aparecen aunque no tengan ningún asiento disponible, en cuyo caso `asientosDisponibles` es `[]`.
- Los asientos de una fila son `1..cantidadAsientos` (FR-003).
- `asientosDisponibles` está ordenado de menor a mayor, sin repetidos, y solo contiene números entre 1
  y `cantidadAsientos` (FR-007).
- `totalAsientos` es la suma de `cantidadAsientos` de todas las filas.
- `cantidadDisponibles` es la suma de los largos de todas las listas `asientosDisponibles`. Cuando vale
  `0`, la función está agotada (FR-012).

**Cómo dibujar el mapa en el front**: para cada fila, recorrer los números de 1 a `cantidadAsientos` y
marcar cada uno como disponible si está en `asientosDisponibles`, u ocupado si no está.

### 404 Not Found

Cuando `funcionId` no existe, o la función pertenece a una película inactiva (FR-009). Sigue el mismo
criterio que los endpoints de Catálogo y Funciones: no se distingue "inexistente" de "inactiva".

### 410 Gone

Cuando la función existe pero su horario de inicio ya pasó respecto del momento de la consulta
(FR-010). Se distingue de `404` por el código de estado, sin necesidad de leer el body. Ver
[research.md](../research.md), Decisión 6.

### `funcionId` con formato inválido

Si `funcionId` no es un GUID, la restricción de ruta `{funcionId:guid}` hace que la ruta no coincida y
ASP.NET responde `404`, igual que en los endpoints existentes.
