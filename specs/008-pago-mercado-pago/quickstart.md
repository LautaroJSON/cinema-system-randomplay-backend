# Quickstart: Pago del Carrito con Mercado Pago

Guía para comprobar la feature de punta a punta. Contrato en [contracts/pagos-api.md](./contracts/pagos-api.md).

## Requisitos

- Docker (Postgres y Mailpit).
- .NET 10 SDK.
- Para el modo real: una cuenta de Mercado Pago Developers con un **usuario vendedor de prueba** y un **usuario
  comprador de prueba**, y un túnel público (por ejemplo, `cloudflared` o `ngrok`) hacia la API.

## 1. Tests automáticos

```bash
dotnet test
```

Corren con el procesador de pagos simulado y Postgres en Testcontainers. Cubren la confirmación, las devoluciones, la
firma del aviso, la idempotencia y la concurrencia (ver "Estrategia de tests" en [plan.md](./plan.md)).

## 2. Modo simulado (sin túnel ni credenciales)

```bash
docker compose up --build
```

Levanta Postgres, Mailpit (UI en http://localhost:8025) y la API en http://localhost:8080 con
`MercadoPago:Modo=Simulado` y `Email:Proveedor=Smtp`.

1. Crear un carrito con `POST /api/funciones/{funcionId}/carritos` (ids de funciones en `GET
   /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` o en Scalar,
   http://localhost:8080/scalar). Guardar el `token`.
2. `PUT /api/carritos/{id}/email-entrega` con `{"email":"ana@example.com"}`.
3. `POST /api/carritos/{id}/pago` → `200`, `estado: "Pagando"`, `venceEn` un minuto más tarde y una `urlPago`
   simulada.
4. `POST /api/dev/pagos-simulados` con `{"carritoId":"{id}","estado":"Aprobado"}` → `estadoCarrito: "Pagado"`.
5. `GET /api/carritos/{id}` → `Pagado`, con una entrada por asiento.
6. En Mailpit aparece un único email para `ana@example.com` con las entradas (puede tardar unos segundos: lo envía el
   procesador del Outbox).

Casos borde para probar a mano:

- Repetir el paso 4 → no hay entradas ni emails nuevos.
- Con otro carrito, simular `"monto": 1` → el carrito sigue `Pagando` y en la base hay una fila en `Devoluciones` con
  motivo `MontoDistinto` que pasa a `Completada`.
- Con otro carrito, esperar a que venza y simular `"aprobadoEn"` posterior a `venceEn` → `Expirado` y devolución con
  motivo `PagoTardio`.
- Con un carrito en `Pagando`, `PUT …/asientos` o `POST …/abandono` → `409 carrito-en-pago`.

## 3. Modo real con credenciales de prueba

1. En Mercado Pago Developers, con el usuario vendedor de prueba: crear una aplicación de Checkout Pro, copiar el access
   token de prueba y configurar el webhook (evento "Pagos") hacia `https://<tunel>/api/pagos/mercadopago/avisos`. Copiar la
   clave secreta del webhook.
2. Configurar los secretos (nunca en `appsettings` ni en `docker-compose.yml`):

   ```bash
   dotnet user-secrets --project src/CinemaSystemRandomPlay.Api set "MercadoPago:AccessToken" "<access token de prueba>"
   ```

   ```bash
   dotnet user-secrets --project src/CinemaSystemRandomPlay.Api set "MercadoPago:ClaveSecretaAvisos" "<clave del webhook>"
   ```

   ```bash
   dotnet user-secrets --project src/CinemaSystemRandomPlay.Api set "MercadoPago:UrlAvisos" "https://<tunel>/api/pagos/mercadopago/avisos?source_news=webhooks"
   ```

   ```bash
   dotnet user-secrets --project src/CinemaSystemRandomPlay.Api set "MercadoPago:Modo" "Real"
   ```

3. Levantar Postgres y Mailpit con Docker, y la API con `dotnet run --project src/CinemaSystemRandomPlay.Api`.
4. Repetir los pasos 1 a 3 del modo simulado. Abrir la `urlPago` en una ventana privada, iniciar sesión con el usuario
   comprador de prueba y pagar con una tarjeta de prueba de Mercado Pago (titular `APRO` para aprobar, `OTHE` para
   rechazar).
5. Al volver al front (o consultando `GET /api/carritos/{id}`), el carrito pasa a `Pagado` en segundos. El email llega a
   Mailpit.
6. Verificar que, pasado `venceEn`, la página de pago de esa preferencia ya no acepta pagos.

## 4. Verificar la firma del aviso a mano

Con la API en Development (clave de desarrollo en `appsettings.Development.json`), un aviso sin firma o con firma
alterada responde `401`:

```bash
curl -i -X POST "http://localhost:8080/api/pagos/mercadopago/avisos?data.id=123&type=payment" -H "x-signature: ts=1,v1=00" -H "x-request-id: abc"
```

## Resultado esperado

- Ningún dato de tarjeta pasa por la API.
- Cada carrito pagado tiene exactamente una entrada por asiento y un email de entrega.
- Los pagos tardíos, duplicados o por un monto distinto se devuelven solos y quedan en `Devoluciones`.
