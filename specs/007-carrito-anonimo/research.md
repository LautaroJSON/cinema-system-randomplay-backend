# Research: Carrito Anónimo con Retención Corta

Decisiones técnicas de la feature 007. Cada una indica qué se eligió, por qué y qué se descartó.

## Decisión 1: el token del carrito es un JWT del esquema `Invitado`, con el id del carrito como claim

**Decisión**: al crear un carrito, la API emite un JWT HS256 firmado con `Invitados:ClaveFirma` (la misma
clave y el mismo esquema `Invitado` de la feature 006). Claims: `sub` y `carrito` con el id del carrito,
`tipo = "portador-carrito"` y un `jti` aleatorio. Vence a los `Invitados:TokenMinutosVigencia` minutos (por
defecto 60). Lo valida el esquema `Invitado` (emisor, audiencia, firma, algoritmo y vencimiento). Un
`OnTokenValidated` rechaza como no autenticado un token con nuestra firma pero sin `tipo` correcto o sin un
`carrito` que sea un Guid. La política `PortadorDeCarrito` reemplaza a `InvitadoVerificado`.

**Rationale**:
- Cumple FR-002: sin la clave no se puede fabricar un token para un id conocido, y el id visible no alcanza.
- Reutiliza lo que ya funciona y está testeado en la 006: la validación al arrancar de la clave (≥ 32
  bytes), la separación respecto del esquema OIDC del personal y los helpers de test (`TokensDePrueba`).
- No hace falta una tabla de tokens. El estado del carrito (activo, expirado, abandonado) gobierna lo que se
  puede hacer, así que no hace falta revocar el token.
- El token vive más que el carrito (60 contra 5 minutos) para que el portador pueda consultar el estado
  final y para que la feature de pago tenga margen. Un token vigente sobre un carrito no activo solo sirve
  para consultarlo.

**Alternativas descartadas**:
- *Token opaco aleatorio guardado hasheado en la tabla del carrito*: también cumple FR-002, pero agrega una
  columna, una búsqueda por hash en cada pedido y su propio código de comparación en tiempo constante, y deja
  sin uso el esquema `Invitado` que el usuario pidió reutilizar.
- *El id del carrito como credencial (UUID v4 secreto)*: el id aparece en rutas, logs y eventos del outbox.
  Mezclar identificador y secreto viola FR-002.

## Decisión 2: `Reserva` se renombra a `Carrito` en todas las capas

**Decisión**: el agregado `Reserva` pasa a ser `Carrito` (`Domain/Carritos`), con estados `Activo`,
`Expirado` (derivado del reloj, no se guarda) y `Abandonado`. Las tablas pasan a ser `Carritos` y
`CarritoAsientos`, y las rutas `/api/carritos`. El domain service `ReservadorDeAsientos` pasa a ser
`AsignadorDeAsientos`, con dos operaciones: `Crear` y `Reemplazar`. Desaparecen `Domain/Reservas`,
`Application/Reservas` y `ReservasController`.

**Rationale**: decisión del usuario (sesión 2026-10-28). El lenguaje ubicuo coincide con la spec y con la
constitución v4.0.0. La feature de pago creará la compra o la entrada a partir del carrito, sin cargar con un
agregado que se llama "reserva" pero no lo es.

**Alternativas descartadas**: conservar `Reserva` en el dominio y usar "carrito" solo en la API (los nombres
del dominio y del borde quedan distintos), o conservar `Reserva` en todo (oculta el cambio de modelo).

## Decisión 3: reemplazo atómico bajo el mismo bloqueo por función

**Decisión**: crear, reemplazar, abandonar e indicar el email se ejecutan en una transacción que toma primero
el bloqueo pesimista de la fila de la función (`FuncionRepository.ObtenerConSalaBloqueando`, `SELECT … FOR
UPDATE`, el mismo de la 004) y recién después lee el carrito y los carritos activos de la función. Para
reemplazar o abandonar, el handler lee primero, sin seguimiento, solo el `FuncionId` del carrito. Después
toma el bloqueo de esa función y vuelve a cargar el carrito con seguimiento, para decidir sobre el estado
confirmado más reciente.

El reemplazo se aplica como una diferencia sobre la colección de asientos del agregado: se quitan los que no
están en la nueva selección, se agregan los nuevos y los comunes no se tocan. Todo se confirma en un único
`SaveChanges` dentro de la transacción.

**Rationale**:
- FR-008 y SC-002: bajo `READ COMMITTED`, cualquier lector (el mapa, otro carrito) ve la selección anterior
  completa o la nueva completa, nunca las dos. Los asientos comunes no se borran ni se vuelven a insertar,
  así que no quedan libres ni un instante.
- FR-012: todo pedido que pueda retener asientos de una función pasa por el mismo bloqueo, así que dos
  carritos que disputan un asiento se serializan y gana exactamente uno.
