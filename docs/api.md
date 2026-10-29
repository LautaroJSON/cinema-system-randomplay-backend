# Guía de la API para el frontend

Esta guía explica **cómo se usa** la API: el flujo, la autenticación, los errores y las reglas que el frontend tiene que
respetar. La **forma exacta** de cada endpoint (rutas, parámetros, cuerpos, respuestas) está en
[`openapi.json`](openapi.json), que es la fuente de verdad.

## Fuentes

| Qué | Dónde |
| --- | --- |
| Contrato OpenAPI 3.1 | [`docs/openapi.json`](openapi.json). Se regenera en cada `dotnet build`; no se edita a mano. |
| Contrato en vivo | `GET /openapi/v1.json` (solo en Development) |
| Referencia navegable | `/scalar` (solo en Development), por ejemplo `http://localhost:5165/scalar` |

Tipos TypeScript del frontend: generarlos desde `openapi.json` en lugar de escribirlos a mano.

```bash
npx openapi-typescript ../cinema-system-randomplay-backend/docs/openapi.json -o src/api/schema.d.ts
```

Si cambia el backend, se vuelven a generar y el compilador marca lo que se rompió.

## Flujo del comprador

```
1. GET  /api/catalogo/peliculas?titulo=...                                   películas activas
2. GET  /api/catalogo/peliculas/{id}                                         detalle
3. GET  /api/funciones/peliculas/{peliculaId}/sucursales-hoy                 sucursales con funciones hoy
4. GET  /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy
                                                                             horarios → funcionId y precio
5. GET  /api/funciones/{funcionId}/asientos                                  mapa de asientos
6. POST /api/funciones/{funcionId}/carritos                                  retiene asientos, devuelve el token
7. PUT  /api/carritos/{carritoId}/asientos        (token)                    cambia la selección
   PUT  /api/carritos/{carritoId}/email-entrega   (token)                    email para las entradas
   GET  /api/carritos/{carritoId}                 (token)                    refresca estado y vencimiento
   POST /api/carritos/{carritoId}/abandono        (token)                    libera los asientos
```

Los pasos 1 a 6 son públicos. El pago todavía no existe: el flujo termina con el carrito activo y el email indicado.

## Autenticación

Hay dos esquemas, y el frontend del comprador usa solo el primero.

- **Token de carrito (`Invitado`).** Lo devuelve `POST /api/funciones/{funcionId}/carritos` en el campo `token`. Es la
  **única vez** que se entrega, así que hay que guardarlo, por ejemplo en `sessionStorage` junto con `carrito.id`. Se envía
  como `Authorization: Bearer {token}` y da acceso solo a ese carrito. Vence en `tokenVenceEn` (60 minutos por defecto).
- **Personal (`Personal`).** JWT del proveedor OIDC para el personal interno (roles `CinemaManager` o `Admin`). Hoy ningún
  endpoint lo usa.

Respuestas de autenticación en las operaciones con token:

- `401`: falta el token, está vencido o no es un token de carrito.
- `404 carrito-no-encontrado`: el token es válido pero de **otro** carrito. La API no distingue "no existe" de "no es tuyo".

## Errores

