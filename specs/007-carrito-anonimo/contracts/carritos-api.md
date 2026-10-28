# Contrato de API: Carrito Anónimo

Reemplaza los endpoints de reservas de las features 004 y 006 (`POST /api/funciones/{funcionId}/reservas`,
`GET /api/reservas`, `GET /api/reservas/{id}`, `POST /api/reservas/{id}/cancelacion`) y los de verificación de
email de la 006 (`POST /api/verificaciones-email`, `POST /api/verificaciones-email/confirmacion`), que
**dejan de existir**.

## Modalidades de acceso

| Endpoint | Modalidad |
|---|---|
| `POST /api/funciones/{funcionId}/carritos` | **Pública** (`[AllowAnonymous]`), con límite por origen y desafío anti-bots opcional |
| `GET /api/carritos/{carritoId}` | **Portador del carrito** (política `PortadorDeCarrito`) |
| `PUT /api/carritos/{carritoId}/asientos` | Portador del carrito |
| `POST /api/carritos/{carritoId}/abandono` | Portador del carrito |
| `PUT /api/carritos/{carritoId}/email-entrega` | Portador del carrito |

Las consultas públicas existentes (catálogo, horarios y `GET /api/funciones/{funcionId}/asientos`) no cambian.
Un endpoint sin modalidad declarada responde `401` (fallback cerrado) y se considera un defecto.

### Token del carrito

Lo entrega solo la respuesta de creación. Se envía así:

```http
Authorization: Bearer <token>
```

| Situación | Respuesta |
|---|---|
| Sin header, o con un token mal formado, vencido, con otra firma u otro emisor, o con un token del personal | `401 Unauthorized`, idéntico para cualquier `carritoId`, exista o no |
| Token válido de **otro** carrito | `404 carrito-no-encontrado`, idéntico a un carrito inexistente |

## Errores

`application/problem+json` (RFC 9457) con la extensión `codigo` estable, igual que en la 004. El cliente
decide por `status` + `codigo`.

| Status | `codigo` | Cuándo | Extensiones |
|---|---|---|---|
| 400 | `pedido-invalido` | Selección vacía, más del máximo, asientos repetidos o mal formados; email inválido | `errors` |
| 403 | `desafio-rechazado` | El desafío anti-bots rechazó la creación | — |
| 404 | `funcion-no-encontrada` | La función no existe o su película está inactiva (crear) | — |
| 404 | `carrito-no-encontrado` | El carrito no existe o el token es de otro carrito | — |
| 409 | `asientos-no-disponibles` | Asientos retenidos por otro carrito activo | `asientos` |
| 409 | `funcion-sin-cupo` | Se superaría el tope de asientos retenidos de la función | — |
| 409 | `carrito-no-activo` | Reemplazar, abandonar o indicar el email en un carrito expirado o abandonado | `estadoActual` |
| 410 | `funcion-no-disponible` | La función ya comenzó (crear) | — |
| 422 | `asientos-inexistentes` | Asientos que no existen en la sala | `asientos` |
| 429 | `demasiados-pedidos` | Se superó el límite de creación por origen | header `Retry-After` (segundos) |

Se elimina `limite-asientos-superado`: no hay tope por comprador. El máximo por carrito es `pedido-invalido`,
como en la 004. Ningún error informa asientos, carritos ni datos de otros compradores, salvo la lista de
asientos pedidos que no están disponibles, igual que en la 004.

## `POST /api/funciones/{funcionId}/carritos`

Crea un carrito activo con la selección. Cada llamada crea un carrito nuevo: para cambiar la selección se
usa `PUT /api/carritos/{carritoId}/asientos`.

```json
{
  "asientos": [ { "fila": "C", "numero": 5 }, { "fila": "C", "numero": 6 } ],
  "desafio": null
}
```

`desafio` es opcional. Sin un desafío configurado se ignora.

**201 Created** (`Location: /api/carritos/{id}`):

