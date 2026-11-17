# Research: Pago del Carrito con Mercado Pago

Decisiones técnicas de la feature 008. Cada una cita los requisitos de [spec.md](./spec.md) que resuelve.

## Decisión 1 — SDK oficial de Mercado Pago, solo en `Infrastructure`

**Decision**: usar el paquete NuGet `mercadopago-sdk` 3.7.1 (oficial, soporta .NET 8 y .NET Standard 2.1, compatible
con .NET 10). Lo referencia únicamente `CinemaSystemRandomPlay.Infrastructure`. Se usan `PreferenceClient` (crear la
preferencia), `PaymentClient` (consultar un pago y buscar pagos por `external_reference`) y `PaymentRefundClient`
(devolución total). Cada llamada recibe un `RequestOptions` con el access token y, cuando aplica, `X-Idempotency-Key`.

**Rationale**: el SDK resuelve serialización, reintentos de red y nombres de campos. El Principio I prohíbe que
`Domain` o `Application` lo vean: lo esconde el puerto `IProcesadorDePagos` (Decisión 2). El access token se pasa por
`RequestOptions` en vez de la configuración global estática `MercadoPagoConfig.AccessToken`, para no depender de estado
global y poder testear.

**Alternatives considered**: llamar a la API REST con `HttpClient` propio (más código y más superficie de error, sin
ventaja); Stripe (la constitución lo nombra, pero el usuario eligió Mercado Pago, ver "Complexity Tracking" en el plan).

## Decisión 2 — Puerto `IProcesadorDePagos` con implementación real y simulada

**Decision**: puerto en `Application/Pagos/Ports/IProcesadorDePagos.cs`:

- `CrearPago(SolicitudDePago)` → `PagoCreado(PreferenciaId, UrlPago)`. La solicitud lleva carrito, ítems (uno por
  asiento con su precio unitario), total, moneda, vencimiento y email del pagador.
- `ObtenerPago(string pagoId)` → `PagoConsultado?` (`Id`, `Estado`, `Monto`, `Moneda`, `ReferenciaExterna`,
  `AprobadoEn`).
- `BuscarPagosDelCarrito(Guid carritoId)` → lista de `PagoConsultado` (búsqueda por `external_reference`, FR-017b).
- `Devolver(string pagoId, Guid claveIdempotencia)` → devolución total.

`EstadoPagoExterno` es un enum propio (`Aprobado`, `Rechazado`, `Pendiente`, `Devuelto`, `Otro`); el adaptador traduce
los textos de Mercado Pago (`approved`, `rejected`, `pending`, `in_process`, `refunded`, `cancelled`, …).

Implementaciones en `Infrastructure/Pagos/`:

- `ProcesadorDePagosMercadoPago`: usa el SDK. Devuelve `sandbox_init_point` como `UrlPago` (credenciales de prueba).
- `ProcesadorDePagosSimulado`: guarda preferencias y pagos en memoria (`Singleton`, `ConcurrentDictionary`). Expone
  `RegistrarPago(...)` para que los tests y el endpoint de desarrollo creen pagos aprobados, rechazados, pendientes, con
  otro monto o con otra hora de aprobación. Registra las devoluciones pedidas para que los tests las verifiquen.

Selección por configuración `MercadoPago:Modo` (`Simulado` | `Real`). Por defecto `Simulado` en Development y Testing.
Con `Real`, al arrancar se valida que existan `AccessToken`, `ClaveSecretaAvisos` y `UrlAvisos`.

**Rationale**: FR-029 exige poder operar sin Mercado Pago real; el Principio IV exige el puerto. Que la simulación viva
detrás del mismo puerto hace que los tests de integración recorran el mismo código de negocio que producción.

**Alternatives considered**: WireMock con la API de Mercado Pago grabada (frágil ante cambios de formato y más pesado);
mocks de NSubstitute en integración (no permiten simular el flujo de punta a punta desde HTTP).

## Decisión 3 — Preferencia de Checkout Pro

**Decision**: la preferencia se crea con:

