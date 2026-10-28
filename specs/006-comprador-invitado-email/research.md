# Research: Comprador Invitado con Email Verificado

Todas las decisiones parten del Principio VII v3.0.0 y del código vigente (feature 004). No quedan
`NEEDS CLARIFICATION`.

## Decisión 1: La credencial es un JWT propio, firmado con clave simétrica, en un esquema de autenticación separado

**Decision**: El sistema emite la credencial de invitado como un JWT HS256 (emisor y audiencia
`cinema-api-invitados`, claims `sub` = email normalizado, `email`, `tipo` = `invitado-verificado`,
`iat`, `exp`). Se valida con un segundo esquema JwtBearer llamado `Invitado`, distinto del esquema
`Bearer` que queda reservado al proveedor OIDC del personal. La clave sale de la configuración
(`Invitados:ClaveFirma`, mínimo 32 bytes) y el algoritmo aceptado se fija a HS256.

**Rationale**:
- FR-008 pide una credencial verificable sin consultar a nadie y que no se pueda falsificar: es
  exactamente un token firmado. No hay tabla de credenciales (Key Entities).
- Un esquema aparte hace que un token del personal (otro emisor, otra clave) nunca sirva como
  credencial de invitado, y al revés (FR-010, "emitida por otro sistema").
- Ya se usa JwtBearer en la Api (feature 004): no hay dependencia de validación nueva.
- Fijar `ValidAlgorithms` evita ataques de confusión de algoritmo.

**Alternatives considered**:
- *Token opaco guardado en base de datos*: permitiría revocar, pero exige una consulta por pedido y una
  tabla que la spec descarta; la revocación está fuera de alcance (Assumptions).
- *Reusar el esquema `Bearer` con dos emisores*: mezcla las dos poblaciones en un solo punto de
  validación; un error de configuración dejaría a un invitado pasar por personal.
- *Cookie de sesión*: el cliente es externo y sin frontend propio; un bearer es lo natural.

## Decisión 2: Quién emite y quién valida (hexagonal)

**Decision**: `IEmisorDeCredencial` (puerto en `Application`) con implementación `EmisorDeCredencialJwt`
en `Infrastructure` (usa `Microsoft.IdentityModel.JsonWebTokens`). La validación al recibir un pedido es
configuración de ASP.NET Core en la `Api` (`Program.cs`). Ambos leen las mismas opciones
(`OpcionesInvitados`) para compartir clave, emisor y audiencia. La credencial se devuelve como
`{ credencial, tipo: "Bearer", venceEn }`. La hora sale de `IReloj`.

**Rationale**: `Application` no debe conocer JWT (Principio I). El caso de uso solo pide "una
credencial para este email con este vencimiento".

**Alternatives considered**: emitir en la `Api` (el controller no puede contener esa lógica,
Principio VIII) o poner JWT en `Domain` (prohibido por el Principio I).

## Decisión 3: Normalización y validación del email en un Value Object del dominio

**Decision**: `EmailComprador` (Value Object en `Domain/Compradores`) recorta espacios, pasa a
minúsculas (cultura invariante) y valida: no vacío, máximo 254 caracteres, una sola `@`, parte local y
dominio no vacíos, dominio con un punto, sin espacios internos. No normaliza alias (`+tag`, puntos de
Gmail), como pide la spec. `EmailComprador.TryCrear` devuelve `false` si es inválido.

**Rationale**: FR-002 y FR-014 exigen que "mismo email" signifique lo mismo en la verificación, la
credencial, la propiedad de la reserva y el tope. Con un único tipo no hay forma de comparar emails sin
normalizar (Principio II, "Value Objects donde aporten seguridad de tipos").

**Alternatives considered**: `System.Net.Mail.MailAddress` (acepta formas raras como nombres con
comillas y comentarios que no queremos como identidad) y una regex extensa (frágil, ilegible). Una
validación estructural simple alcanza: la prueba real de que el email existe es que llegue el código.

## Decisión 4: Verificación como agregado propio, con código hasheado y una sola vigente por email

**Decision**: Agregado `VerificacionEmail` en `Domain/Verificaciones`: `Id`, `Email`, `CodigoHash`,
`CreadaEn`, `VenceEn`, `IntentosFallidos`, `Estado` guardado (`Vigente`, `Usada`, `Invalidada`). El
estado efectivo `Vencida` se calcula contra `IReloj`, igual que en `Reserva`. Métodos del agregado:
`Crear`, `Invalidar`, `EstadoEn(ahora)` y `Verificar(hashIngresado, ahora, maximoIntentos)`, que devuelve
un resultado explícito (`Correcto`, `Incorrecto`, `Vencido`, `Invalidado`, `YaUsado`).

- El código es de 6 dígitos generado con `RandomNumberGenerator`. Se guarda un HMAC-SHA256 del par
  email + código, con una clave derivada de la configuración, y se compara en tiempo constante.
- "Un email tiene a lo sumo una vigente" (Key Entities) se garantiza con un índice único parcial en
  Postgres: `UNIQUE (Email) WHERE Estado = 'Vigente'`.
