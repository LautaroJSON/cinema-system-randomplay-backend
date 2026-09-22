# Contrato de API: Catálogo de Películas

Todos los endpoints son de acceso público (sin autenticación — FR-007), marcados explícitamente como anónimos por el Principio VII de la constitución.

## `GET /api/catalogo/peliculas`

Lista las películas activas. Soporta filtro opcional por título (User Story 3 / FR-006).

**Query params**:
- `titulo` (opcional, string): coincidencia parcial, case-insensitive, sobre `Pelicula.Titulo`.

**200 OK** — Body:
```json
[
  {
    "id": "guid",
    "titulo": "string",
    "duracionMinutos": 120,
    "clasificacion": "Mas13",
    "sinFuncionesDisponibles": true
  }
]
```
Devuelve `[]` (lista vacía) cuando no hay películas activas o ninguna coincide con el filtro — el cliente es responsable de mostrar el mensaje de "sin resultados" (FR-005/SC-004), la API no devuelve un error en ese caso.

## `GET /api/catalogo/peliculas/{id}`

Detalle completo de una película activa.

**200 OK** — Body:
```json
{
  "id": "guid",
  "titulo": "string",
  "duracionMinutos": 120,
  "clasificacion": "Mas13",
  "sinopsis": "string",
  "sinFuncionesDisponibles": true
}
```

**404 Not Found** — cuando el `id` no existe, o cuando existe pero `Activa == false` (una película desactivada no es distinguible de una inexistente para el borde público — evita filtrar el estado interno de administración a un cliente no autenticado).
