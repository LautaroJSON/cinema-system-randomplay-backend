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
| Reserva de Asientos de una Función | [specs/004-reserva-asientos-funcion](specs/004-reserva-asientos-funcion/spec.md) | ✅ Reserva de 1 a 10 asientos con retención de 10 minutos, mis reservas, cancelación y mapa que descuenta lo retenido |
| Precios de las Funciones | [specs/005-precios-funciones](specs/005-precios-funciones/spec.md) | ✅ Precio por entrada (ARS) en horarios y mapa; la reserva congela el precio unitario e informa su total |

Las tres primeras son features de **solo lectura y acceso público**. La cuarta es la primera que
modifica estado y exige autenticación (JWT bearer, rol `Customer`). `Sucursal`,
`Sala` (con su disposición de asientos) y `Funcion` todavía no tienen alta ni administración vía
API: sus datos se cargan sembrados directamente en la base de datos (ver
[Datos de desarrollo](#datos-de-desarrollo) más abajo).

### Flujo que puede consumir un cliente hoy

1. **Cartelera**: `GET /api/catalogo/peliculas` → el usuario elige una película.
2. **Detalle**: `GET /api/catalogo/peliculas/{id}`.
3. **Sucursales con funciones hoy**: `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy` → elige una sucursal.
4. **Horarios de hoy**: `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` → elige un horario (`funcionId`).
5. **Mapa de asientos**: `GET /api/funciones/{funcionId}/asientos` → ve qué asientos están disponibles.

6. **Reservar** (autenticado, rol `Customer`): `POST /api/funciones/{funcionId}/reservas` → retiene los asientos
   hasta `min(creación + 10 min, inicio de la función)`.
7. **Mis reservas / cancelar**: `GET /api/reservas`, `GET /api/reservas/{id}`, `POST /api/reservas/{id}/cancelacion`.

El próximo paso del flujo es el **pago** de la reserva (feature pendiente).

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

Los endpoints de catálogo, horarios y mapa son de acceso público (`[AllowAnonymous]` explícito). Los de
reservas exigen `Authorization: Bearer <jwt>` con el rol `Customer`; un endpoint sin política declarada
exige usuario autenticado (fallback cerrado).

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/catalogo/peliculas?titulo=` | Lista películas activas, con filtro opcional por título |
| `GET` | `/api/catalogo/peliculas/{id}` | Detalle de una película activa (`404` si no existe o está inactiva) |
| `GET` | `/api/funciones/peliculas/{peliculaId}/sucursales-hoy` | Sucursales con al menos una función hoy para esa película |
| `GET` | `/api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` | Horarios de hoy disponibles en esa Sucursal |
| `GET` | `/api/funciones/{funcionId}/asientos` | Mapa de asientos de una función: filas de la Sala y asientos disponibles, sin los retenidos por reservas vigentes (`404` si no existe, `410` si ya comenzó) |
| `POST` | `/api/funciones/{funcionId}/reservas` | 🔒 `Customer`. Reserva 1 a 10 asientos (todo o nada). `201`, o `400`/`404`/`409`/`410`/`422` con `codigo` |
| `GET` | `/api/reservas` | 🔒 `Customer`. Reservas del Cliente, de la más reciente a la más antigua |
| `GET` | `/api/reservas/{reservaId}` | 🔒 `Customer`. Una reserva propia (`404` si no existe o es ajena) |
| `POST` | `/api/reservas/{reservaId}/cancelacion` | 🔒 `Customer`. Cancela una reserva propia pendiente y vigente (`409 reserva-no-cancelable` si no) |

En el mapa de asientos solo se envían los asientos **disponibles**. Todo asiento de la fila (de 1 a
`cantidadAsientos`) que no figure en `asientosDisponibles` está ocupado, y el front debe mostrarlo
como tal.

Contratos completos, incluyendo códigos de error y ejemplos de body, en
[`specs/001-catalogo-peliculas/contracts`](specs/001-catalogo-peliculas/contracts/catalogo-api.md),
[`specs/002-horarios-sucursal-hoy/contracts`](specs/002-horarios-sucursal-hoy/contracts/funciones-api.md)
[`specs/003-mapa-asientos-funcion/contracts`](specs/003-mapa-asientos-funcion/contracts/mapa-asientos-api.md)
y [`specs/004-reserva-asientos-funcion/contracts`](specs/004-reserva-asientos-funcion/contracts/reservas-api.md).
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
  precioEntrada: Dinero;        // precio por entrada de la función
  filas: {
    fila: string;               // "A".."Z", en orden alfabético
    cantidadAsientos: number;   // los asientos de la fila son 1..cantidadAsientos
    asientosDisponibles: number[]; // lo que no está acá, está ocupado
  }[];
}

// Montos: texto con exactamente 2 decimales (nunca un número), moneda ISO 4217.
interface Dinero {
  monto: string;                // "5500.00"
  moneda: string;               // "ARS"
}
```

Los horarios de hoy informan `precioEntrada` por función, y las reservas (crear, listar y consultar)
informan `precioUnitario` y `total` (`precioUnitario × asientos`), congelados al crear la reserva.

## Configuración de reservas y autenticación

- `Reservas:MinutosRetencion` (10) y `Reservas:MaximoAsientosPorCliente` (10) en `appsettings.json`.
- La API valida JWT con la configuración estándar `Authentication:Schemes:Bearer` (`Authority`,
  `ValidAudiences`, `ValidIssuer`). `Autenticacion:ClaimRoles` (por defecto `roles`) indica el claim con los
  roles; el Cliente es el claim `sub`.
- En desarrollo se pueden emitir tokens locales con
  `dotnet user-jwts create --project src/CinemaSystemRandomPlay.Api --name cliente-ana --role Customer`.

## Deuda técnica conocida

Simplificaciones deliberadas, documentadas en el `research.md` de cada feature:

- **Outbox sin publicador** (desviación del Principio VI de la constitución): crear y cancelar una reserva
  escribe `ReservaCreada` / `ReservaCancelada` en `OutboxMensajes` dentro de la misma transacción, pero no
  existe todavía el proceso que los publica (`ProcesadoEn` queda en `NULL`). Tampoco se emite un evento
  cuando una reserva vence (el vencimiento se calcula contra el reloj). Ambos llegan con la feature de
  notificaciones en tiempo real (SignalR). Ver `specs/004-reserva-asientos-funcion/plan.md`, Complexity Tracking.
- **Marca "sin funciones disponibles" del Catálogo**: `NingunaFuncionDisponibleChecker` sigue
  devolviendo que ninguna película tiene funciones, aunque el módulo `Funciones` ya existe.
- **CORS**: no está configurado; un front web servido desde otro origen necesita configurarlo o
  usar un proxy en desarrollo.

## Testing

El testing se aplica por criterio (Principio V de la constitución): se testean las reglas de negocio
no triviales, los casos de uso que modifican estado y las consultas con lógica de filtrado o fechas; el
código trivial (DTOs, registro de DI, mapeos 1 a 1) no lleva tests. La única obligación sin excepción
es el test de concurrencia de la reserva de asientos. Cuando se testea persistencia, restricciones o
concurrencia, se hace contra un **Postgres real** levantado con Testcontainers — el provider InMemory
de EF Core no se usa para eso.

```bash
dotnet test tests/CinemaSystemRandomPlay.Domain.UnitTests
dotnet test tests/CinemaSystemRandomPlay.Application.UnitTests
dotnet test tests/CinemaSystemRandomPlay.IntegrationTests   # requiere Docker activo (Testcontainers)
```

Los tests de integración de reservas (`Reservas/`) también aceptan un Postgres ya levantado en lugar de
Docker: definí `CINEMA_TEST_POSTGRES` con una cadena de conexión al servidor (cada fixture crea y descarta
su propia base).

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
