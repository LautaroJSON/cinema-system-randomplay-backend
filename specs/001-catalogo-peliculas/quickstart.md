# Quickstart: Catálogo de Películas

## Prerrequisitos

- .NET 10 SDK instalado.
- Docker corriendo (para Postgres local y para `Testcontainers.PostgreSql` en los tests de integración).
- Haber cableado los `ProjectReference` entre `Domain`/`Application`/`Infrastructure`/`Api` y registrado `PeliculaRepository`, `NingunaFuncionDisponibleChecker` y los query handlers en `Program.cs` (tarea de `/speckit-implement`, no de este documento).

## Levantar la base de datos local

```bash
docker run --name cinema-postgres -e POSTGRES_PASSWORD=postgres -p 5433:5432 -d postgres:16
```

Nota: se usa el puerto **5433** (no el 5432 estándar) porque esta máquina ya tiene un Postgres nativo instalado escuchando en 5432; el contenedor de este proyecto se mapea a un puerto distinto para no pisarlo. Si en tu máquina el 5432 está libre, podés usarlo sin problema — solo ajustá el puerto acá y en `appsettings.Development.json` para que coincidan.

Aplicar migraciones de EF Core (una vez que `CinemaDbContext` y la migración inicial existan):

```bash
dotnet ef database update --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api
```

## Cargar datos de prueba

Insertar manualmente 2-3 filas en la tabla `Peliculas` (activa/inactiva, con título repetido para validar el Edge Case de títulos duplicados) — no hay todavía un endpoint de administración para crearlas (esa es otra feature).

## Correr la API

```bash
dotnet run --project src/CinemaSystemRandomPlay.Api
```

## Validar el flujo end-to-end

1. **Listado (User Story 1)**: `GET /api/catalogo/peliculas` → debe devolver solo las películas `Activa == true`, cada una con `sinFuncionesDisponibles: true` (no existe todavía la feature Funciones, así que todas se marcan sin funciones — ver [research.md](./research.md) Decisión 1).
2. **Catálogo vacío**: desactivar/borrar todas las películas de prueba → `GET /api/catalogo/peliculas` debe devolver `[]`.
3. **Detalle (User Story 2)**: `GET /api/catalogo/peliculas/{id}` de una película activa → debe incluir `sinopsis`. Con el `id` de una película inactiva o inexistente → `404`.
4. **Búsqueda (User Story 3)**: `GET /api/catalogo/peliculas?titulo=<substring>` → debe devolver solo las coincidencias parciales, case-insensitive.

## Correr los tests

```bash
dotnet test tests/CinemaSystemRandomPlay.Domain.UnitTests
dotnet test tests/CinemaSystemRandomPlay.Application.UnitTests
dotnet test tests/CinemaSystemRandomPlay.IntegrationTests   # requiere Docker activo (Testcontainers)
```

Los tests de integración deben cubrir, como mínimo, los 4 escenarios de validación de arriba contra un Postgres real levantado por Testcontainers, no contra el provider InMemory (Principio V de la constitución).
