# Contrato de API: Reserva de Asientos de una Función

## Autenticación y autorización

Todos los endpoints de este documento exigen un token JWT bearer válido emitido por el proveedor OIDC
configurado, con el rol `Customer` (FR-021):

```http
Authorization: Bearer <token>
```

| Situación | Respuesta |
|---|---|
| Sin header, token mal formado, vencido, con firma o emisor inválidos | `401 Unauthorized` (con `WWW-Authenticate: Bearer ...`) |
| Token válido sin el rol `Customer` | `403 Forbidden` |

El Cliente es el claim `sub` del token. Ningún endpoint acepta un id de Cliente en la ruta, la query o
el body (FR-023).

Los endpoints públicos existentes (`/api/peliculas/...`, `/api/funciones/peliculas/...`,
`GET /api/funciones/{funcionId}/asientos`) **no cambian**: siguen sin requerir token (FR-024). Un
endpoint nuevo sin política declarada responde `401` por la política de fallback (research.md,
Decisión 8).

## Errores

Los errores de negocio se responden como `application/problem+json` (RFC 9457) con una extensión
`codigo` estable. El front debe decidir por `status` + `codigo`, no por `title` ni `detail`.

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Hay asientos que ya no están disponibles.",
  "status": 409,
  "codigo": "asientos-no-disponibles",
  "asientos": [ { "fila": "C", "numero": 5 } ]
}
```

## Representación de una reserva (`Reserva`)

```json
{
  "id": "guid",
  "estado": "PendienteDePago",
  "creadaEn": "2026-10-27T18:20:00-03:00",
  "venceEn": "2026-10-27T18:30:00-03:00",
  "canceladaEn": null,
  "asientos": [
    { "fila": "C", "numero": 5 },
    { "fila": "C", "numero": 6 }
  ],
  "funcion": {
    "funcionId": "guid",
    "horaInicio": "2026-10-27T21:00:00-03:00",
    "peliculaId": "guid",
    "peliculaTitulo": "string",
    "sucursalId": "guid",
    "sucursalNombre": "string",
    "salaId": "guid",
    "salaNombre": "string"
  }
}
```

- `estado`: `PendienteDePago` | `Expirada` | `Cancelada`. Es el estado **efectivo** al momento de la
  respuesta: una reserva vencida se informa como `Expirada` aunque nadie la haya tocado (FR-011).
- `venceEn`: lo que ocurra primero entre `creadaEn` + 10 minutos y `funcion.horaInicio` (FR-002). El
  front usa este valor para mostrar el contador de tiempo.
- `asientos`: ordenados por fila y número.
- `canceladaEn`: solo cuando `estado == "Cancelada"`.

## `POST /api/funciones/{funcionId}/reservas`

Crea una reserva pendiente de pago (User Story 1).

### Request

```json
{
  "asientos": [
    { "fila": "C", "numero": 5 },
    { "fila": "C", "numero": 6 }
  ]
}
```

- `asientos`: de 1 a 10 elementos, sin repetidos.
- `fila`: una sola letra mayúscula `A`–`Z`.
- `numero`: entero ≥ 1.

### 201 Created

`Location: /api/reservas/{id}`. Body: una `Reserva` con `estado: "PendienteDePago"`.

### Errores

| Status | `codigo` | Cuándo | Extensiones |
|---|---|---|---|
| `400` | `pedido-invalido` | Lista vacía o ausente, más de 10 asientos, asientos repetidos, fila o número con formato inválido (FR-008, FR-009). | `errors`: diccionario campo → mensajes |
| `404` | `funcion-no-encontrada` | La función no existe o es de una película inactiva (FR-010). | — |
| `410` | `funcion-no-disponible` | El horario de inicio de la función ya pasó (FR-010). | — |
| `422` | `asientos-inexistentes` | Algún asiento no existe en la disposición de la Sala (FR-007). | `asientos`: los inexistentes |
| `409` | `asientos-no-disponibles` | Algún asiento está retenido por una reserva vigente, de cualquier Cliente (FR-005, FR-006). | `asientos`: los no disponibles |
| `409` | `limite-asientos-superado` | Con este pedido el Cliente superaría 10 asientos retenidos en la función (FR-008). | `asientosRestantes`: cuántos puede pedir todavía (0 o más) |

En todos los casos de error **no se reserva ningún asiento** (FR-004). Si hay varios problemas a la
vez, se informa el primero en este orden: `400` → `404` → `410` → `422` → `409 asientos-no-disponibles`
→ `409 limite-asientos-superado`.

Si `funcionId` no es un GUID, la restricción de ruta no coincide y la respuesta es `404`, igual que en
los endpoints existentes.

## `GET /api/reservas`

Lista las reservas del Cliente autenticado (User Story 3), de la más reciente a la más antigua por
`creadaEn`. Incluye todos los estados.

### 200 OK

```json
[ { "...": "Reserva" } ]
```

Si el Cliente no tiene reservas, devuelve `[]`. No hay paginación en esta feature.

## `GET /api/reservas/{reservaId}`

### 200 OK

Una `Reserva`.

### 404 Not Found — `reserva-no-encontrada`

La reserva no existe **o pertenece a otro Cliente** (FR-017). Las dos situaciones son
indistinguibles.

## `POST /api/reservas/{reservaId}/cancelacion`

Cancela una reserva propia pendiente y vigente (User Story 4). Sin body.

### 200 OK

La `Reserva` con `estado: "Cancelada"` y `canceladaEn` completo. Sus asientos quedan disponibles de
inmediato en el mapa y para otros pedidos (FR-012).

### Errores

| Status | `codigo` | Cuándo | Extensiones |
|---|---|---|---|
| `404` | `reserva-no-encontrada` | No existe o es de otro Cliente (FR-017). | — |
| `409` | `reserva-no-cancelable` | La reserva está `Expirada` o `Cancelada` (FR-013). | `estadoActual` |

## Cambio en `GET /api/funciones/{funcionId}/asientos` (feature 003)

El contrato **no cambia** ([../../003-mapa-asientos-funcion/contracts/mapa-asientos-api.md](../../003-mapa-asientos-funcion/contracts/mapa-asientos-api.md)).
Lo único que cambia es el contenido: `asientosDisponibles` y `cantidadDisponibles` excluyen los
asientos de reservas `PendienteDePago` vigentes de esa función (FR-018 a FR-020). Sigue siendo
público.
