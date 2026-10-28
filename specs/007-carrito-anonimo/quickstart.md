# Quickstart: Carrito Anónimo con Retención Corta

Guía de validación de punta a punta. Contrato: [contracts/carritos-api.md](./contracts/carritos-api.md).

## Requisitos

- .NET 10 SDK y Docker (los tests de integración levantan Postgres con Testcontainers). Sin Docker, definir
  `CINEMA_TEST_POSTGRES` con la cadena de conexión de un Postgres existente.
- Una clave de firma de al menos 32 bytes para el token del carrito:

```bash
dotnet user-secrets set "Invitados:ClaveFirma" "una-clave-local-de-al-menos-32-bytes-0123" --project src/CinemaSystemRandomPlay.Api
```

## Build y tests

```bash
dotnet build
```

```bash
dotnet test
```

Resultado esperado: todo en verde, incluidos `Carritos/CarritoConcurrenteTests` (creaciones y reemplazos
simultáneos contra Postgres real) y el test de la migración `ReemplazarReservasPorCarritos`.

## Migración

En `Development` la API migra al arrancar y define `CINEMA_PERMITIR_DESCARTE_RESERVAS=true`: las reservas de
la 006 se descartan. En otro entorno, la migración aborta si hay reservas y la variable no está definida:

```bash
CINEMA_PERMITIR_DESCARTE_RESERVAS=true dotnet ef database update --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api
```

## Recorrido manual

Con la API en `Development` (`dotnet run --project src/CinemaSystemRandomPlay.Api`), tomar un `funcionId`
futuro de `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` (o del archivo `.http`).

1. **Crear el carrito** sin credencial: `POST /api/funciones/{funcionId}/carritos` con dos asientos libres.
   → `201` con `carrito.venceEn` = creación + 5 min y un `token`.
2. **Mapa**: `GET /api/funciones/{funcionId}/asientos` → esos asientos ya no figuran como disponibles.
3. **Reemplazar**: `PUT /api/carritos/{id}/asientos` con `Authorization: Bearer <token>` y otros dos asientos.
   → `200`; el mapa muestra libres los viejos y retenidos los nuevos; `venceEn` no cambió.
4. **Reemplazo rechazado**: crear un segundo carrito con un asiento X y pedir X desde el primero.
   → `409 asientos-no-disponibles` con `[X]`; el primer carrito conserva su selección.
5. **Email de entrega**: `PUT /api/carritos/{id}/email-entrega` con `" Ana@Example.com "`.
   → `200` con `emailEntrega = "ana@example.com"`; no se envía ningún email.
6. **Aislamiento**: `GET /api/carritos/{idDelSegundo}` con el token del primero → `404 carrito-no-encontrado`.
   Sin token → `401`.
7. **Abandonar**: `POST /api/carritos/{id}/abandono` → `200 Abandonado`; los asientos vuelven al mapa.
   Repetir → `409 carrito-no-activo` con `estadoActual = Abandonado`.
8. **Vencimiento**: crear un carrito y esperar 5 minutos (o bajar `Carritos:MinutosRetencion`).
   → `GET` lo muestra `Expirado` y `PUT …/asientos` responde `409 carrito-no-activo`.
9. **Tope por función**: en la sala Sur 1 (40 asientos, tope 20), retener 20 asientos con dos carritos y
   pedir uno más → `409 funcion-sin-cupo`.
10. **Límite por origen**: 11 `POST …/carritos` seguidos desde la misma IP → el 11.º responde `429
    demasiados-pedidos` con `Retry-After`.
11. **Endpoints retirados**: `POST /api/verificaciones-email` y `GET /api/reservas` ya no existen. Sin token responden
    `401` (el fallback cerrado aplica también a rutas inexistentes); con un token del personal, `404`.