- Orden de bloqueo único (función → carrito), sin riesgo de interbloqueo entre operaciones.
- Diferencia en lugar de "borrar todo y volver a insertar": EF Core rastrea los asientos como entidades
  propias con clave `(CarritoId, Fila, Numero)`. Quitar y agregar la misma clave en un solo `SaveChanges`
  hace que EF falle por la clave duplicada en seguimiento.

**Alternativas descartadas**:
- *Bloquear solo la fila del carrito para reemplazar*: no serializa contra otros carritos que quieren los
  mismos asientos nuevos.
- *Restricción única parcial en `CarritoAsientos` sobre carritos activos*: el vencimiento depende del reloj,
  y Postgres no admite índices parciales con `now()`. La 004 ya descartó esto por la misma razón.

## Decisión 4: tope global por función calculado en el domain service

**Decisión**: `ReglasCarrito.PorcentajeMaximoRetenidoPorFuncion` (por defecto 50) define el tope:
`max(1, floor(Sala.TotalAsientos × porcentaje / 100))`. `AsignadorDeAsientos` suma los asientos de los
carritos activos de la función, **excluido el carrito que se reemplaza**, y rechaza si esa suma más la nueva
selección supera el tope. El orden de validación es: función comenzada → asientos inexistentes → asientos no
disponibles → tope global.

**Rationale**: es una invariante que cruza carritos, igual que "un asiento no se retiene dos veces", y se
evalúa con los mismos datos y bajo el mismo bloqueo, así que también es exacta con pedidos concurrentes
(SC-006). Excluir al propio carrito cumple el escenario 5 de la US4. Chequear la disponibilidad antes que el
tope da el error más útil (qué asientos faltan) y no revela nada de otros carritos.

**Alternativas descartadas**: un contador por función guardado aparte (se desincroniza con los vencimientos,
que se derivan del reloj) y un tope absoluto por función (no escala con el tamaño de la sala; el usuario
eligió el porcentaje).

## Decisión 5: límite por origen con el rate limiter de ASP.NET Core sobre la creación

**Decisión**: se reutiliza el limitador de ventana fija particionado por `RemoteIpAddress` de la 006, ahora
como política `CreacionDeCarritos`, aplicado **solo** a `POST /api/funciones/{funcionId}/carritos`.
Configuración: `Carritos:CreacionesPorOrigen` (10) y `Carritos:VentanaCreacionMinutos` (10). El rechazo es
`429 demasiados-pedidos` con `Retry-After`, y el mensaje pasa a ser genérico (ya no menciona códigos).

**Rationale**: FR-021. Es una defensa de borde, no una regla de negocio, así que vive en la `Api`. Cuenta
intentos, no carritos creados con éxito: así, probar asientos al azar también consume el límite (la spec se
ajustó en ese sentido).

**Limitaciones que se mantienen de la 006 y se documentan**: el contador es por proceso (con varias
instancias, el límite efectivo se multiplica) y, detrás de un proxy, hace falta `ForwardedHeaders` para que
la IP sea la del cliente. Clientes que comparten una IP comparten el límite.

**Alternativas descartadas**: contar en la base los carritos creados por origen (persistiría IPs, un dato
personal que hoy no se guarda, para una defensa que el tope global ya acota) y particionar por
token (no hay token antes de crear).

## Decisión 6: punto de extensión anti-bots como puerto de `Application`

**Decisión**: puerto `IDesafioAntiBots` con
`Task<bool> Verificar(string? respuesta, string? origen, CancellationToken)`. El pedido de creación acepta un
campo opcional `desafio` (por ejemplo, el token de un widget de CAPTCHA). La implementación registrada es
`DesafioAntiBotsDesactivado`, que siempre aprueba. `CrearCarritoCommandHandler` lo invoca **antes** de abrir
la transacción. Si rechaza, responde `403 desafio-rechazado` y no crea nada.

**Rationale**: FR-023. Verificar un CAPTCHA real es una llamada de red, y el Principio VI prohíbe hacerla
dentro de la transacción. Al ser un puerto, sumar un proveedor es registrar otra implementación.

**Alternativas descartadas**: un filtro de MVC en la `Api` (la decisión de aprobar o rechazar la creación
quedaría fuera del caso de uso y no se podría testear como tal) y un middleware (mismo problema, y
aplicaría a rutas que no corresponden).

## Decisión 7: eventos del outbox sin datos del comprador

**Decisión**: en la misma transacción que el cambio se registran `CarritoCreado(CarritoId, FuncionId,
Asientos, VenceEn, OcurridoEn, PrecioUnitario, Total)`, `SeleccionReemplazada(CarritoId, FuncionId,
AsientosLiberados, AsientosRetenidos, VenceEn, OcurridoEn)` y `CarritoAbandonado(CarritoId, FuncionId,
AsientosLiberados, OcurridoEn)`. Indicar el email no genera evento (no tiene efecto externo ni cambia la
ocupación). El vencimiento tampoco, igual que en la 004: se deriva del reloj.