- Pedir un código nuevo, dentro de la misma transacción, invalida la vigente anterior y crea la nueva.
- El conteo de intentos fallidos se persiste aunque el resultado sea un rechazo: el caso de uso devuelve
  el resultado (no lanza una excepción), así que la transacción confirma el incremento.

**Rationale**: las reglas de FR-004 y FR-005 son invariantes de estado y viven en el agregado
(Principio II). Hashear el código evita guardar un secreto usable si se filtra la base. El índice parcial
cierra la carrera de dos pedidos simultáneos.

**Alternatives considered**: código en claro (más simple, pero filtra secretos); contador de intentos en
memoria (se pierde al reiniciar y no escala a varias instancias); tabla separada de intentos (un
agregado alcanza).

## Decisión 5: Serializar por email con un bloqueo asesor de Postgres

**Decision**: Las dos operaciones de verificación (pedir y confirmar) corren en una transacción que
primero toma `pg_advisory_xact_lock(hashtextextended(email, 0))`, detrás de `IVerificacionEmailRepository`
(`BloquearPorEmail`). Con el bloqueo tomado se cuentan las solicitudes recientes, se invalida la vigente y
se inserta la nueva (o, al confirmar, se lee la vigente y se aplica `Verificar`).

**Rationale**: sin serialización, dos pedidos concurrentes podrían excederse del límite por email, o dos
confirmaciones simultáneas podrían saltarse el tope de intentos. Es el mismo patrón que la feature 004
(bloqueo de la fila de la función) y reutiliza `IUnidadDeTrabajo`. El bloqueo asesor no exige que la
fila exista, a diferencia de `SELECT ... FOR UPDATE`.

**Alternatives considered**: control optimista con `xmin` y reintentos (más código y reintentos
visibles) y confiar solo en el índice único (no protege el contador de intentos ni el límite).

## Decisión 6: Límite por email en base de datos; límite por origen con el rate limiter de ASP.NET Core

**Decision**:
- **Por email (FR-006, 5/hora)**: se cuentan las filas de `VerificacionesEmail` del email con
  `CreadaEn >= ahora - ventana`, bajo el bloqueo de la Decisión 5. Si el conteo alcanza el máximo, el
  caso de uso responde `LimiteExcedido` y no se crea ni envía nada. El conteo no depende de que el email
  tenga reservas, así que no viola FR-003. Las filas viejas sirven de registro; su limpieza queda
  fuera de esta feature.
- **Por origen (20/hora)**: middleware `AddRateLimiter` de ASP.NET Core con una política
  `solicitud-codigo`, ventana fija particionada por `RemoteIpAddress`, aplicada con
  `[EnableRateLimiting]` al endpoint de pedir código. Rechaza con `429` y `Retry-After`.
  Los valores salen de `Invitados:SolicitudesPorOrigen` y `Invitados:VentanaSolicitudesMinutos`.

**Rationale**: el límite por email es una regla de negocio que debe valer en todas las instancias, así que
va en la base. El límite por origen es una defensa de borde contra el envío masivo, y el limiter nativo
es la herramienta estándar: no agrega dependencias ni tabla ni guarda IPs.

**Limitaciones conocidas** (se documentan en el README): el limitador por origen es por proceso (con
varias instancias, el tope efectivo se multiplica) y detrás de un proxy hace falta configurar
`ForwardedHeaders` para que `RemoteIpAddress` sea la IP real del cliente. Con la IP del proxy, todos los
clientes compartirían un solo cupo.

**Alternatives considered**: guardar el origen (hasheado) en `VerificacionesEmail` y contarlo en SQL
(correcto con varias instancias, pero guarda datos personales y complica el modelo; se reevalúa con
Redis o un límite en el gateway si hace falta) y una librería de terceros (innecesaria).

## Decisión 7: Envío del email detrás de un puerto, fuera de la transacción

**Decision**: `IEnviadorDeEmail` (puerto en `Application`) con `MensajeDeVerificacion(Email, Codigo)`.
La implementación `EnviadorDeEmailRegistrador` (en `Infrastructure`) escribe el mensaje con `ILogger` en
lugar de enviarlo (FR-007). El caso de uso envía **después** de confirmar la transacción: el envío no
corre dentro de la transacción (Principio VI). Si el envío falla, el código queda creado pero sin
entregar; el invitado puede pedir otro dentro del límite (spec, Edge Cases).

Registro por entorno: el registrador solo se registra en `Development` y `Testing`, porque registra el
código en claro. En cualquier otro entorno se registra `EnviadorDeEmailNoConfigurado`, que falla con un
mensaje claro: pedir un código responde `500` en lugar de filtrar códigos a los logs. El envío real de
producción queda fuera de alcance.

**Rationale**: separar "persistir" de "notificar" es lo que pide el Principio VI. Usar el outbox no
aplica: guardaría el código en claro en una tabla, y no existe aún el publicador. La desventaja de
entrega "como mucho una vez" se acepta porque el reintento es barato para el invitado.