Todos los errores de negocio son `application/problem+json` (RFC 9457) con una extensión **`codigo`**. Decidir siempre por
`codigo`, nunca por `title`, que es texto para humanos y puede cambiar.

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Hay asientos que ya no están disponibles.",
  "status": 409,
  "codigo": "asientos-no-disponibles",
  "asientos": [{ "fila": "C", "numero": 7 }]
}
```

| `codigo` | Status | Cuándo | Campos extra | Qué hacer en el frontend |
| --- | --- | --- | --- | --- |
| `pedido-invalido` | 400 | Selección vacía, más del máximo, asientos repetidos o mal formados; email inválido | `errors`: campo → mensajes | Mostrar los mensajes junto al campo (`asientos`, `email`) |
| `desafio-rechazado` | 403 | No se superó el desafío anti-bots | — | Repetir el desafío |
| `funcion-no-encontrada` | 404 | La función no existe o su película no está activa | — | Volver a los horarios |
| `carrito-no-encontrado` | 404 | El carrito no existe o el token es de otro carrito | — | Descartar el token guardado y empezar de nuevo |
| `asientos-no-disponibles` | 409 | Otro comprador tomó esos asientos | `asientos` | Marcarlos como ocupados y refrescar el mapa |
| `funcion-sin-cupo` | 409 | La función llegó al máximo de asientos retenidos a la vez | — | Pedir que se intente más tarde |
| `carrito-no-activo` | 409 | El carrito ya está `Abandonado` o `Expirado` | `estadoActual` | Avisar y empezar un carrito nuevo |
| `funcion-no-disponible` | 410 | La función ya comenzó | — | Volver a los horarios |
| `asientos-inexistentes` | 422 | Hay asientos que no existen en esa sala | `asientos` | Error del cliente: refrescar el mapa |
| `demasiados-pedidos` | 429 | Demasiadas creaciones de carrito desde la misma IP | header `Retry-After` (segundos) | Esperar `Retry-After` antes de reintentar |

Los `404` y `410` de las consultas (catálogo, horarios, mapa de asientos) son `ProblemDetails` estándar **sin** `codigo`:
alcanza con el status.

## Reglas que el frontend tiene que respetar

Los valores son los de `appsettings.json` y pueden cambiar por entorno. No hace falta copiarlos al frontend, porque la API
responde con el error correspondiente, pero sirven para dar una buena experiencia.

- **Retención:** un carrito retiene sus asientos **5 minutos**. `venceEn` indica el límite: mostrar una cuenta regresiva y, al
  llegar a cero, tratar el carrito como expirado. Cambiar la selección no extiende el vencimiento.
- **Máximo por carrito:** **10 asientos**, sin repetir.
- **Cupo por función:** entre todos los carritos activos, como máximo el **50 %** de los asientos de la sala puede estar
  retenido a la vez (`funcion-sin-cupo`).
- **Creación de carritos:** **10 por IP cada 10 minutos**. Cuentan también los intentos rechazados.
- **Reemplazar asientos** (`PUT .../asientos`) envía la **selección completa**: los asientos que no se incluyen se liberan.
- **Solo un carrito `Activo`** admite cambios (asientos, email, abandono). Uno `Abandonado` o `Expirado` se puede consultar,
  pero no modificar.
- **Abandonar** al salir del flujo libera los asientos al instante, sin esperar el vencimiento.

## Formatos

- **IDs:** GUID en texto.
- **Fechas:** ISO 8601 con offset (`DateTimeOffset`), por ejemplo `2026-10-28T21:30:00-03:00`.
- **Dinero:** `{ "monto": "4500.00", "moneda": "ARS" }`. `monto` es **texto** con 2 decimales y punto decimal, nunca un
  número: formatearlo para mostrarlo, sin hacer aritmética de punto flotante.
- **Asientos:** `{ "fila": "C", "numero": 7 }`. La fila es una letra mayúscula de A a Z y el número empieza en 1.
- **Mapa de asientos:** cada fila trae `cantidadAsientos` (numerados de 1 a N) y `asientosDisponibles`. Los números que
  faltan en `asientosDisponibles` están ocupados o retenidos.
- **Enums en texto:** `estado` del carrito (`Activo`, `Abandonado`, `Expirado`) y `clasificacion` de la película (`ATP`,
  `Mas13`, `Mas16`, `Mas18`).

## Pendiente para conectar un frontend

- **CORS no está configurado.** Un frontend servido desde otro origen (por ejemplo, `http://localhost:5173` de Vite) va a
  ser bloqueado por el navegador. Alternativas: configurar CORS en la API o usar el proxy del servidor de desarrollo del
  frontend.
