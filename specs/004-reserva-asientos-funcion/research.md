# Research: Reserva de Asientos de una Función

El stack está fijado por la constitución y las reglas de negocio se cerraron en la clarificación de la
spec, así que el Technical Context del plan no tiene `NEEDS CLARIFICATION`. Este documento registra las
decisiones de diseño que había que tomar antes de Phase 1. Las dos más delicadas son cómo garantizar
que un asiento no se reserve dos veces bajo concurrencia (Decisión 2) y cómo entra la autenticación a
una API que hasta ahora era toda pública (Decisiones 7 a 9).

## Decisión 1: `Reserva` como agregado y la regla entre reservas en un domain service

**Decision**: `Reserva` es un aggregate root nuevo en `Domain/Reservas`. Protege sus propias
invariantes: de 1 a 10 asientos, sin repetidos, transiciones de estado válidas y el cálculo de su
vencimiento. Las reglas que involucran **varias** reservas (que ningún asiento esté retenido dos veces
y el tope de 10 asientos por Cliente y función) viven en un domain service, `ReservadorDeAsientos`.
Recibe la `Funcion` con su `Sala`, las reservas vigentes de esa función, el Cliente, los asientos
pedidos y el momento actual, y devuelve un resultado explícito: la `Reserva` creada o el motivo del
rechazo con los asientos afectados.

**Rationale**: El Principio II exige que "un asiento no puede reservarse dos veces" viva en el
agregado o en un domain service. Ninguna `Reserva` individual puede verificarlo, porque depende de las
demás reservas de la función, así que corresponde a un domain service. Devolver un resultado en lugar
de lanzar excepciones repite el patrón de `ObtenerMapaAsientosResultado` (feature 003) y permite que el
error lleve la lista de asientos (FR-006, FR-007).

**Alternatives considered**:
- Un agregado `OcupacionFuncion` que contenga todas las reservas de una función → rechazado: cargar y
  guardar todas las reservas de la función para crear una sola agranda el agregado sin necesidad. La
  consistencia entre reservas ya queda garantizada por el bloqueo de la Decisión 2.
- Validar en el handler de Application → rechazado: es regla de negocio y el Principio II lo prohíbe.

## Decisión 2: Concurrencia con bloqueo pesimista de la fila de la `Funcion`

**Decision**: El caso de uso "reservar" corre en una transacción de base de datos. Lo primero que hace
es bloquear la fila de la función en `Funciones` (`SELECT ... FOR UPDATE`). Con el bloqueo tomado, lee
las reservas vigentes de esa función, ejecuta `ReservadorDeAsientos`, inserta la reserva junto con su
mensaje de outbox y confirma la transacción. Dos pedidos sobre la misma función quedan serializados:
el segundo espera a que el primero confirme y entonces ve sus asientos como ocupados. Pedidos sobre
funciones distintas no se bloquean entre sí.

**Rationale**:
- Un solo mecanismo cubre las **dos** reglas concurrentes: asientos duplicados (FR-005) y el tope por
  Cliente y función (FR-008). Dos pedidos del mismo Cliente con 6 asientos cada uno también tienen que
  quedar serializados, y un índice único sobre asientos no lo impediría.
- Funciona con el vencimiento perezoso de la Decisión 3: una reserva vencida no bloquea nada aunque
  su fila siga diciendo `PendienteDePago`. Con un índice único habría que limpiar los vencidos antes de
  insertar, y esa limpieza a su vez compite con otros pedidos.
- Contención aceptable: el bloqueo dura lo que tarda una transacción corta (una lectura y dos
  inserciones), y una función tiene como mucho unos cientos de asientos.
- Lo exige probar contra Postgres real (Principio V): ver el test de concurrencia en el plan.

**Alternatives considered**:
- Índice único parcial sobre `(FuncionId, Fila, Numero)` para asientos retenidos → rechazado como
  mecanismo principal: no cubre el tope por Cliente, y como la condición de vigencia depende de la hora
  (`VenceEn > now()`) no puede ir en el predicado de un índice. Obligaría a materializar el vencimiento
  dentro de cada pedido. Queda anotado como posible defensa extra si en el futuro se agrega un proceso
  de vencimiento en segundo plano.
- Nivel de aislamiento `SERIALIZABLE` con reintentos → rechazado: correcto, pero cuando hay conflicto
  falla y hay que reintentar todo el caso de uso. El bloqueo explícito hace esperar en lugar de fallar
  y es más fácil de razonar.
- Concurrencia optimista con un token de versión en `Funcion` → rechazado por el mismo motivo
  (reintentos) y porque ensucia `Funcion` con una columna que solo existe para Reservas.
