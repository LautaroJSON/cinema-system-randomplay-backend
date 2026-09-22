# Contrato de API: Horarios por Sucursal — Funciones de Hoy

Todos los endpoints son de acceso público (sin autenticación — FR-010), marcados explícitamente como
anónimos por el Principio VII de la constitución.

## `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy`

Lista las Sucursales que tienen al menos una `Funcion` disponible hoy (no pasada) para la película
dada (User Story 1 / FR-001, FR-002).

**200 OK** — Body:
```json
[
  {
    "id": "guid",
    "nombre": "string"
  }
]
```
Devuelve `[]` cuando la película es válida (existe y está activa) pero no tiene ninguna Sucursal con
funciones hoy — el cliente muestra el mensaje de "sin funciones hoy" (FR-003/SC-005), la API no
devuelve un error en ese caso. Orden: alfabético por `nombre` (ver spec, Assumptions).

**404 Not Found** — cuando `peliculaId` no existe, o existe pero `Activa == false` (FR-012). Mismo
criterio de "no distinguir inexistente de inactiva" que usa `GET /api/catalogo/peliculas/{id}` (ver
`specs/001-catalogo-peliculas/contracts/catalogo-api.md`).

## `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy`

Lista los horarios de hoy disponibles (no pasados) para la película, en la Sucursal dada (User Story 2
/ FR-005, FR-006, FR-007, FR-008).

**200 OK** — Body:
```json
[
  {
    "funcionId": "guid",
    "horaInicio": "2026-09-21T14:30:00-03:00",
    "salaId": "guid",
    "salaNombre": "string"
  }
]
```
Devuelve `[]` cuando la película y la Sucursal son válidas (la Sucursal tuvo funciones hoy para esa
película) pero ya no queda ninguna disponible porque todas pasaron — el cliente muestra el mensaje de
"sin horarios disponibles hoy" (FR-009/SC-005). Orden: cronológico ascendente por `horaInicio`
(FR-006).

**404 Not Found** — cuando:
- `peliculaId` no existe o está inactiva (FR-012, mismo criterio que el endpoint anterior), o
- `sucursalId` no existe, o existe pero nunca tuvo ninguna `Funcion` hoy para esa película — es decir,
  no formaba parte del resultado de `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy`
  (FR-013).

No se distingue entre estos dos motivos en la respuesta (mismo `404` sin body adicional), consistente
con el patrón ya usado en `Catalogo` de no filtrar detalles internos a un cliente no autenticado.
