# Contrato de API: Pago del Carrito

Extiende el contrato de la 007 ([carritos-api.md](../../007-carrito-anonimo/contracts/carritos-api.md)). Los endpoints
de la 007 siguen igual salvo lo indicado acá.

## Modalidades de acceso

| Endpoint | Modalidad |
|---|---|
| `POST /api/carritos/{carritoId}/pago` | **Portador del carrito** (política `PortadorDeCarrito`) |
| `GET /api/carritos/{carritoId}` | Portador del carrito (sin cambios; ahora verifica pagos en `Pagando`) |
| `POST /api/pagos/mercadopago/avisos` | **Pública** (`[AllowAnonymous]`), protegida por la firma `x-signature` |
| `POST /api/dev/pagos-simulados` | **Pública**, solo existe en Development y Testing con `MercadoPago:Modo=Simulado` |
| `GET /api/dev/pagos-simulados/{preferenciaId}` | Ídem: página informativa a la que apunta la `urlPago` simulada |

## Errores nuevos

`application/problem+json` con la extensión `codigo`, igual que en la 007.

| Status | `codigo` | Cuándo | Extensiones |
|---|---|---|---|
| 409 | `email-entrega-faltante` | Iniciar el pago sin email de entrega | — |
| 409 | `carrito-en-pago` | Reemplazar asientos, abandonar o cambiar el email de un carrito en `Pagando` | — |
| 409 | `carrito-modificado` | La selección cambió mientras se creaba el pago; reintentar | — |
| 503 | `pago-no-disponible` | Mercado Pago no respondió al crear el pago; el carrito no cambió | — |

`409 carrito-no-activo` (con `estadoActual`) ahora también incluye `Pagado` como estado posible al iniciar el pago.

## Cambios en el objeto `carrito`

```json
{
  "id": "6f1c…",
  "estado": "Pagando",
  "creadoEn": "2026-11-16T20:00:00Z",
  "venceEn": "2026-11-16T20:06:00Z",
  "abandonadoEn": null,
  "asientos": [ { "fila": "C", "numero": 5 }, { "fila": "C", "numero": 6 } ],
  "funcion": { "…": "igual que en la 007" },
  "precioUnitario": { "monto": "4500.00", "moneda": "ARS" },
  "total": { "monto": "9000.00", "moneda": "ARS" },
  "emailEntrega": "ana@example.com",
  "pago": {
    "urlPago": "https://sandbox.mercadopago.com.ar/checkout/v1/redirect?pref_id=…",
    "iniciadoEn": "2026-11-16T20:03:00Z",
    "pagadoEn": null
  },
  "entradas": []
}
```

- `estado`: `Activo`, `Pagando`, `Pagado`, `Expirado` o `Abandonado`.
- `pago`: `null` mientras no se inició el pago. `urlPago` se mantiene en `Pagando` para volver a la página de pago.
- `entradas`: vacío salvo en `Pagado`. Cada entrada:

```json
{
  "codigo": "K7M2Q-X9PDA",
  "pelicula": "El viaje de Chihiro",
  "sucursal": "Centro",
  "sala": "Sala 2",
  "horaInicio": "2026-11-16T21:00:00Z",
  "asiento": { "fila": "C", "numero": 5 },
  "precioUnitario": { "monto": "4500.00", "moneda": "ARS" }
}
```

## `POST /api/carritos/{carritoId}/pago`

Sin body. Inicia el pago del carrito en Mercado Pago.

**200 OK**:

```json
{ "urlPago": "https://sandbox.mercadopago.com.ar/checkout/v1/redirect?pref_id=…", "carrito": { "…": "estado Pagando" } }
```

- Primera vez, carrito `Activo` con email: el carrito pasa a `Pagando` y `venceEn` pasa a `min(venceEn + 1 min, inicio de
  la función)`.
- Carrito ya en `Pagando`: misma `urlPago`, sin cambios en `venceEn` (idempotente).

El cliente redirige al comprador a `urlPago`. Al terminar, Mercado Pago lo devuelve a `MercadoPago:UrlRetorno`
(con `{carritoId}` reemplazado), donde el front consulta `GET /api/carritos/{carritoId}` hasta ver `Pagado` o
`Expirado`.

Errores: `401`, `404 carrito-no-encontrado`, `409 email-entrega-faltante`, `409 carrito-no-activo` (`Expirado`,
`Abandonado` o `Pagado`, en `estadoActual`), `409 carrito-modificado` y `503 pago-no-disponible`.