```json
{
  "carrito": {
    "id": "6f1c…",
    "estado": "Activo",
    "creadoEn": "2026-10-28T20:00:00+00:00",
    "venceEn": "2026-10-28T20:05:00+00:00",
    "abandonadoEn": null,
    "asientos": [ { "fila": "C", "numero": 5 }, { "fila": "C", "numero": 6 } ],
    "funcion": {
      "funcionId": "…", "horaInicio": "…", "peliculaId": "…", "peliculaTitulo": "Interstellar",
      "sucursalId": "…", "sucursalNombre": "Centro", "salaId": "…", "salaNombre": "Sala 1"
    },
    "precioUnitario": { "monto": 5500.00, "moneda": "ARS" },
    "total": { "monto": 11000.00, "moneda": "ARS" },
    "emailEntrega": null
  },
  "token": "eyJhbGciOiJIUzI1NiIs…",
  "tokenVenceEn": "2026-10-28T21:00:00+00:00"
}
```

Errores: `400`, `403`, `404 funcion-no-encontrada`, `409 asientos-no-disponibles` y `funcion-sin-cupo`,
`410`, `422` y `429`.

## `GET /api/carritos/{carritoId}`

**200 OK**: el objeto `carrito` de arriba, con el `estado` efectivo (`Activo`, `Expirado` o `Abandonado`).
Funciona en cualquier estado mientras el token esté vigente.

Errores: `401` y `404 carrito-no-encontrado`.

## `PUT /api/carritos/{carritoId}/asientos`

Reemplaza la selección completa, de forma atómica y todo o nada. No modifica `venceEn`.

```json
{ "asientos": [ { "fila": "D", "numero": 1 }, { "fila": "D", "numero": 2 } ] }
```

**200 OK**: el carrito con la nueva selección. Si el pedido se rechaza, el carrito queda **sin cambios**.

Errores: `400`, `401`, `404 carrito-no-encontrado`, `409 asientos-no-disponibles`, `funcion-sin-cupo` y
`carrito-no-activo`, y `422`. Los asientos que ya tiene el propio carrito cuentan como disponibles.

## `POST /api/carritos/{carritoId}/abandono`

Sin body. **200 OK**: el carrito en estado `Abandonado`, con `abandonadoEn`. Los asientos quedan libres de
inmediato.

Errores: `401`, `404 carrito-no-encontrado` y `409 carrito-no-activo` (con `estadoActual`).

## `PUT /api/carritos/{carritoId}/email-entrega`

```json
{ "email": " Ana@Example.com " }
```

**200 OK**: el carrito con `"emailEntrega": "ana@example.com"`. No cambia `estado` ni `venceEn`, y se puede
volver a llamar para corregirlo. No se verifica el email ni se envía nada.

Errores: `400 pedido-invalido` (email vacío o sin formato válido), `401`, `404 carrito-no-encontrado` y
`409 carrito-no-activo`.

## Mapa de asientos (sin cambios de forma)

`GET /api/funciones/{funcionId}/asientos` excluye de los disponibles los asientos de los carritos activos.
Los de carritos expirados o abandonados vuelven a figurar como disponibles.

## Configuración

| Clave | Por defecto | Uso |
|---|---|---|
| `Carritos:MinutosRetencion` | 5 | Retención desde la creación (FR-005) |
| `Carritos:MaximoAsientosPorCarrito` | 10 | FR-004 |
| `Carritos:PorcentajeMaximoRetenidoPorFuncion` | 50 | Tope global (FR-022) |
| `Carritos:CreacionesPorOrigen` | 10 | Límite por origen (FR-021) |
| `Carritos:VentanaCreacionMinutos` | 10 | Ventana del límite por origen |
| `Invitados:ClaveFirma` | — (obligatoria, ≥ 32 bytes) | Firma del token |
| `Invitados:TokenMinutosVigencia` | 60 | Vida del token |
