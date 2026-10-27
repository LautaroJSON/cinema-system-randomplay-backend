# Quickstart: Reserva de Asientos de una Función

## Prerrequisitos

- .NET 10 SDK instalado.
- Docker corriendo (Postgres local y `Testcontainers.PostgreSql` para los tests de integración).
- Postgres local levantado con `docker compose up -d`, como en las features anteriores.

## Aplicar migraciones

```bash
dotnet ef database update --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api
```

La migración `AddReservas` crea `Reservas`, `ReservaAsientos` y `OutboxMensajes` (ver
[data-model.md](./data-model.md), "Esquema"). En Development, la API también aplica las migraciones al
arrancar.

## Obtener tokens de desarrollo

En desarrollo no hace falta un proveedor OIDC: `dotnet user-jwts` emite tokens firmados localmente y
configura la API para aceptarlos ([research.md](./research.md), Decisión 7).

```bash
dotnet user-jwts create --project src/CinemaSystemRandomPlay.Api --name cliente-ana --role Customer
```

```bash
dotnet user-jwts create --project src/CinemaSystemRandomPlay.Api --name cliente-beto --role Customer
```

```bash
dotnet user-jwts create --project src/CinemaSystemRandomPlay.Api --name sin-rol
```

Cada comando imprime un token. Guardarlos como `TOKEN_ANA`, `TOKEN_BETO` y `TOKEN_SIN_ROL`. El `sub` de
cada token es el `--name`.

## Correr la API

```bash
dotnet run --project src/CinemaSystemRandomPlay.Api
```

Obtener un `funcionId` con el flujo de la feature 002 (`.../sucursales-hoy` → `.../horarios-hoy`).

## Validar el flujo end-to-end

Contrato de referencia: [contracts/reservas-api.md](./contracts/reservas-api.md).

1. **Reservar (US1)**: `POST /api/funciones/{funcionId}/reservas` con `TOKEN_ANA` y body
   `{"asientos":[{"fila":"C","numero":5},{"fila":"C","numero":6}]}` → `201`, `estado: "PendienteDePago"`,
   `venceEn` unos 10 minutos después de `creadaEn`, y header `Location`.
2. **El mapa refleja la retención (US2)**: `GET /api/funciones/{funcionId}/asientos` sin token → `200`,
   la fila `C` no incluye `5` ni `6`, y `cantidadDisponibles` bajó en 2.
3. **Asiento tomado (US1)**: el mismo POST con `TOKEN_BETO` y `C4`, `C5` → `409`,
   `codigo: "asientos-no-disponibles"`, `asientos: [{"fila":"C","numero":5}]`. El mapa sigue mostrando
   `C4` disponible (todo o nada).
4. **Asiento inexistente**: POST con `{"fila":"Z","numero":1}` → `422`, `codigo: "asientos-inexistentes"`.
5. **Pedido inválido**: POST con `[]`, con 11 asientos o con un asiento repetido → `400`,
   `codigo: "pedido-invalido"`.
6. **Tope por Cliente**: con `TOKEN_ANA`, que ya retiene 2, pedir 9 asientos libres más → `409`,
   `codigo: "limite-asientos-superado"`, `asientosRestantes: 8`. Pedir 8 → `201`.
7. **Mis reservas (US3)**: `GET /api/reservas` con `TOKEN_ANA` → sus reservas, la más reciente
   primero. Con `TOKEN_BETO` → `[]`.
8. **Reserva ajena**: `GET /api/reservas/{idDeAna}` con `TOKEN_BETO` → `404`.
9. **Cancelar (US4)**: `POST /api/reservas/{id}/cancelacion` con `TOKEN_ANA` → `200`,
   `estado: "Cancelada"`. El mapa vuelve a mostrar los asientos. Repetir la cancelación → `409`,
   `codigo: "reserva-no-cancelable"`, `estadoActual: "Cancelada"`.
10. **Autenticación**: el POST sin header → `401`; con `TOKEN_SIN_ROL` → `403`. Los `GET` públicos de
    películas, horarios y mapa siguen respondiendo `200` sin token.
11. **Vencimiento**: para no esperar 10 minutos, arrancar la API con `Reservas__MinutosRetencion=1`,
    reservar, esperar un minuto y consultar: la reserva figura `Expirada` y sus asientos vuelven al
    mapa.
12. **Outbox**: después de los pasos 1 y 9, la tabla tiene un mensaje `ReservaCreada` y uno
    `ReservaCancelada`, con `ProcesadoEn` en `NULL`.

    ```sql
    SELECT "Tipo", "OcurridoEn", "ProcesadoEn" FROM "OutboxMensajes" ORDER BY "OcurridoEn";
    ```

## Correr los tests

```bash
dotnet test
```

El test de concurrencia (`ReservaConcurrenteTests`) levanta su propio Postgres con Testcontainers.
Para correrlo solo:

```bash
dotnet test tests/CinemaSystemRandomPlay.IntegrationTests --filter "FullyQualifiedName~ReservaConcurrente"
```

Resultado esperado: de 20 pedidos simultáneos sobre el mismo asiento, exactamente uno responde `201` y
19 responden `409 asientos-no-disponibles`. La base queda con una sola reserva para ese asiento.