- `items`: uno por asiento (`title` "Película — Sala — Fila C Asiento 5", `quantity` 1, `unit_price` el precio unitario
  congelado, `currency_id` "ARS"). La suma es el total del carrito (FR-008).
- `external_reference`: el id del carrito.
- `payer.email`: el email de entrega (solo como sugerencia en la página de pago).
- `back_urls.success|failure|pending`: `MercadoPago:UrlRetorno` con `{carritoId}` reemplazado. `auto_return =
  "approved"` solo si la URL es `https` (Mercado Pago rechaza `auto_return` con URLs locales).
- `notification_url`: `MercadoPago:UrlAvisos` (con `?source_news=webhooks` para recibir solo webhooks, no IPN).
- `expires = true`, `expiration_date_from = ahora`, `expiration_date_to = vencimiento extendido del carrito` (FR-006).
- `payment_methods.excluded_payment_types = [ticket, atm]` (efectivo y cajero, FR-007), `installments = 1`.
- `binary_mode = true`: Mercado Pago aprueba o rechaza en el momento, sin dejar pagos `in_process`. Refuerza FR-007.

**Rationale**: cumple el flujo 1 y 3 de la spec. `binary_mode` reduce el caso "pendiente" a lo mínimo; si igual
aparece, FR-016 lo cubre.

**Alternatives considered**: `auto_return` siempre (rompe el desarrollo local con `http://localhost`).

## Decisión 4 — Iniciar el pago: llamada de red fuera de la transacción

**Decision**: `IniciarPagoCommandHandler` hace tres pasos.

1. **Lectura sin bloqueo**: carga el carrito. Si ya está en `Pagando`, devuelve su `UrlPago` sin tocar nada (FR-005).
   Si no está `Activo` o le falta el email, rechaza (FR-002). Calcula el vencimiento extendido con
   `Carrito.VencimientoAlIniciarPago(inicioFuncion)` = `min(VenceEn + 1 min, inicio)`.
2. **Llamada a Mercado Pago, fuera de toda transacción** (Principio VI): crea la preferencia con ese vencimiento, los
   asientos y el total leídos. Si falla, responde `503 pago-no-disponible` y el carrito no cambió (FR-004).
3. **Transacción corta**: bloquea la función (mismo orden que la 007), recarga el carrito y llama a
   `Carrito.IniciarPago(preferenciaId, urlPago, asientosEsperados, ahora, inicioFuncion)`. El dominio vuelve a verificar
   que siga `Activo`, que la selección sea la misma que se envió a Mercado Pago y aplica el vencimiento extendido.
   - Si entre 1 y 3 otro pedido ya lo pasó a `Pagando`, devuelve la `UrlPago` guardada (la preferencia nueva queda
     huérfana y vence sola; un pago en ella referencia el mismo carrito y total, así que las reglas de FR-015 lo tratan
     igual que uno de la preferencia guardada).
   - Si la selección cambió, responde `409 carrito-modificado` para que el cliente reintente.
   - Si venció, `409 carrito-no-activo`.

**Rationale**: el Principio VI prohíbe llamadas de red dentro de una transacción; el bloqueo de la función no se puede
sostener durante la llamada. Verificar la selección dentro de la transacción garantiza que lo que cobra Mercado Pago es
exactamente el carrito.

**Alternatives considered**: crear la preferencia desde el Outbox y que el cliente consulte la URL después (agrega una
espera sin necesidad, el usuario necesita la URL ya); bloquear el carrito con una marca "iniciando" en una primera
transacción (más estados intermedios y limpieza si el proceso cae).

## Decisión 5 — Estados del carrito y retención

**Decision**: `EstadoCarrito` suma `Pagando` y `Pagado`. Estado efectivo (`Carrito.EstadoEn`):

- `Abandonado` y `Pagado` guardados → se devuelven tal cual (finales).
- `Activo` o `Pagando` guardados con `VenceEn <= ahora` → `Expirado`.
- En otro caso, el guardado.

