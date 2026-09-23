# Cinema System — RandomPlay Backend

Backend puro en **.NET 10 / C# 12 / ASP.NET Core** para un sistema de reserva de entradas de
cine. Expone una API REST (JSON) para un cliente externo — este repositorio no incluye frontend
ni renderizado de vistas.

El diseño y la evolución del proyecto siguen el flujo de [Spec Kit](https://github.com/github/spec-kit):
cada feature nace como una especificación en `specs/`, pasa por un plan técnico y una lista de
tareas antes de implementarse. La [constitución del proyecto](.specify/memory/constitution.md)
es la fuente de verdad de los principios no negociables — este README resume lo esencial para
poder levantar el sistema y orientarse; para el detalle normativo completo, ver ese documento.

## Arquitectura

Arquitectura hexagonal en 4 capas, con la regla de dependencia apuntando siempre hacia adentro:

```text
src/
├── CinemaSystemRandomPlay.Domain/          # Entidades, invariantes, puertos (interfaces). Sin dependencias de framework.
├── CinemaSystemRandomPlay.Application/     # Casos de uso (queries/handlers) y DTOs. Depende solo de Domain.
├── CinemaSystemRandomPlay.Infrastructure/  # EF Core, Npgsql, implementaciones de los puertos.
└── CinemaSystemRandomPlay.Api/             # Controllers ASP.NET Core (host HTTP). Sin lógica de negocio.
```

- **Domain** no referencia EF Core, ASP.NET Core ni ningún framework de infraestructura.
- **Application** depende únicamente de tipos e interfaces de `Domain`.
- **Infrastructure** implementa esos puertos (repositorios, reloj, ocupación de asientos) y no es referenciada por
  `Domain` ni `Application`.
- **Api** solo traduce HTTP ↔ casos de uso; no contiene lógica de negocio.
- Toda dependencia externa (repositorios, proveedor de fecha/hora) se consume vía interfaz,
  resuelta con el contenedor de DI nativo de ASP.NET Core.
- Los tipos del dominio se nombran en español (`Pelicula`, `Sucursal`, `Sala`, `Funcion`, y
  los Value Objects `Asiento` y `FilaSala`), reflejando el lenguaje ubicuo del negocio.

Cada módulo (`Catalogo`, `Funciones`, …) se organiza por carpeta dentro de cada capa, no por
proyecto — un único monolito modular, sin microservicios.

## Features implementadas

| Feature | Spec | Estado |
|---|---|---|
| Catálogo de Películas | [specs/001-catalogo-peliculas](specs/001-catalogo-peliculas/spec.md) | ✅ Listado, filtro por título y detalle de películas activas |
| Horarios por Sucursal — Funciones de Hoy | [specs/002-horarios-sucursal-hoy](specs/002-horarios-sucursal-hoy/spec.md) | ✅ Sucursales con funciones hoy y horarios disponibles por Sucursal |
| Mapa de Asientos de una Función | [specs/003-mapa-asientos-funcion](specs/003-mapa-asientos-funcion/spec.md) | ✅ Disposición de la Sala (filas y asientos) y asientos disponibles de una función |

Las tres son features de **solo lectura y acceso público** (sin autenticación). `Sucursal`,
`Sala` (con su disposición de asientos) y `Funcion` todavía no tienen alta ni administración vía
API: sus datos se cargan sembrados directamente en la base de datos (ver
[Datos de desarrollo](#datos-de-desarrollo) más abajo).

### Flujo que puede consumir un cliente hoy

1. **Cartelera**: `GET /api/catalogo/peliculas` → el usuario elige una película.
2. **Detalle**: `GET /api/catalogo/peliculas/{id}`.
3. **Sucursales con funciones hoy**: `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy` → elige una sucursal.
4. **Horarios de hoy**: `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` → elige un horario (`funcionId`).
5. **Mapa de asientos**: `GET /api/funciones/{funcionId}/asientos` → ve qué asientos están disponibles.

El próximo paso del flujo es la **reserva de asientos** (feature pendiente), que exigirá
autenticación y será la primera feature que modifique estado.

## Cómo levantar el sistema

### Opción rápida: Docker Compose (recomendada)

Levanta Postgres y la Api juntos, aplica las migraciones y siembra datos de desarrollo
automáticamente — un solo comando:

```bash
docker compose up --build
```

La Api queda escuchando en `http://localhost:8080`.

Para bajar todo:

```bash
docker compose down        # conserva los datos (volumen persistente)
docker compose down -v     # borra también los datos, para arrancar de cero
```

### Opción manual: `dotnet run` contra Postgres local

Prerrequisitos: .NET 10 SDK y Docker (para Postgres).

```bash
docker run --name cinema-postgres -e POSTGRES_PASSWORD=postgres -p 5433:5432 -d postgres:16-alpine
dotnet ef database update --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api
dotnet run --project src/CinemaSystemRandomPlay.Api
```

En `Development`, la Api corre las migraciones pendientes y siembra datos de desarrollo
automáticamente al arrancar (ver `DevDataSeeder` en `Infrastructure/Persistence`), tanto en
Docker como en local — no hace falta insertar datos a mano para probar el flujo completo.

## Datos de desarrollo

`Sucursal`, `Sala` y la disposición de asientos de cada Sala (`SalaFilas`) se siembran como datos
de referencia estáticos en las migraciones.
`Funcion` se siembra en tiempo de ejecución (`DevDataSeeder`, solo en `Development`, idempotente)
con horarios relativos al momento en que arranca la Api, para que siempre haya "funciones de
hoy" disponibles sin importar qué día la levantes. El id de la película de demostración sembrada
es fijo: `88888888-0000-0000-0000-000000000001`.

## Endpoints

Todos los endpoints listados son de acceso público (`[AllowAnonymous]` explícito).

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/catalogo/peliculas?titulo=` | Lista películas activas, con filtro opcional por título |
| `GET` | `/api/catalogo/peliculas/{id}` | Detalle de una película activa (`404` si no existe o está inactiva) |
| `GET` | `/api/funciones/peliculas/{peliculaId}/sucursales-hoy` | Sucursales con al menos una función hoy para esa película |
| `GET` | `/api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` | Horarios de hoy disponibles en esa Sucursal |
| `GET` | `/api/funciones/{funcionId}/asientos` | Mapa de asientos de una función: filas de la Sala y asientos disponibles (`404` si no existe, `410` si ya comenzó) |

En el mapa de asientos solo se envían los asientos **disponibles**. Todo asiento de la fila (de 1 a
`cantidadAsientos`) que no figure en `asientosDisponibles` está ocupado, y el front debe mostrarlo
como tal.

Contratos completos, incluyendo códigos de error y ejemplos de body, en
[`specs/001-catalogo-peliculas/contracts`](specs/001-catalogo-peliculas/contracts/catalogo-api.md),
[`specs/002-horarios-sucursal-hoy/contracts`](specs/002-horarios-sucursal-hoy/contracts/funciones-api.md)
y [`specs/003-mapa-asientos-funcion/contracts`](specs/003-mapa-asientos-funcion/contracts/mapa-asientos-api.md).
En `Development` también está disponible el explorador OpenAPI en `/openapi/v1.json`.

### Forma de la respuesta del mapa de asientos

```ts
interface MapaAsientosResponse {
  funcionId: string;
  horaInicio: string;           // ISO 8601
  peliculaId: string;
  peliculaTitulo: string;
  sucursalId: string;
  sucursalNombre: string;
  salaId: string;
  salaNombre: string;
  totalAsientos: number;
  cantidadDisponibles: number;  // 0 => función agotada
  filas: {
    fila: string;               // "A".."Z", en orden alfabético
    cantidadAsientos: number;   // los asientos de la fila son 1..cantidadAsientos
    asientosDisponibles: number[]; // lo que no está acá, está ocupado
  }[];
}
```

## Deuda técnica conocida

Simplificaciones deliberadas, documentadas en el `research.md` de cada feature:

- **Ocupación de asientos**: `SinAsientosOcupados` (stub de `IOcupacionAsientos`) informa todos los
  asientos como disponibles hasta que exista la feature de Reservas, que reemplazará solo su
  registro en `Program.cs`, sin cambiar el contrato HTTP.
- **Marca "sin funciones disponibles" del Catálogo**: `NingunaFuncionDisponibleChecker` sigue
  devolviendo que ninguna película tiene funciones, aunque el módulo `Funciones` ya existe.
- **CORS**: no está configurado; un front web servido desde otro origen necesita configurarlo o
  usar un proxy en desarrollo.

## Testing

El testing es un principio no negociable de la constitución: la lógica de `Domain` se cubre con
tests unitarios, y los casos de uso se validan con tests de integración contra un **Postgres
real** levantado con Testcontainers — el provider InMemory de EF Core no se usa para validar
restricciones ni comportamiento de queries.

```bash
dotnet test tests/CinemaSystemRandomPlay.Domain.UnitTests
dotnet test tests/CinemaSystemRandomPlay.Application.UnitTests
dotnet test tests/CinemaSystemRandomPlay.IntegrationTests   # requiere Docker activo (Testcontainers)
```

## Stack técnico

- .NET 10 / C# 12 / ASP.NET Core (Controllers)
- Entity Framework Core + Npgsql (PostgreSQL — mismo motor en desarrollo, tests y producción)
- xUnit + NSubstitute (unit tests) · Testcontainers.PostgreSql + `WebApplicationFactory` (tests de integración)
- Docker / Docker Compose para el entorno de desarrollo

## Estructura de carpetas

```text
.specify/            # Constitución del proyecto y plantillas de Spec Kit
specs/                # Especificaciones, planes, research y tasks por feature
src/                  # Código fuente (Domain / Application / Infrastructure / Api)
tests/                # Domain.UnitTests / Application.UnitTests / IntegrationTests
docker-compose.yml    # Levanta Postgres + Api para desarrollo local
```

## Flujo de desarrollo

Toda contribución llega por Pull Request y debe describir qué principios de la constitución toca
y cómo los respeta. Puertas de CI obligatorias antes de mergear:

1. La solución compila con nullable habilitado y sin advertencias nuevas.
2. Pasan los tests unitarios de dominio.
3. Pasan los tests de integración con Testcontainers de los casos de uso críticos.

Cualquier desviación deliberada de un principio de la constitución debe documentarse
explícitamente en el PR o el commit — no debe introducirse en silencio.
