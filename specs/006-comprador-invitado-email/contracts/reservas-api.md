# Contrato de API: Reservas con Credencial de Invitado (cambios sobre la feature 004)

> **Nota (feature 007):** reemplazada por el [carrito anónimo con retención corta](../../007-carrito-anonimo/contracts/carritos-api.md). La verificación por email, sus endpoints y el tope por email ya no existen. Este documento es el registro histórico de lo que se mergeó en la feature 006.

Este documento solo describe lo que cambia respecto de
[../../004-reserva-asientos-funcion/contracts/reservas-api.md](../../004-reserva-asientos-funcion/contracts/reservas-api.md).
Las rutas, los cuerpos de pedido y respuesta, los códigos de estado y los `codigo` de error **no cambian**
(FR-012, FR-020, SC-007). Las demás secciones de ese documento siguen vigentes.

## Autenticación y autorización (reemplaza la sección de la feature 004)

Los cuatro endpoints de reservas exigen una **credencial de invitado** vigente, obtenida con
[verificacion-email-api.md](./verificacion-email-api.md):

```http
Authorization: Bearer <credencial>
```

| Situación | Respuesta |
|---|---|
| Sin header, credencial mal formada, vencida, con la firma alterada o emitida por otro sistema (incluido un token del personal) | `401 Unauthorized` |

- No existe `403` para el comprador: no hay roles (FR-010). Poseer una credencial vigente alcanza.
- El comprador es el email de la credencial. Ningún endpoint acepta un email en la ruta, la query o el body,
  y el body de `POST /api/funciones/{funcionId}/reservas` ignora cualquier campo `email` (FR-009).
- El rol `Customer` deja de existir (FR-019).

### Modalidades de acceso (FR-017)

| Endpoint | Modalidad |
|---|---|
| `GET /api/peliculas`, `GET /api/peliculas/{id}` | Pública |
| `GET /api/funciones/...` (sucursales y horarios de hoy) | Pública |
| `GET /api/funciones/{funcionId}/asientos` | Pública |
| `POST /api/verificaciones-email` | Pública |
| `POST /api/verificaciones-email/confirmacion` | Pública |
| `POST /api/funciones/{funcionId}/reservas` | Invitado verificado |
| `GET /api/reservas` | Invitado verificado |
| `GET /api/reservas/{reservaId}` | Invitado verificado |
| `POST /api/reservas/{reservaId}/cancelacion` | Invitado verificado |

Un endpoint sin modalidad declarada responde `401` (fallback cerrado) y se considera un defecto.

## Cambios de comportamiento

- **Tope por comprador** (FR-014): los 10 asientos retenidos por función se cuentan por email normalizado.
  `ANA@example.com ` y `ana@example.com` son el mismo comprador. `409 limite-asientos-superado` informa
  `asientosRestantes` sobre ese total. Otro email no comparte el cupo.
- **Aislamiento** (FR-015): `GET /api/reservas` devuelve solo las reservas del email de la credencial.
  `GET /api/reservas/{id}` y `POST /api/reservas/{id}/cancelacion` sobre una reserva de otro email responden
  `404 reserva-no-encontrada`, igual que si no existiera.
- **Vencimiento de la credencial** (FR-016): no afecta a las reservas. Siguen retenidas hasta su propio
  `venceEn`; solo se pierde el acceso hasta volver a verificar el email.
- **Mismo acceso desde otro dispositivo** (FR-011): una credencial nueva para el mismo email ve las mismas
  reservas.
- El mensaje (`title`) de `limite-asientos-superado` pasa de decir "por Cliente" a "por comprador"; el
  `codigo` y el status no cambian y son lo que el cliente debe usar para decidir.
