# Quickstart: Precios de las Funciones

## Prerrequisitos

- Los mismos que la feature 004: .NET 10 SDK, Docker y Postgres local con `docker compose up -d`.
- Tokens de desarrollo de la feature 004 (`dotnet user-jwts`, ver
  [specs/004-reserva-asientos-funcion/quickstart.md](../004-reserva-asientos-funcion/quickstart.md)).
  Guardar uno con rol `Customer` como `TOKEN_ANA`.

## Aplicar migraciones

```bash
dotnet ef database update --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api
```

La migración `AddPrecios` agrega las columnas de precio a `Funciones` y `Reservas`
([data-model.md](./data-model.md), "Esquema").

Verificación de datos existentes (si la base ya tenía funciones y reservas de la feature 004):

```bash
docker compose exec postgres psql -U postgres -d cinema_dev -c 'SELECT r."Id", r."PrecioUnitario_Monto", f."PrecioEntrada_Monto" FROM "Reservas" r JOIN "Funciones" f ON f."Id" = r."FuncionId";'
```

Esperado: cada reserva tiene el mismo monto que su función y ninguna columna de precio es `NULL`
(FR-012).

## Correr la API

```bash
dotnet run --project src/CinemaSystemRandomPlay.Api
```

El sembrado de desarrollo crea funciones con precios `5500.00`, `7250.50` y `6000.00` cuando no hay
funciones de hoy (research.md, Decisión 9). Si la base ya tenía funciones de hoy, conservan el precio
`5500.00` que les asignó la migración.

## Validar el flujo end-to-end

Contrato de referencia: [contracts/precios-api.md](./contracts/precios-api.md).

1. **Precio en los horarios (US1)**: `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy`
   sin token → `200`; cada horario tiene `precioEntrada` con `monto` como texto de 2 decimales y
   `moneda` `"ARS"`, además de los campos de siempre.
2. **Precio en el mapa (US1)**: `GET /api/funciones/{funcionId}/asientos` sin token → `200` con
   `precioEntrada`.
3. **Reserva con total (US2)**: `POST /api/funciones/{funcionId}/reservas` con `TOKEN_ANA` y 3 asientos
   disponibles → `201`; `precioUnitario` igual al `precioEntrada` del paso 2 y `total` igual al
   triple.
4. **Precio congelado (US2)**: cambiar el precio de la función en la base:

   ```bash
   docker compose exec postgres psql -U postgres -d cinema_dev -c "UPDATE \"Funciones\" SET \"PrecioEntrada_Monto\" = 9999.99 WHERE \"Id\" = '<funcionId>';"
   ```

   `GET /api/reservas/{id}` de la reserva del paso 3 → mismos `precioUnitario` y `total`. El mapa de la
   función ahora informa `9999.99`, y una reserva nueva usa ese precio.
5. **Montos en mis reservas (US3)**: `GET /api/reservas` con `TOKEN_ANA` → cada reserva tiene
   `precioUnitario` y `total`, también las canceladas o expiradas.
6. **Rechazos sin cambios**: repetir el paso 3 con los mismos asientos → `409` con el mismo cuerpo que
   en la feature 004, sin montos.

## Tests

```bash
dotnet test
```

Incluye los tests nuevos de `Dinero`, `Funcion`, `Reserva` y los de integración de precios (ver
[plan.md](./plan.md), "Estrategia de tests"), y el test de concurrencia de reservas de la feature 004,
que debe seguir pasando.