- Locks en memoria (`SemaphoreSlim` por función) → rechazado: no funciona con más de una instancia de
  la API.

## Decisión 3: Vencimiento perezoso, calculado contra el reloj

**Decision**: La reserva guarda `Estado` (`PendienteDePago` o `Cancelada`) y `VenceEn`. El estado
**efectivo** se calcula en el dominio: una reserva `PendienteDePago` con `VenceEn <= ahora` está
`Expirada`. Esa regla está en un único lugar (`Reserva.EstadoEn(ahora)`). Todas las lecturas (mapa,
reservas vigentes para reservar, "mis reservas", cancelación) filtran o calculan con `IReloj`. No hay
un proceso en segundo plano que actualice filas vencidas en esta feature.

`VenceEn = min(CreadaEn + MinutosRetencion, Funcion.FechaHoraInicio)` (FR-002, clarificación del
2026-10-27).

**Rationale**: FR-011 exige que la reserva se considere expirada "desde ese instante", sin depender de
que un proceso intervenga. Un job que corre cada N segundos dejaría una ventana en la que la reserva
vencida todavía ocupa asientos. Calcularlo contra el reloj es exacto, no agrega piezas móviles y es
fácil de testear con un `IReloj` falso.

**Alternatives considered**:
- `BackgroundService` que marca como `Expirada` cada X segundos → rechazado como fuente de verdad por
  la ventana de inexactitud. Se va a necesitar cuando haya que **notificar** la liberación por
  vencimiento en tiempo real (feature de SignalR); entonces podrá materializar el estado y escribir en
  el outbox sin cambiar la regla del dominio.
- Guardar `Expirada` al leer (actualizar en cada consulta) → rechazado: convierte lecturas en
  escrituras y el mapa público pasaría a escribir en la base.

## Decisión 4: Persistencia de `Reserva`

**Decision**: Tabla `Reservas` (`Id`, `FuncionId`, `ClienteId`, `Estado`, `CreadaEn`, `VenceEn`,
`CanceladaEn`) y tabla owned `ReservaAsientos` (`ReservaId`, `Fila`, `Numero`) con clave
`(ReservaId, Fila, Numero)`. `Asiento` (el Value Object de la feature 003) se mapea con `OwnsMany`.
Índices: `(FuncionId, Estado, VenceEn)` para el mapa y para las reservas vigentes, y
`(ClienteId, CreadaEn)` para "mis reservas". `Estado` se guarda como texto. Ver
[data-model.md](./data-model.md).

**Rationale**: Mismo criterio que `SalaFilas` en la feature 003: los asientos de una reserva no tienen
identidad propia fuera de ella. La clave compuesta hace que también la base rechace un asiento
repetido dentro de una reserva. El estado como texto hace legible la tabla sin depender del orden del
enum.

**Alternatives considered**:
- Asientos como columna JSON o arreglo de Postgres → rechazado: el mapa necesita filtrar y unir por
  asiento, y la clave compuesta da unicidad gratis.

## Decisión 5: Outbox mínimo, sin publicador

**Decision**: Se crea la tabla `OutboxMensajes` (`Id`, `Tipo`, `Payload` jsonb, `OcurridoEn`,
`ProcesadoEn` nullable). Crear y cancelar una reserva escriben en la **misma transacción** un mensaje
`ReservaCreada` o `ReservaCancelada` con la función, la reserva y los asientos. El caso de uso lo hace
a través del puerto `IOutbox` (Application). **No** se implementa en esta feature el proceso en segundo
plano que publica los mensajes.

**Rationale**: El Principio VI exige que "reserva creada" y "asiento liberado" queden registrados en
el outbox dentro de la misma transacción del cambio de negocio. Hacerlo desde la primera feature que
modifica estado evita volver a tocar estos casos de uso cuando llegue SignalR. El publicador no tiene
transporte al cual publicar, porque las notificaciones en tiempo real están fuera de alcance. Es una
desviación documentada del Principio VI (ver plan, Complexity Tracking).