`RetieneAsientosEn(ahora)` es verdadero para `Activo` y `Pagando` vigentes y para `Pagado` siempre (FR-010, FR-023). El
repositorio y `CarritosRetienenAsientos` pasan a filtrar `(Estado IN (Activo, Pagando) AND VenceEn > ahora) OR Estado =
Pagado`. El índice `(FuncionId, Estado, VenceEn)` sigue sirviendo.

`ReemplazarAsientos`, `Abandonar` e `IndicarEmailEntrega` rechazan `Pagando` con un resultado propio `CarritoEnPago`
(`409 carrito-en-pago`, FR-009), distinto de `carrito-no-activo`.

**Rationale**: el vencimiento sigue siendo un dato, no un proceso: el estado `Expirado` se calcula, igual que en la 007
(FR-011). `Pagado` retiene para siempre sin depender de `VenceEn`.

**Alternatives considered**: tabla de "asientos vendidos" separada de los carritos (duplica la fuente de verdad de la
ocupación; la tabla `Entradas` cumple ese rol como red de seguridad, ver Decisión 8).

## Decisión 6 — Confirmación del pago: regla en el dominio, idempotencia en la base

**Decision**: un único caso de uso `ProcesarPagoCommandHandler(pagoId)`, que usan el aviso (Decisión 7) y la consulta del
carrito (Decisión 9).

1. **Fuera de transacción**: `IProcesadorDePagos.ObtenerPago(pagoId)`. Si no existe, o su `ReferenciaExterna` no es el
   id de un carrito existente, termina sin cambios y lo registra en el log (FR-018). Si no está `Aprobado`, termina sin
   cambios (FR-016).
2. **Transacción**: bloquea la función del carrito, carga el carrito y los carritos que retienen asientos de esa función,
   y verifica si el pago ya figura en `PagosProcesados` (si sí, termina: idempotencia, FR-017).
3. El domain service `ConfirmadorDePagos.Decidir(carrito, otrosCarritos, pago, ahora)` devuelve `Acreditar` o
   `Devolver(motivo)` aplicando FR-015 y FR-019 en este orden:
   - carrito `Pagado` → `Devolver(CarritoYaPagado)`;
   - carrito no `Pagando` (Activo o Abandonado guardados) → `Devolver(PagoTardio)`;
   - monto o moneda distintos del total → `Devolver(MontoDistinto)`;
   - `AprobadoEn > VenceEn` → `Devolver(PagoTardio)`;
   - carrito ya vencido y algún asiento retenido o vendido por otro carrito → `Devolver(AsientosNoDisponibles)`;
   - en otro caso → `Acreditar`.
4. Con `Acreditar`: `Carrito.MarcarPagado(pagoId, aprobadoEn, ahora)`, se emiten las `Entrada` (Decisión 8) y se registra
   en el Outbox `CarritoPagado` (dispara el email). Con `Devolver`: se crea una `Devolucion` pendiente y se registra
   `DevolucionSolicitada` en el Outbox.
5. En ambos casos se inserta la fila de `PagosProcesados` con PK = id del pago. Si dos procesos compiten, el bloqueo de
   la función los serializa y el segundo encuentra la fila; la PK es la última barrera.

`AprobadoEn` sale de la consulta a Mercado Pago (`date_approved`), nunca del aviso (clarificación 1).

**Rationale**: la decisión es una regla de negocio no trivial (Principio II) y queda testeable sin infraestructura. La
idempotencia por PK resiste avisos duplicados y concurrentes (SC-004).

**Alternatives considered**: guardar el id del aviso (`x-request-id`) en vez del id del pago (Mercado Pago manda varios
avisos distintos por el mismo pago; lo que no se debe repetir es el efecto sobre el pago).

## Decisión 7 — Webhook: firma HMAC y consulta del pago

**Decision**: `POST /api/pagos/mercadopago/avisos`, `[AllowAnonymous]` explícito (FR-013, FR-027). El controller pasa al
caso de uso `RecibirAvisoDePagoCommand(firma, requestId, dataId, tipo)`:

- Lee `x-signature` (`ts=<ms>,v1=<hex>`), `x-request-id` y el query param `data.id` (el `type` puede venir en query o en
  el cuerpo).
