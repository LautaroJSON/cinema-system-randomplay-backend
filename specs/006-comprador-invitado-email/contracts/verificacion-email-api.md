# Contrato de API: Verificación de Email del Invitado

> **Nota (feature 007):** reemplazada por el [carrito anónimo con retención corta](../../007-carrito-anonimo/contracts/carritos-api.md). La verificación por email, sus endpoints y el tope por email ya no existen. Este documento es el registro histórico de lo que se mergeó en la feature 006.

Los dos endpoints de este documento son **públicos** (`[AllowAnonymous]`, FR-018): su función es dar acceso
a quien todavía no lo tiene. Los errores usan el mismo formato `application/problem+json` con una
extensión `codigo` estable que el resto de la API (ver [reservas-api.md](./reservas-api.md), "Errores").

## `POST /api/verificaciones-email`

Pide un código de un solo uso para un email (User Story 1, FR-001).

### Request

```json
{ "email": "Ana@Example.com" }
```

- `email`: obligatorio. Se recorta y se pasa a minúsculas antes de usarse (FR-002).

### 202 Accepted

Sin body. La respuesta es **idéntica** para cualquier email válido, haya tenido o no reservas (FR-003,
SC-008). En desarrollo, el código se escribe en el log de la API en lugar de enviarse (FR-007).

### Errores

| Status | `codigo` | Cuándo | Extensiones |
|---|---|---|---|
| `400` | `pedido-invalido` | `email` ausente, vacío o con formato inválido. No se genera ningún código. | `errors` |
| `429` | `demasiados-pedidos` | Se superó el límite de pedidos por email (5 por hora por defecto) (FR-006). No se envía nada. | `Retry-After` (segundos) |
| `429` | — | Se superó el límite por origen (20 por hora por defecto). Lo responde el middleware de rate limiting, con `Retry-After`. | — |

Al aceptarse, el código anterior de ese email queda invalidado: solo el último es válido (FR-004).

## `POST /api/verificaciones-email/confirmacion`

Ingresa el código y entrega la credencial de acceso (User Story 1, FR-008).

### Request

```json
{ "email": "ana@example.com", "codigo": "493017" }
```

- `email`: el mismo del pedido (se normaliza igual).
- `codigo`: seis dígitos.

### 200 OK

```json
{
  "credencial": "eyJhbGciOiJIUzI1NiIs...",
  "tipo": "Bearer",
  "venceEn": "2026-10-27T19:20:00-03:00"
}
```

- `credencial`: un JWT firmado, asociado al email normalizado. Se envía en las operaciones del comprador
  como `Authorization: Bearer <credencial>`.
- `venceEn`: por defecto 60 minutos después de emitirse (FR-008). No se revoca antes; para recuperar el
  acceso se vuelve a verificar el email (FR-011: pueden coexistir varias credenciales vigentes).

### Errores

| Status | `codigo` | Cuándo |
|---|---|---|
| `400` | `pedido-invalido` | `email` con formato inválido o `codigo` ausente o que no son seis dígitos. |
| `400` | `codigo-invalido` | Código incorrecto, ya usado, o sin código vigente para ese email. Las tres causas son indistinguibles. |
| `400` | `codigo-invalidado` | El intento fallido que agota el máximo (5 por defecto) invalida el código: hay que pedir uno nuevo (FR-005). Los intentos siguientes, aun con el código correcto, responden `codigo-invalido`. |
| `410` | `codigo-vencido` | El código venció (10 minutos por defecto): hay que pedir uno nuevo (FR-004). |

Ningún error entrega una credencial. Conocer el email de otra persona sin recibir su código no permite
obtener una credencial para ese email (SC-002).
