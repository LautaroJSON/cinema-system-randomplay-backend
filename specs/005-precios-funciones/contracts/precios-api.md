# Contrato de API: Precios de las Funciones

Esta feature **no agrega endpoints**. Agrega campos a respuestas existentes. Todos los cambios son
**aditivos**: ningún campo existente se quita, se renombra ni cambia de significado, y la autenticación
de cada endpoint no cambia (FR-007, FR-010).

## Representación de un monto (`Dinero`)

```json
{ "monto": "5500.00", "moneda": "ARS" }
```

| Campo | Tipo JSON | Reglas |
|---|---|---|
| `monto` | `string` | Siempre con exactamente 2 decimales y punto decimal, sin separador de miles (`"5500.00"`, `"7250.50"`). Nunca un número. |
| `moneda` | `string` | Código ISO 4217. En esta feature, siempre `"ARS"`. |

El cliente no debe convertir `monto` a punto flotante para hacer cálculos; para mostrarlo puede
formatearlo como quiera (FR-013).

## `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` (público)

Contrato base: [specs/002-horarios-sucursal-hoy/contracts/funciones-api.md](../../002-horarios-sucursal-hoy/contracts/funciones-api.md).
Cada horario agrega `precioEntrada` (FR-005):

```json
[
  {
    "funcionId": "guid",
    "horaInicio": "2026-10-27T21:00:00-03:00",
    "salaId": "guid",
    "salaNombre": "Sala 1",
    "precioEntrada": { "monto": "5500.00", "moneda": "ARS" }
  }
]
```

Los códigos de estado y los casos de error no cambian.

## `GET /api/funciones/{funcionId}/asientos` (público)

Contrato base: [specs/003-mapa-asientos-funcion/contracts/mapa-asientos-api.md](../../003-mapa-asientos-funcion/contracts/mapa-asientos-api.md).
La respuesta agrega `precioEntrada` (FR-006):

```json
{
  "funcionId": "guid",
  "horaInicio": "2026-10-27T21:00:00-03:00",
  "peliculaId": "guid",
  "peliculaTitulo": "string",
  "sucursalId": "guid",
  "sucursalNombre": "string",
  "salaId": "guid",
  "salaNombre": "string",
  "totalAsientos": 96,
  "cantidadDisponibles": 94,
  "filas": [ { "fila": "A", "cantidadAsientos": 12, "asientosDisponibles": [1, 2, 3] } ],
  "precioEntrada": { "monto": "5500.00", "moneda": "ARS" }
}
```

Los códigos de estado y los casos de error no cambian.

## Reservas (rol `Customer`)

Contrato base: [specs/004-reserva-asientos-funcion/contracts/reservas-api.md](../../004-reserva-asientos-funcion/contracts/reservas-api.md).
La representación de una reserva agrega `precioUnitario` y `total` (FR-010). Aplica a:

- `POST /api/funciones/{funcionId}/reservas` → `201 Created`
- `GET /api/reservas` → `200 OK` (cada elemento)
- `GET /api/reservas/{id}` → `200 OK`

```json
{
  "id": "guid",
  "estado": "PendienteDePago",
  "creadaEn": "2026-10-27T18:20:00-03:00",
  "venceEn": "2026-10-27T18:30:00-03:00",
  "canceladaEn": null,
  "asientos": [
    { "fila": "C", "numero": 5 },
    { "fila": "C", "numero": 6 },
    { "fila": "C", "numero": 7 }
  ],
  "funcion": { "funcionId": "guid", "horaInicio": "...", "peliculaId": "guid", "peliculaTitulo": "string",
               "sucursalId": "guid", "sucursalNombre": "string", "salaId": "guid", "salaNombre": "string" },
  "precioUnitario": { "monto": "5500.00", "moneda": "ARS" },
  "total": { "monto": "16500.00", "moneda": "ARS" }
}
```

| Regla | Referencia |
|---|---|
| `precioUnitario` es el precio de la función al momento de crear la reserva. | FR-008 |
| `total` = `precioUnitario` × cantidad de `asientos`, exacto. | FR-008, SC-003 |
| Ninguno de los dos cambia si después cambia el precio de la función, en ningún estado de la reserva. | FR-009, SC-004 |
| Las respuestas de error (`400`, `401`, `403`, `404`, `409`, `422`) no cambian y no llevan montos. | FR-011 |
| `POST /api/reservas/{id}/cancelacion` (`204`) no cambia. | — |