- `IVerificadorDeAvisos` (puerto en `Application`, implementación `VerificadorDeAvisosMercadoPago` en
  `Infrastructure`) arma el manifest `id:{data.id en minúsculas};request-id:{x-request-id};ts:{ts};`, omitiendo las
  partes que no vengan, calcula HMAC-SHA256 con `MercadoPago:ClaveSecretaAvisos` en hex y compara con
  `CryptographicOperations.FixedTimeEquals`. Firma ausente o inválida → `401` sin procesar.
- Tipo distinto de `payment` → `200` sin procesar (FR-018).
- Tipo `payment` → `ProcesarPagoCommandHandler(data.id)`. Si Mercado Pago no responde al consultar el pago → `500`, para
  que Mercado Pago reintente el aviso. En cualquier otro caso → `200`.

No se valida antigüedad del `ts`: un aviso válido reenviado solo provoca otra consulta a Mercado Pago, que la
idempotencia vuelve inocua.

En modo `Simulado` el verificador usa la misma clave (de configuración de desarrollo), así que los tests de firma corren
contra el código real.

**Rationale**: es el esquema documentado por Mercado Pago. Aunque la firma se falsificara, la decisión se toma con los
datos consultados a Mercado Pago, nunca con el cuerpo del aviso (FR-014, US5).

**Alternatives considered**: IPN (`topic`/`id`, sin firma; Mercado Pago lo está retirando); validar por IP de origen
(Mercado Pago no publica un rango estable).

## Decisión 8 — Entradas

**Decision**: entidad `Entrada` (tabla `Entradas`), creada por `Carrito.MarcarPagado` como parte del agregado `Carrito`
(una por asiento, FR-022). Guarda una copia de película, sucursal, sala y horario al momento de pagar, además del
asiento y el precio unitario. Código: 10 caracteres del alfabeto Crockford Base32 (sin `I`, `L`, `O`, `U`) generados con
`RandomNumberGenerator`, presentados como `XXXXX-XXXXX`. 50 bits de azar: no adivinable. Índice único sobre `Codigo` e
índice único sobre `(FuncionId, Fila, Numero)`.

**Rationale**: la copia hace que la entrada y el email no cambien si después se edita una película o una sala. El
índice único por asiento y función es la red de seguridad de la base contra una doble venta, además del bloqueo de la
función. El generador de códigos se inyecta en el dominio como interfaz `IGeneradorDeCodigos` para testear sin azar.

**Alternatives considered**: GUID como código (largo para leer en boletería); secuencial (adivinable).

## Decisión 9 — Consulta del carrito con verificación contra Mercado Pago

**Decision**: `ObtenerCarritoQueryHandler` sigue leyendo sin transacción. Si el carrito guardado está en `Pagando`
(vigente o vencido), primero llama a `IProcesadorDePagos.BuscarPagosDelCarrito(carritoId)` con un timeout de
3 segundos; por cada pago aprobado que no figure en `PagosProcesados`, invoca `ProcesarPagoCommandHandler`. Después lee
y responde. Si Mercado Pago falla o tarda, se registra en el log y se responde el estado conocido sin error (FR-017b).

**Rationale**: resuelve el regreso desde Mercado Pago antes del aviso y el desarrollo sin túnel (clarificación 2). Solo
consulta para carritos en `Pagando`, así que el resto de las consultas no cambia de costo.

**Alternatives considered**: barrido periódico en background (descartado en la clarificación); consultar en cada GET sin
importar el estado (llamadas inútiles).

## Decisión 10 — Procesador del Outbox

**Decision**: `ProcesadorDeOutbox` (`BackgroundService` en `Infrastructure/Outbox/`). Cada `Outbox:IntervaloSegundos`
(por defecto 2):

1. Transacción corta: toma hasta 20 mensajes con `ProcesadoEn IS NULL AND FallidoEn IS NULL AND (ProximoIntentoEn IS
   NULL OR ProximoIntentoEn <= now)` con `FOR UPDATE SKIP LOCKED`, les pone `ProximoIntentoEn = now + 5 min` (lease) y
   confirma.
