# Quickstart: Horarios por Sucursal — Funciones de Hoy

## Prerrequisitos

- .NET 10 SDK instalado.
- Docker corriendo (para Postgres local y para `Testcontainers.PostgreSql` en los tests de integración).
- Haber corrido antes el quickstart de `001-catalogo-peliculas` (mismo Postgres, misma `CinemaDbContext`) y tener al menos una `Pelicula` activa sembrada.
- Haber cableado `FuncionRepository`, `RelojSistema` (`IReloj`) y los query handlers de este módulo en `Program.cs` (tarea de `/speckit-implement`, no de este documento).

## Aplicar migraciones

```bash
dotnet ef database update --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api
```

Esto crea las tablas `Sucursales`, `Salas` y `Funciones`, y siembra `Sucursales`/`Salas` vía `HasData`
(dato de referencia estático — ver [research.md](./research.md), Decisión 2).

## Cargar datos de prueba de `Funcion`

`Funcion` no se siembra en la migración porque depende de "hoy" (ver research.md, Decisión 2). Insertar
manualmente unas filas usando fechas **relativas al momento en que corrés esto**, por ejemplo contra la
misma base Postgres del quickstart de `001`:

```sql
-- Sustituí <peliculaId> y <salaId> por ids reales de tus datos sembrados (SELECT "Id" FROM "Peliculas"; SELECT "Id", "SucursalId" FROM "Salas";)
INSERT INTO "Funciones" ("Id", "PeliculaId", "SalaId", "FechaHoraInicio") VALUES
  (gen_random_uuid(), '<peliculaId>', '<salaId-sucursalA>', CURRENT_DATE + INTERVAL '14 hours'),
  (gen_random_uuid(), '<peliculaId>', '<salaId-sucursalA>', CURRENT_DATE + INTERVAL '20 hours'),
  (gen_random_uuid(), '<peliculaId>', '<salaId-sucursalB>', CURRENT_DATE + INTERVAL '18 hours'),
  (gen_random_uuid(), '<peliculaId>', '<salaId-sucursalC>', CURRENT_DATE + INTERVAL '1 day' + INTERVAL '14 hours'); -- función de "mañana", para validar que NO aparece hoy
```

Ajustá los horarios para incluir al menos un caso ya pasado (para validar FR-008/Acceptance Scenario 2
de User Story 2) y al menos una Sucursal sin ninguna función hoy para esa película (para validar
FR-002).

## Correr la API

```bash
dotnet run --project src/CinemaSystemRandomPlay.Api
```

## Validar el flujo end-to-end

1. **Sucursales con funciones hoy (User Story 1)**: `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy` → debe listar solo Sucursal A y Sucursal B (no la que solo tiene función mañana), ordenadas por nombre.
2. **Película sin funciones hoy**: usar una `Pelicula` activa distinta sin `Funcion`es sembradas → debe devolver `[]`.
3. **Película inexistente/inactiva**: `GET /api/funciones/peliculas/{id-invalido}/sucursales-hoy` → `404`.
4. **Horarios de hoy (User Story 2)**: `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalA-id}/horarios-hoy` → debe listar solo las funciones cuyo `horaInicio` no pasó todavía, ordenadas cronológicamente, cada una con `salaNombre`.
5. **Todas las funciones de hoy ya pasaron**: correr la consulta anterior después de que pase el último horario sembrado para esa Sucursal → debe devolver `[]`.
6. **Sucursal no válida para la película**: `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalC-id}/horarios-hoy` (Sucursal C solo tiene función mañana, no hoy) → `404`.

## Correr los tests

```bash
dotnet test tests/CinemaSystemRandomPlay.Domain.UnitTests
dotnet test tests/CinemaSystemRandomPlay.Application.UnitTests
dotnet test tests/CinemaSystemRandomPlay.IntegrationTests   # requiere Docker activo (Testcontainers)
```

Los tests de integración deben cubrir, como mínimo, los 6 escenarios de validación de arriba contra un
Postgres real levantado por Testcontainers, no contra el provider InMemory (Principio V de la
constitución). Los tests unitarios de Application deben poder fijar "ahora" de forma determinística
mediante un `IReloj` de prueba (fake/stub), sin depender de la hora real del reloj del sistema — ver
research.md, Decisión 1.