## `GET /api/carritos/{carritoId}`

Igual que en la 007, con los campos nuevos. Si el carrito está en `Pagando` (vigente o vencido), antes de responder
pregunta a Mercado Pago por pagos aprobados del carrito y los procesa. Si Mercado Pago no responde en 3 segundos,
responde el estado conocido, sin error.

Uso recomendado del front al volver de Mercado Pago: consultar cada 2 segundos mientras `estado == "Pagando"`.

## `PUT /api/carritos/{carritoId}/asientos`, `POST …/abandono`, `PUT …/email-entrega`

Sin cambios de forma. Con el carrito en `Pagando` responden `409 carrito-en-pago`.

## `POST /api/pagos/mercadopago/avisos`

Lo llama Mercado Pago. Formato del aviso de Mercado Pago (webhook):

```http
POST /api/pagos/mercadopago/avisos?data.id=1234567890&type=payment
x-signature: ts=1742505638683,v1=ced36ab6d33566bb1e16c125819b8d840d6b8ef136b0b9127c76064466f5229b
x-request-id: bb56a2f1-6aae-46ac-982e-9dcd3581d08e
Content-Type: application/json

{ "action": "payment.updated", "type": "payment", "data": { "id": "1234567890" } }
```

Validación: manifest `id:{data.id};request-id:{x-request-id};ts:{ts};` firmado con HMAC-SHA256 y
`MercadoPago:ClaveSecretaAvisos`, comparado en tiempo constante con `v1`. El cuerpo no se usa para decidir nada.

| Situación | Respuesta |
|---|---|
| Sin `x-signature`, formato inválido o firma que no coincide | `401` (sin cuerpo de negocio, no procesa) |
| Firma válida, `type` distinto de `payment` | `200` sin procesar |
| Firma válida, pago inexistente o de un carrito desconocido | `200`, se registra en el log |
| Firma válida, pago no aprobado | `200`, sin cambios |
| Firma válida, pago aprobado (acreditado, devuelto o ya procesado) | `200` |
| Mercado Pago no responde al consultar el pago | `500`, para que Mercado Pago reintente |

## `POST /api/dev/pagos-simulados` (solo Development y Testing)

```json
{ "carritoId": "6f1c…", "estado": "Aprobado", "monto": 9000.00, "aprobadoEn": "2026-11-16T20:05:50Z" }
```

- `estado`: `Aprobado`, `Rechazado` o `Pendiente`.
- `monto`: opcional; por defecto, el total del carrito.
- `aprobadoEn`: opcional; por defecto, ahora.

Registra el pago en el procesador simulado y lo procesa igual que un aviso. **200 OK**:
`{ "pagoId": "sim-…", "estadoCarrito": "Pagado" }`. `404` si el carrito no existe o si el endpoint no está habilitado.

## Mapa de asientos (sin cambios de forma)

`GET /api/funciones/{funcionId}/asientos` excluye además los asientos de carritos `Pagando` vigentes y de carritos
`Pagado`.

## Configuración

| Clave | Por defecto | Descripción |
|---|---|---|
| `MercadoPago:Modo` | `Simulado` | `Simulado` o `Real`. |
| `MercadoPago:AccessToken` | — | Secreto. Credencial de prueba del vendedor. Obligatoria con `Real`. |
| `MercadoPago:ClaveSecretaAvisos` | — | Secreto. Clave de la firma de webhooks. Obligatoria con `Real`; en Development/Testing hay una de desarrollo. |
| `MercadoPago:UrlAvisos` | — | URL pública del endpoint de avisos (por ejemplo, un túnel). Obligatoria con `Real`. |
| `MercadoPago:UrlRetorno` | `http://localhost:5173/carritos/{carritoId}/pago` | Vuelta al front; `{carritoId}` se reemplaza. |
| `MercadoPago:SegundosTimeoutConsulta` | `3` | Timeout de la verificación al consultar el carrito. |
| `Outbox:IntervaloSegundos` | `2` | Frecuencia del procesador del Outbox. |
| `Outbox:MaximoIntentos` | `8` | Reintentos antes de marcar un mensaje como fallido. |
| `Email:Proveedor` | `Registrador` | `Registrador` o `Smtp`. |
| `Email:Remitente` | `entradas@cinema.local` | Dirección "From". |
| `Email:Smtp:Host`, `Puerto`, `Usuario`, `Clave`, `UsarTls` | — | SMTP (Mailpit en Development; Resend por SMTP en otros entornos). |