2. Por cada mensaje, fuera de transacción, busca el manejador por `Tipo` (`IManejadorDeMensaje` registrados en el DI) y
   lo ejecuta en su propio scope.
3. Éxito → `ProcesadoEn = now`. Falla → `Intentos++`, `UltimoError`, `ProximoIntentoEn = now + min(2^Intentos × 10 s,
   1 h)`; al llegar a `Outbox:MaximoIntentos` (por defecto 8) → `FallidoEn = now` y log de error.
4. Tipos sin manejador (los eventos de la 004/007, a la espera de SignalR) → `ProcesadoEn = now` sin acción.

Manejadores de esta feature:

- `CarritoPagado` → `EnviarEmailDeEntrega`: lee carrito y entradas, arma el mensaje y llama a `IEnviadorDeEmail`. Si el
  carrito ya tiene `EntregaEnviadaEn`, no reenvía. Al terminar, guarda `EntregaEnviadaEn` (FR-025, FR-026).
- `DevolucionSolicitada` → `EjecutarDevolucion`: llama a `IProcesadorDePagos.Devolver(pagoId, devolucionId)`. La clave
  de idempotencia es el id de la `Devolucion`, así que un reintento nunca devuelve dos veces en Mercado Pago. Marca la
  devolución `Completada`; si se agotan los intentos, el procesador la marca `Fallida` (FR-021).

**Rationale**: el Principio VI exige el publicador y la entrega at-least-once. El lease evita que dos instancias tomen el
mismo mensaje y deja que un mensaje se reintente si el proceso cae a mitad. El único hueco de at-least-once es caer
entre "email aceptado" y "marcar enviado": se acepta y se documenta (ver plan, Complexity Tracking).

**Alternatives considered**: Hangfire o Quartz (dependencia pesada para un solo tipo de trabajo); `Channel` en memoria
(se pierde al reiniciar, rompe el Principio VI).

## Decisión 11 — Email: SMTP con MailKit, Mailpit en desarrollo

**Decision**: `EnviadorDeEmailSmtp` en `Infrastructure/Email/` con MailKit 4.x (versión estable vigente al implementar,
fijada en el `.csproj`). Configuración `Email:Proveedor` (`Registrador` | `Smtp`), `Email:Smtp:Host|Puerto|Usuario|
Clave|UsarTls` y `Email:Remitente`. El cuerpo es texto plano más HTML simple con una fila por entrada.

`docker-compose.yml` suma el servicio `mailpit` (imagen `axllent/mailpit` con versión fijada; SMTP 1025, UI 8025) y el
servicio `api` apunta a él con `Email__Proveedor=Smtp`. Testing sigue usando `EnviadorDeEmailEnMemoria` (ya existe en los
tests) para verificar cantidad y contenido.

**Rationale**: SMTP sirve para Mailpit y también para Resend (expone SMTP), así que el proveedor real queda resuelto por
configuración, sin código nuevo. MailKit es la recomendación de Microsoft frente a `System.Net.Mail.SmtpClient`.

**Alternatives considered**: SDK HTTP de Resend (código específico de un proveedor que la spec deja fuera de alcance).

## Decisión 12 — Endpoint de simulación para desarrollo y pruebas

**Decision**: con `MercadoPago:Modo = Simulado` y entorno Development o Testing, se mapea `POST
/api/dev/pagos-simulados` (`[AllowAnonymous]`, declarado explícito). Recibe `carritoId`, `estado` (`Aprobado`,
`Rechazado`, `Pendiente`) y, opcionalmente, `monto` y `aprobadoEn`. Registra el pago en `ProcesadorDePagosSimulado` y
ejecuta `ProcesarPagoCommandHandler`, igual que si hubiera llegado el aviso. Devuelve el id del pago simulado. La
`UrlPago` del simulador apunta a una página informativa (`/api/dev/pagos-simulados/{preferenciaId}`) que explica cómo
simular el pago. En otros entornos el endpoint no existe (`404`).

