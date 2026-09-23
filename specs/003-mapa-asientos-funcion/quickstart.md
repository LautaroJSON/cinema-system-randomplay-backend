# Quickstart: Mapa de Asientos de una Función

## Prerrequisitos

- .NET 10 SDK instalado.
- Docker corriendo (Postgres local y `Testcontainers.PostgreSql` para los tests de integración).
- Postgres local levantado con `docker compose up -d`, como en las features 001 y 002.

## Aplicar migraciones

```bash
dotnet ef database update --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api
```

La migración `AddDisposicionSalas` crea la tabla `SalaFilas` y siembra la disposición de las 4 Salas
existentes (ver [data-model.md](./data-model.md), "Esquema"). En Development, la API también aplica las
migraciones al arrancar.

## Correr la API

```bash
dotnet run --project src/CinemaSystemRandomPlay.Api
```

En Development, `DevDataSeeder` garantiza que la película demo (`88888888-0000-0000-0000-000000000001`)
tenga funciones hoy en las Salas sembradas.

## Obtener un `funcionId`

Seguir el flujo de la feature 002:

1. `GET /api/funciones/peliculas/88888888-0000-0000-0000-000000000001/sucursales-hoy` → elegir una sucursal.
2. `GET /api/funciones/peliculas/88888888-0000-0000-0000-000000000001/sucursales/{sucursalId}/horarios-hoy` → tomar un `funcionId`.

## Validar el flujo end-to-end

Contrato de referencia: [contracts/mapa-asientos-api.md](./contracts/mapa-asientos-api.md).

1. **Disposición uniforme (User Story 1)**: `GET /api/funciones/{funcionId}/asientos` con una función de "Sucursal Centro — Sala 1" → `200` con filas `A` a `H`, cada una con `cantidadAsientos: 12`, y `totalAsientos: 96`.
2. **Filas irregulares**: una función de "Sucursal Centro — Sala 2" → `200` con 6 filas de largo distinto (A:8 … F:14) y `totalAsientos: 70`.
3. **Todos disponibles (User Story 2, sin Reservas)**: en cualquier respuesta `200`, cada `asientosDisponibles` va de `1` a `cantidadAsientos` y `cantidadDisponibles == totalAsientos`.
4. **Contexto de la función**: la respuesta incluye `peliculaTitulo`, `sucursalNombre`, `salaNombre` y `horaInicio`, y coinciden con lo que mostró el endpoint de horarios.
5. **Función inexistente**: `GET /api/funciones/{guid-aleatorio}/asientos` → `404`.
6. **Función ya comenzada**: insertar una función con horario pasado y consultarla → `410`.

   ```sql
   INSERT INTO "Funciones" ("Id", "PeliculaId", "SalaId", "FechaHoraInicio") VALUES
     ('99999999-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000001',
      '22222222-0000-0000-0000-000000000004', NOW() - INTERVAL '1 hour');
   ```

   `GET /api/funciones/99999999-0000-0000-0000-000000000001/asientos` → `410`.

La ocupación real ("hay asientos ocupados") no se puede validar a mano en esta feature, porque todavía
no existe nada que ocupe asientos. Ese caso se cubre en los tests con un `IOcupacionAsientos` falso.

## Correr los tests

```bash
dotnet test
```

Cobertura esperada:
- **Domain.UnitTests**: invariantes de `Asiento`, `FilaSala` y `Sala` (sin filas, letras repetidas), `Sala.AsientosDisponibles` (con y sin ocupados, ignorando ocupados ajenos a la Sala) y `Funcion.YaComenzo`.
- **Application.UnitTests**: los tres resultados del handler (Ok, no encontrada, no disponible), el caso de película inactiva y el caso con asientos ocupados usando un `IOcupacionAsientos` falso, con `IReloj` fijo.
- **IntegrationTests** (Postgres real con Testcontainers): `FuncionRepository.ObtenerConSala` carga filas y sucursal, el seed de `SalaFilas`, y el endpoint devolviendo `200`, `404` y `410`.
