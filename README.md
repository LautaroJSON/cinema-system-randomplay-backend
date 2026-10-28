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
| Reserva de Asientos de una Función | [specs/004-reserva-asientos-funcion](specs/004-reserva-asientos-funcion/spec.md) | ♻️ Base del carrito de la 007 (retención, todo o nada, mapa que descuenta lo retenido); sus endpoints de reservas ya no existen |
| Precios de las Funciones | [specs/005-precios-funciones](specs/005-precios-funciones/spec.md) | ✅ Precio por entrada (ARS) en horarios y mapa; la reserva congela el precio unitario e informa su total |
| Comprador Invitado con Email Verificado | [specs/006-comprador-invitado-email](specs/006-comprador-invitado-email/spec.md) | ♻️ Reemplazada por la 007: la verificación por email no frenaba el acaparamiento |
| Carrito Anónimo con Retención Corta | [specs/007-carrito-anonimo](specs/007-carrito-anonimo/spec.md) | ✅ Sin cuenta, login ni verificación: elegir asientos crea un carrito con un token, retenido 5 minutos; la selección se reemplaza de forma atómica sin extender el reloj, se puede abandonar y se indica el email de entrega. Topes por carrito, por función y por origen |

Las tres primeras son features de **solo lectura y acceso público**. El carrito (007) es lo que modifica estado:
el comprador es **anónimo** y su única credencial es el **token del carrito** que recibe al crearlo. `Sucursal`,
`Sala` (con su disposición de asientos) y `Funcion` todavía no tienen alta ni administración vía
API: sus datos se cargan sembrados directamente en la base de datos (ver
[Datos de desarrollo](#datos-de-desarrollo) más abajo).

### Flujo que puede consumir un cliente hoy

1. **Cartelera**: `GET /api/catalogo/peliculas` → el usuario elige una película.
2. **Detalle**: `GET /api/catalogo/peliculas/{id}`.
3. **Sucursales con funciones hoy**: `GET /api/funciones/peliculas/{peliculaId}/sucursales-hoy` → elige una sucursal.
4. **Horarios de hoy**: `GET /api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` → elige un horario (`funcionId`).
5. **Mapa de asientos**: `GET /api/funciones/{funcionId}/asientos` → ve qué asientos están disponibles.

6. **Elegir asientos** (sin credencial): `POST /api/funciones/{funcionId}/carritos` → crea un carrito que retiene
   los asientos hasta `min(creación + 5 min, inicio de la función)` y devuelve su **token**.
7. **Cambiar la selección** (con el token): `PUT /api/carritos/{id}/asientos` → reemplaza la selección completa de
   forma atómica; el vencimiento no se extiende.
8. **Email de entrega**: `PUT /api/carritos/{id}/email-entrega` → se guarda sin verificar, para enviar la entrada.
9. **Ver o abandonar**: `GET /api/carritos/{id}`, `POST /api/carritos/{id}/abandono` (libera los asientos al instante).

El próximo paso del flujo es el **pago** del carrito (feature pendiente), que enviará la entrada al email indicado.

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

Cada endpoint declara su modalidad de acceso: **pública** (`[AllowAnonymous]` explícito: catálogo, horarios,
mapa y crear un carrito), **portador del carrito** (`Authorization: Bearer <token>`, el token que devuelve la
creación del carrito) o **personal con rol** (`CinemaManager`/`Admin`, por el proveedor OIDC; la política existe
pero todavía no hay endpoints de personal). Un endpoint sin modalidad declarada exige usuario autenticado
(fallback cerrado) y se considera un defecto. El rol `Customer` no existe. Sin token válido, las operaciones del
carrito responden `401` para cualquier id; con el token de otro carrito, `404 carrito-no-encontrado`, igual que
si no existiera.

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/catalogo/peliculas?titulo=` | Lista películas activas, con filtro opcional por título |
| `GET` | `/api/catalogo/peliculas/{id}` | Detalle de una película activa (`404` si no existe o está inactiva) |
| `GET` | `/api/funciones/peliculas/{peliculaId}/sucursales-hoy` | Sucursales con al menos una función hoy para esa película |
| `GET` | `/api/funciones/peliculas/{peliculaId}/sucursales/{sucursalId}/horarios-hoy` | Horarios de hoy disponibles en esa Sucursal |
| `GET` | `/api/funciones/{funcionId}/asientos` | Mapa de asientos de una función: filas de la Sala y asientos disponibles, sin los retenidos por carritos activos (`404` si no existe, `410` si ya comenzó) |
| `POST` | `/api/funciones/{funcionId}/carritos` | Crea un carrito con 1 a 10 asientos (todo o nada) y devuelve el carrito y su `token` (`201`). `400`/`404`/`409 asientos-no-disponibles` o `funcion-sin-cupo`/`410`/`422`; `403 desafio-rechazado`; `429 demasiados-pedidos` si se superó el límite por origen |
| `GET` | `/api/carritos/{carritoId}` | 🔒 Portador. El carrito, con su estado efectivo (`Activo`, `Expirado`, `Abandonado`) |
| `PUT` | `/api/carritos/{carritoId}/asientos` | 🔒 Portador. Reemplaza la selección (atómico, todo o nada, sin extender el vencimiento). `409 carrito-no-activo` si venció o se abandonó |
| `POST` | `/api/carritos/{carritoId}/abandono` | 🔒 Portador. Abandona el carrito y libera sus asientos (`409 carrito-no-activo` si ya no está activo) |
| `PUT` | `/api/carritos/{carritoId}/email-entrega` | 🔒 Portador. Guarda el email de entrega, normalizado y sin verificar (`400` si es inválido) |

En el mapa de asientos solo se envían los asientos **disponibles**. Todo asiento de la fila (de 1 a
`cantidadAsientos`) que no figure en `asientosDisponibles` está ocupado, y el front debe mostrarlo
como tal.

Contratos completos, incluyendo códigos de error y ejemplos de body, en
[`specs/001-catalogo-peliculas/contracts`](specs/001-catalogo-peliculas/contracts/catalogo-api.md),
[`specs/002-horarios-sucursal-hoy/contracts`](specs/002-horarios-sucursal-hoy/contracts/funciones-api.md)
[`specs/003-mapa-asientos-funcion/contracts`](specs/003-mapa-asientos-funcion/contracts/mapa-asientos-api.md)
y [`specs/007-carrito-anonimo/contracts`](specs/007-carrito-anonimo/contracts/carritos-api.md).
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

Los horarios de hoy informan `precioEntrada` por función, y el carrito informa `precioUnitario` (congelado al
crearlo) y `total` (`precioUnitario × asientos`, que cambia si se reemplaza la selección).

## Configuración del carrito y autenticación

- **Carrito** (sección `Carritos` de `appsettings.json`): `MinutosRetencion` (5), `MaximoAsientosPorCarrito` (10),
  `PorcentajeMaximoRetenidoPorFuncion` (50: entre todos los carritos activos de una función no se retiene más de la
  mitad de la sala, redondeado hacia abajo, mínimo 1), `CreacionesPorOrigen` (10) y `VentanaCreacionMinutos` (10).
- **Token del carrito** (sección `Invitados`): `ClaveFirma` (obligatoria, mínimo 32 bytes; sin ella la API no
  arranca) y `TokenMinutosVigencia` (60). En desarrollo local:
  `dotnet user-secrets set "Invitados:ClaveFirma" "<al-menos-32-bytes>" --project src/CinemaSystemRandomPlay.Api`
  (`docker compose` ya trae una clave de desarrollo). En otros entornos, por secreto o variable
  (`Invitados__ClaveFirma`).
- El token es un JWT HS256 firmado por esta API (esquema `Invitado`) con el id del carrito como claim; no se guarda
  ni se revoca. Lo que se puede hacer con él depende del estado del carrito: vencido o abandonado, solo se puede
  consultar. El personal usa un esquema aparte, `Bearer`, con la configuración estándar
  `Authentication:Schemes:Bearer` (`Authority`, `ValidAudiences`, `ValidIssuer`) y
  `Autenticacion:ClaimRoles` (por defecto `roles`); un token del personal no sirve como token de carrito.
- **Desafío anti-bots**: la creación de carritos pasa por el puerto `IDesafioAntiBots` (campo opcional `desafio`
  del pedido). La implementación registrada, `DesafioAntiBotsDesactivado`, aprueba siempre; sumar un CAPTCHA es
  registrar otra implementación.
- **Email**: el email de entrega se guarda en el carrito y no se verifica. Todavía no se envía nada: el puerto
  `IEnviadorDeEmail` lo usará la feature de pago. En `Development` y `Testing` está registrado un registrador que
  escribe en el log; en otros entornos, una implementación que falla de forma explícita.
- **Migraciones que descartan reservas**: la de la feature 006 (`AddCompradorEmailEnReservas`) y la de la 007
  (`ReemplazarReservasPorCarritos`, que además elimina `VerificacionesEmail` y crea `Carritos`) **descartan las
  reservas existentes**. Se niegan a hacerlo si hay reservas y no se definió `CINEMA_PERMITIR_DESCARTE_RESERVAS=true`;
  `Development` lo define al arrancar.

## Deuda técnica conocida

Simplificaciones deliberadas, documentadas en el `research.md` de cada feature:

- **Outbox sin publicador** (desviación del Principio VI de la constitución): crear, reemplazar y abandonar un
  carrito escribe `CarritoCreado` / `SeleccionReemplazada` / `CarritoAbandonado` en `OutboxMensajes` dentro de la
  misma transacción, pero no existe todavía el proceso que los publica (`ProcesadoEn` queda en `NULL`). Tampoco se
  emite un evento cuando un carrito vence (el vencimiento se calcula contra el reloj). Ambos llegan con la feature de
  notificaciones en tiempo real (SignalR). Ver `specs/004-reserva-asientos-funcion/plan.md`, Complexity Tracking.
- **Marca "sin funciones disponibles" del Catálogo**: `NingunaFuncionDisponibleChecker` sigue
  devolviendo que ninguna película tiene funciones, aunque el módulo `Funciones` ya existe.
- **Límite de creación de carritos por origen**: usa el rate limiter de ASP.NET Core, por proceso (con varias
  instancias el tope efectivo se multiplica) y, detrás de un proxy, requiere configurar `ForwardedHeaders` para
  que la IP sea la del cliente. Clientes que comparten una IP comparten el límite. El tope global por función,
  en cambio, es exacto para todas las instancias (se calcula en la base, bajo el bloqueo de la función).
- **Carritos viejos sin purga**: los carritos expirados y abandonados quedan en la base como historial.
- **Token perdido**: no hay forma de recuperarlo; el carrito vence solo a los 5 minutos.
- **Email sin servicio real**: `IEnviadorDeEmail` está registrado pero sin uso hasta la feature de pago; solo
  existe el registrador de desarrollo.
- **Desafío anti-bots**: solo existe el punto de extensión; no hay CAPTCHA real.
- **CORS**: no está configurado; un front web servido desde otro origen necesita configurarlo o
  usar un proxy en desarrollo.

## Testing

El testing se aplica por criterio (Principio V de la constitución): se testean las reglas de negocio
no triviales, los casos de uso que modifican estado y las consultas con lógica de filtrado o fechas; el
código trivial (DTOs, registro de DI, mapeos 1 a 1) no lleva tests. La única obligación sin excepción
es el test de concurrencia de la reserva de asientos (`Carritos/CarritoConcurrenteTests`: creaciones,
reemplazos y tope por función simultáneos). Cuando se testea persistencia, restricciones o
concurrencia, se hace contra un **Postgres real** levantado con Testcontainers — el provider InMemory
de EF Core no se usa para eso.

```bash
dotnet test tests/CinemaSystemRandomPlay.Domain.UnitTests
dotnet test tests/CinemaSystemRandomPlay.Application.UnitTests
dotnet test tests/CinemaSystemRandomPlay.IntegrationTests   # requiere Docker activo (Testcontainers)
```

Los tests de integración de carritos (`Carritos/`) también aceptan un Postgres ya levantado en lugar de
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