**Alternatives considered**: enviar dentro de la transacción (viola el Principio VI); escribir el código
en el outbox (secreto en claro, y sin publicador no se envía nada).

## Decisión 8: La identidad del comprador reemplaza `ClienteId` en la reserva

**Decision**: `Reserva.ClienteId` (string del `sub` del proveedor) pasa a `Reserva.Comprador`
(`EmailComprador`), mapeado a la columna `CompradorEmail` (`varchar(254)`, con un conversor de valor).
`ReglasReserva.MaximoAsientosPorCliente` pasa a `MaximoAsientosPorComprador` (clave de configuración
`Reservas:MaximoAsientosPorComprador`). `ReservadorDeAsientos` compara por `EmailComprador`, que ya viene
normalizado. Los comandos y consultas reciben `EmailComprador` en lugar de `string clienteId`. Los
contratos HTTP de respuesta no cambian (FR-020); el `type`/`codigo` de los errores tampoco.

**Rationale**: FR-013, FR-014 y FR-015 piden que el dueño sea el email normalizado. Tipar la identidad
hace imposible comparar un email sin normalizar. El tope por función ya se cuenta dentro del bloqueo de
la función, así que la concurrencia de la feature 004 se conserva.

**Alternatives considered**: guardar el email como `string` y normalizar en cada punto (la causa de bugs
sutiles que FR-002 quiere evitar).

## Decisión 9: Modalidades de acceso explícitas y fallback cerrado

**Decision**: En `Program.cs`:
- Esquema por defecto: `Bearer` (OIDC del personal), como hoy. Esquema nuevo: `Invitado`.
- Política `InvitadoVerificado`: `AuthenticationSchemes = Invitado`, exige usuario autenticado y el
  claim `tipo = invitado-verificado`. El controller de reservas usa `[Authorize(Policy = InvitadoVerificado)]`.
- Política `Personal`: exige el rol `CinemaManager` o `Admin`. Queda definida pero sin endpoints (spec,
  Edge Cases).
- Se elimina la política `Customer`.
- `FallbackPolicy` sigue exigiendo usuario autenticado por el esquema por defecto: un endpoint sin
  modalidad declarada falla cerrado (FR-017, FR-019). Los endpoints públicos (incluidos los dos de
  verificación) llevan `[AllowAnonymous]` explícito.
- Un `ClaimsPrincipal` sin email válido (`EmailComprador.TryCrear` falla) responde `401`.

**Rationale**: es el Principio VII aplicado. Cada endpoint declara su modalidad; olvidar la declaración
da `401` y no un endpoint público. La política `Personal` cumple FR-019 sin crear endpoints.

**Alternatives considered**: `AuthorizationHandler` propio para comparar el email del claim con la
reserva (innecesario: la propiedad se resuelve filtrando por email en la consulta, y el resultado es
"no encontrada", como pide FR-015).

## Decisión 10: Migración y descarte explícito de las reservas de desarrollo (FR-021)

**Decision**: Dos migraciones EF: `AddCompradorEmailEnReservas` (pasos 1 y 2) y `AddVerificacionesEmail` (paso 3), con SQL explícito:
1. Borra `ReservaAsientos`, `Reservas` y los `OutboxMensajes` de tipo `ReservaCreada` y
   `ReservaCancelada`.
2. Renombra `ClienteId` a `CompradorEmail` (y su índice) y ajusta el largo a 254.
3. Crea `VerificacionesEmail` con el índice único parcial.

Protección contra datos reales: el paso 1 está precedido por una guarda SQL que lanza una excepción si
`Reservas` tiene filas y la variable de entorno `CINEMA_PERMITIR_DESCARTE_RESERVAS` no vale `true` cuando se
genera el script. Development la fija en `true` antes de `MigrateAsync()` (es una decisión explícita
para bases de desarrollo). `dotnet ef database update` fuera de Development, con filas, falla con un mensaje
que explica cómo decidirlo.

**Rationale**: FR-021 pide que el descarte sea explícito y que no se aplique a una base con datos reales
sin una decisión aparte. Una migración que borra en silencio incumple ambas cosas. La guarda convierte "no
perder datos" en el comportamiento por defecto.

**Alternatives considered**: asignar un email ficticio a las reservas viejas (inventa dueños y deja
reservas a nombre de nadie real); migración no destructiva con columna nullable (deja reservas sin dueño
por email, que FR-021 prohíbe).

## Decisión 11: Estrategia de pruebas de integración de identidad

**Decision**: La factory de tests configura una `ClaveFirma` de test y reemplaza `IEnviadorDeEmail` por un
doble en memoria que guarda los mensajes (para leer el código). `TokensDePrueba` gana
`Invitado(email, vencido, clave)`, que firma con esa clave; `Customer(...)` se elimina. Los tests de
reservas existentes pasan a usar credenciales de invitado. Un test de punta a punta recorre el flujo real:
pedir código, leerlo del doble, confirmarlo y reservar.

**Rationale**: probar el flujo completo una vez da confianza en que emisor y validador comparten
configuración; el resto de los tests no necesita repetirlo.