**Rationale**: FR-029 y el quickstart sin túnel. Sale por el mismo caso de uso que el aviso real.

**Alternatives considered**: firmar avisos a mano en el quickstart (posible, y se documenta como prueba de la firma, pero
incómodo para el uso diario).

## Decisión 13 — Credenciales y secretos

**Decision**: `MercadoPago:AccessToken` y `MercadoPago:ClaveSecretaAvisos` solo por user-secrets o variables de entorno,
nunca en `appsettings*.json` ni en `docker-compose.yml` (FR-028). El adaptador real usa `sandbox_init_point`. El
README explica cómo obtener credenciales de prueba de un usuario vendedor de prueba y tarjetas de prueba. Los logs
nunca incluyen el token, la clave ni el cuerpo completo de las respuestas de Mercado Pago.

**Rationale**: la spec limita la feature a credenciales de prueba; no hay forma confiable de distinguir por formato una
credencial de prueba de una de producción (las de usuarios de prueba también empiezan con `APP_USR-`), así que el
resguardo es operativo y documental. Se deja como limitación conocida.

**Alternatives considered**: rechazar al arrancar credenciales que no empiecen con `TEST-` (rompería las credenciales de
usuarios de prueba, que es el método recomendado hoy).

## Decisión 14 — Enmienda de la constitución

**Decision**: antes de implementar se enmienda la constitución a v4.1.0 (MINOR): Mercado Pago reemplaza a Stripe como
pasarela en el Principio I, el Principio IV (ejemplos) y "Restricciones de Plataforma y Tecnología", y se nombra el
procesador del Outbox como componente existente. Se hace con `/speckit-constitution` como primera tarea.

**Rationale**: la Governance exige que una desviación no quede en silencio; cambiar la integración nombrada es una
ampliación material de una sección.

## Ajustes durante la implementación

- **MailKit 5.0.0** en lugar de 4.x: es la versión estable vigente. En la 5, `SmtpClient.SendAsync` recibe un
  `SmtpSendRequest`.
- **`Microsoft.Extensions.Hosting.Abstractions` 10.0.12** en `Infrastructure`, para el `BackgroundService` del Outbox
  (fijada en 10.0.x para no mezclar con paquetes de .NET 11).
- **`Entrada` como entidad propia** (tabla `Entradas` con FK a `Carritos`, borrado en cascada) en lugar de un tipo
  `OwnsMany`, y con `Fila` y `Numero` como columnas propias: el índice único `(FuncionId, Fila, Numero)` necesita
  columnas simples.
- **Tope global por función**: cuenta solo los carritos `Activo` y `Pagando` vigentes. Los asientos de carritos
  `Pagado` no están disponibles, pero no cuentan para el tope; si contaran, una sala no podría vender más de la mitad.
- **Sin logging en `Application`**: los casos de uso devuelven resultados explícitos (`ResultadoProcesamientoPago`,
  `ResultadoAviso`) y el registro en el log lo hacen el controller del webhook y el procesador del Outbox.
- **`IProcesadorDePagos` se resuelve según `MercadoPago:Modo` al pedirlo**, no al arrancar, para que la configuración
  de cada entorno (y de los tests) decida la implementación.
- **El webhook lee el cuerpo a mano** y solo si el tipo o el id no vinieron en el query, para que un aviso sin
  cuerpo o sin `Content-Type` llegue igual a la validación de la firma (`401`, no `415`).
- **`SolicitudDePago` lleva `Desde`** (`expiration_date_from`), además del vencimiento.
- **`ClaveSecretaAvisos` solo es obligatoria en modo `Real`**: en modo simulado es opcional y, sin ella, el endpoint de
  avisos rechaza todo con `401`. Exigirla siempre impedía arrancar en entornos que no usan pagos.
- **La hora de la función en el email** se muestra en la zona local del sistema, la misma convención que usa la consulta
  de funciones de hoy (`FuncionRepository`). En un servidor en UTC se ve en UTC; para mostrar la hora de Argentina el
  proceso tiene que correr con `TZ=America/Argentina/Buenos_Aires`.