**Rationale**: Principio VI. Los eventos describen cambios de ocupación, que es lo que un hub en tiempo real
necesita. Se elimina la limitación conocida de la 006: el email ya no viaja en el payload del outbox.

## Decisión 8: email de entrega como dato opcional del carrito; `IEnviadorDeEmail` pasa a ser genérico

**Decisión**: `Carrito.EmailEntrega` es un `EmailComprador?` (el Value Object de la 006: normaliza y valida
formato). Se fija con `Carrito.IndicarEmailEntrega(email, ahora)`, que solo se permite con el carrito
activo, no toca estado ni vencimiento y se puede repetir. El puerto `IEnviadorDeEmail` se mueve a
`Application/Compartido/Ports` con un mensaje genérico `MensajeDeEmail(Destinatario, Asunto, Cuerpo)` y
conserva sus dos implementaciones (registrador en Development y Testing, `NoConfigurado` en el resto). En esta
feature nadie lo invoca: lo usará la feature de pago para enviar la entrada.

**Rationale**: FR-017 y FR-018. El usuario pidió conservar el puerto y el Value Object. Un mensaje genérico
evita dejar un tipo de "verificación" que ya no existe.

## Decisión 9: migración única que descarta las reservas y retira la verificación

**Decisión**: una sola migración nueva `ReemplazarReservasPorCarritos` (las de la 006 ya están en master y no
se reescriben). Hace lo siguiente:
1. Aplica la **misma guarda** que `AddCompradorEmailEnReservas`: si hay filas en `Reservas` y
   `CINEMA_PERMITIR_DESCARTE_RESERVAS` no vale `true`, aborta con `RAISE EXCEPTION`.
2. Borra los mensajes del outbox de tipo `ReservaCreada` y `ReservaCancelada`.
3. Elimina `ReservaAsientos`, `Reservas` y `VerificacionesEmail`.
4. Crea `Carritos` (`Id`, `FuncionId` FK restrict, `Estado`, `CreadoEn`, `VenceEn`, `AbandonadoEn`,
   `EmailEntrega` nullable, `PrecioUnitario_Monto`, `PrecioUnitario_Moneda`, índice
   `(FuncionId, Estado, VenceEn)`) y `CarritoAsientos` (clave `(CarritoId, Fila, Numero)`, cascada desde el
   carrito).

`Down` recrea `Reservas`, `ReservaAsientos` y `VerificacionesEmail` vacías, con el esquema de la 006.

**Rationale**: las reservas existentes tienen un email como dueño y no tienen token: nadie podría operarlas
(FR-024). Como se descartan, recrear las tablas es más simple y más claro que renombrar columnas una por una,
y no pierde nada que no se descarte igual. La guarda conserva el criterio de la 006: nunca borrar datos en
silencio. `Development` sigue definiendo la variable al arrancar.

**Alternativas descartadas**: renombrar `Reservas` a `Carritos` columna por columna (más pasos sin beneficio,
porque los datos se descartan) y reescribir las migraciones de la 006 (ya están en master y pueden estar
aplicadas en bases de desarrollo).

## Decisión 10: 401 sin credencial válida, 404 con un token ajeno

**Decisión**: los endpoints del carrito exigen la política `PortadorDeCarrito`. Sin token, o con uno
alterado, vencido o de otro emisor, responden `401` antes de llegar al caso de uso, para cualquier id. Con
un token válido, el caso de uso compara el id de la ruta con el claim `carrito`: si difieren, responde
`404 carrito-no-encontrado` **sin consultar la base**, exactamente igual que un id inexistente.

**Rationale**: decisión del usuario (sesión 2026-10-28). Ninguna de las dos respuestas revela si el carrito
existe (FR-019, SC-005). La comparación vive en `Application`, no en el controller (Principio VIII).

## Decisión 11: tests y límites configurables en la factory

**Decisión**: `ReservasApiFactory` pasa a ser `CarritosApiFactory` y sube `Carritos:CreacionesPorOrigen` a
1000, igual que la 006 subía `SolicitudesPorOrigen`, porque todos los pedidos de `TestServer` comparten
origen. El tope global queda en su valor real (50%). Los tests de concurrencia usan pocos asientos de salas
grandes, y los del tope usan la sala Sur 1 (40 asientos, tope 20). Una factory derivada baja el límite por
origen para su test.

**Rationale**: los tests de concurrencia necesitan más de 10 creaciones desde el mismo "origen". Los valores
de negocio no se tocan para no esconder bugs del tope.