**Alternatives considered**:
- No escribir outbox hasta la feature de SignalR → rechazado: viola el Principio VI ("DEBEN
  registrarse") y obliga a reabrir los casos de uso.
- Publicador que solo escribe en el log → rechazado: es código que no aporta y habría que mantener;
  los mensajes quedan en la tabla, sin perderse, hasta que exista un consumidor real.

## Decisión 6: Límites de la reserva como configuración inyectada al dominio

**Decision**: `ReglasReserva` (record en `Domain/Reservas`) con `MinutosRetencion = 10` y
`MaximoAsientosPorCliente = 10` (por reserva y por Cliente y función). `Program.cs` la construye desde
la sección de configuración `Reservas` y la registra como `Singleton`. El dominio la recibe por
parámetro y no conoce `IOptions`.

**Rationale**: FR-003 pide que la retención sea configurable. Que el dominio reciba un record simple
mantiene la regla de dependencia (Principio I) y hace triviales los tests.

## Decisión 7: JWT bearer con la configuración estándar de ASP.NET Core

**Decision**: Se agrega `Microsoft.AspNetCore.Authentication.JwtBearer` a la Api. Se registra con
`AddAuthentication().AddJwtBearer()` leyendo la sección estándar `Authentication:Schemes:Bearer`
(`Authority`, `ValidAudiences`, `ValidIssuer`). El proveedor OIDC real se configura por entorno, sin
tocar código. El nombre del claim de roles se configura en `Autenticacion:ClaimRoles` (por defecto
`roles`).

- **Desarrollo local**: `dotnet user-jwts create --role Customer` emite tokens firmados localmente y
  escribe solo la configuración de `Authentication:Schemes:Bearer` en `appsettings.Development.json`.
  No hace falta levantar un proveedor OIDC para probar la API.
- **Tests de integración**: la factory reemplaza la validación por una clave simétrica de test y
  genera tokens con `JsonWebTokenHandler`, con `sub` y roles a elección de cada test.

**Rationale**: El Principio VII exige OIDC con JWT bearer y que la Api solo valide tokens. Usar la
sección de configuración estándar hace que `user-jwts` y cualquier proveedor real (Keycloak, Auth0,
Entra ID) funcionen cambiando solo la configuración. Cada proveedor pone los roles en un claim
distinto, por eso el nombre es configurable. Por ejemplo, Keycloak necesita un mapper de "realm roles"
a un claim plano `roles`.

**Alternatives considered**:
- Keycloak en `docker-compose` para desarrollo → rechazado por ahora: agrega un servicio pesado y su
  configuración de realm solo para obtener tokens, cuando `user-jwts` ya resuelve el caso local. Se
  puede sumar cuando haya un front que necesite el flujo de login completo.
- Autenticación simulada por header (`X-User-Id`) en desarrollo → rechazado: deja un camino de
  autenticación que no es el real y podría quedar activo por error.

## Decisión 8: Fallback de autorización cerrado y políticas por rol

**Decision**:
- `AddAuthorization` define la política `"Customer"` (`RequireRole("Customer")`) y una
  **FallbackPolicy** que exige usuario autenticado. Un endpoint sin atributo de autorización queda
  protegido en lugar de público.
- Los endpoints de reservas usan `[Authorize(Policy = "Customer")]`. Los existentes ya tienen
  `[AllowAnonymous]` y siguen públicos (FR-024).
- `Program.cs` agrega `app.UseAuthentication()` antes del `UseAuthorization()` que ya existe.
- Sin token o con token inválido la respuesta es `401`; autenticado sin rol `Customer` es `403`
  (FR-022). Es el comportamiento estándar del middleware.

**Rationale**: El Principio VII dice que un endpoint sin política declarada es un defecto. Con el
fallback, ese defecto falla cerrado (`401`) en lugar de exponer datos, y los tests lo detectan. Los
cinco endpoints públicos actuales ya están marcados `[AllowAnonymous]`, así que el fallback no les
cambia nada.

## Decisión 9: Identidad del Cliente desde el claim `sub`

**Decision**: El dueño de una reserva es el `sub` del token, guardado como `ClienteId` (`string`,
hasta 255 caracteres). El controller lo lee de `User` con una extensión
(`ClaimsPrincipalExtensions.ObtenerClienteId()`) y lo pasa en el comando o la query. Ningún request
body o query string acepta un id de Cliente (FR-023). No se guarda ningún otro dato personal.

**Rationale**: `sub` es el identificador estable que garantiza OIDC. Es un `string` opaco y no siempre
un GUID (Auth0 usa `auth0|...`), por eso no se tipa como `Guid`. Leer un claim es traducción HTTP: no
es lógica de negocio en el controller (Principio VIII).

**Alternatives considered**:
- Puerto `IClienteActual` implementado con `IHttpContextAccessor` → rechazado: agrega una abstracción
  y estado ambiente para algo que el controller ya tiene a mano; pasarlo explícito en el comando hace
  que los handlers sean más fáciles de testear.

## Decisión 10: Contrato HTTP y códigos de error

**Decision** (detalle en [contracts/reservas-api.md](./contracts/reservas-api.md)):

| Operación | Ruta | Éxito |
|---|---|---|
| Reservar | `POST /api/funciones/{funcionId}/reservas` | `201 Created` + `Location: /api/reservas/{id}` |
| Mis reservas | `GET /api/reservas` | `200` |
| Una reserva | `GET /api/reservas/{reservaId}` | `200` |
| Cancelar | `POST /api/reservas/{reservaId}/cancelacion` | `200` con la reserva cancelada |

Errores con `ProblemDetails` (RFC 9457) y una extensión `codigo` estable:

| Caso | Status | `codigo` | Extensión extra |
|---|---|---|---|
| Body inválido: vacío, más de 10, repetidos, fila o número mal formados | `400` | `pedido-invalido` | `errors` |
| Función inexistente o de película inactiva / reserva inexistente o ajena | `404` | `funcion-no-encontrada` / `reserva-no-encontrada` | — |
| Función ya comenzada | `410` | `funcion-no-disponible` | — |
| Asientos que no existen en la Sala | `422` | `asientos-inexistentes` | `asientos` |
| Asientos ya retenidos | `409` | `asientos-no-disponibles` | `asientos` |
| Supera 10 asientos por Cliente y función | `409` | `limite-asientos-superado` | `asientosRestantes` |
| Cancelar una reserva expirada o cancelada | `409` | `reserva-no-cancelable` | `estadoActual` |

**Rationale**:
- `410` para la función ya comenzada mantiene el criterio del mapa (feature 003, Decisión 6).
- `404` para reservas ajenas no revela que existen (FR-017).
- `409` para conflictos que dependen del estado actual, y que pueden cambiar si otro libera asientos.
  `422` para asientos que nunca van a existir en esa Sala.
- Cancelar es una transición de estado que conserva la reserva, por eso `POST .../cancelacion` y no
  `DELETE`.
- El `codigo` le permite al front distinguir los dos `409` sin parsear el texto.

**Alternatives considered**:
- `DELETE /api/reservas/{id}` para cancelar → rechazado: sugiere que la reserva desaparece, pero
  sigue apareciendo en "mis reservas" como `Cancelada`.
- `POST /api/reservas` con el `funcionId` en el body → rechazado: la ruta anidada en la función sigue
  el patrón de `GET /api/funciones/{funcionId}/asientos` y el `funcionId` se valida por restricción de
  ruta.

## Decisión 11: El mapa lee la ocupación de las reservas; se elimina el stub

**Decision**: `ReservasRetienenAsientos` (Infrastructure/Reservas) implementa `IOcupacionAsientos`.
Devuelve los asientos de las reservas `PendienteDePago` de la función con `VenceEn > ahora`, usando
`IReloj`. `Program.cs` reemplaza el registro de `SinAsientosOcupados` y ese stub se borra. El handler
del mapa y su contrato HTTP no cambian (FR-020).

**Rationale**: Es exactamente el reemplazo previsto en la feature 003 (research.md, Decisión 3). La
consulta de lectura del mapa no bloquea: puede leer un instante antes de que otra transacción confirme,
lo cual es aceptable porque el mapa es informativo y la reserva vuelve a validar bajo bloqueo.

## Decisión 12: Las consultas de "mis reservas" usan un puerto de lectura

**Decision**: `IConsultaReservas` (Application/Reservas/Ports) devuelve directamente las filas de
lectura de las reservas del Cliente unidas con función, película, Sala y Sucursal (una consulta con
joins). El handler les aplica `Reserva.EstadoEn(ahora)` para el estado efectivo y las mapea a DTOs.
Las operaciones que modifican estado (reservar y cancelar) usan el agregado mediante
`IReservaRepository`.

**Rationale**: Para listar no hace falta reconstruir el agregado: armar la lista desde agregados
exigiría cargar funciones, salas y películas por separado (N+1 o varios métodos nuevos en otros
repositorios). Un puerto de lectura proyecta exactamente lo necesario en una sola consulta, y el
estado efectivo se calcula con la misma regla de dominio que usa el agregado.

## Nota fuera de alcance

- La confirmación por pago va a agregar el estado `Confirmada` y la transición
  `PendienteDePago → Confirmada`, válida solo si `ahora < VenceEn`. Las reservas confirmadas también
  ocupan asientos: `IOcupacionAsientos` y `ReservadorDeAsientos` van a tener que incluirlas.
- La notificación en tiempo real de la liberación por vencimiento requiere el proceso de la
  Decisión 3, alternativa 1.
