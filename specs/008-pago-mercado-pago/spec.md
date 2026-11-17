# Feature Specification: Pago del Carrito con Mercado Pago (credenciales de prueba)

**Feature Branch**: `008-pago-mercado-pago`

**Created**: 2026-11-16

**Status**: Draft

**Input**: User description: "Pago del Carrito con Mercado Pago (credenciales de prueba). El comprador anónimo paga su carrito activo con Mercado Pago Checkout Pro y recibe sus entradas por email. Se usan exclusivamente credenciales de prueba: el dinero es ficticio y ninguna tarjeta real puede ser cobrada. El pago ocurre en la página de Mercado Pago; la API nunca recibe ni guarda datos de tarjeta. Flujo: iniciar el pago crea una preferencia y devuelve la URL de pago; el carrito pasa a Pagando y su vencimiento se extiende 1 minuto sin superar el inicio de la función; la preferencia vence al mismo momento que el carrito; Mercado Pago notifica por webhook, la API consulta el pago y, si está aprobado y el carrito sigue en Pagando y vigente, el carrito pasa a Pagado, se emiten las entradas y se encola el email. Reglas: Pagando es de un solo uso; en Pagando no se cambia la selección ni se abandona; se excluyen medios de pago no instantáneos; un pago rechazado deja el carrito en Pagando; el webhook valida firma, consulta el pago y es idempotente; pagos tardíos o con monto distinto se reembolsan automáticamente; consultar el carrito distingue pago en proceso de pagado; una entrada por asiento con código único; email por Outbox con reintentos, Mailpit en Development; Mercado Pago como puerto nuevo con implementación real y simulada. Fuera de alcance: QR, panel de administración, reembolsos pedidos por el comprador, cambio de los 5 minutos de retención y credenciales de producción."

## Clarifications

### Session 2026-11-16

- Q: Si Mercado Pago aprobó el pago antes del vencimiento del carrito pero el aviso llega después, ¿qué cuenta? → A: La hora de aprobación informada por Mercado Pago (consultada a Mercado Pago, no tomada del aviso). Si se aprobó a tiempo y los asientos siguen libres, se paga; si algún asiento ya lo tomó otro carrito, se devuelve.
- Q: Si el aviso de Mercado Pago se demora o nunca llega, ¿la API busca el estado del pago por su cuenta? → A: Sí, al consultar el carrito: si está en "pagando" (vigente o recién vencido), la API pregunta a Mercado Pago por pagos de ese carrito y procesa cada uno igual que un aviso. No hay barrido periódico en segundo plano.
- Q: Mientras el carrito está en "pagando", ¿se puede corregir el email de entrega? → A: No. Desde que se inicia el pago el email queda fijo, igual que los asientos (FR-009).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pagar el carrito y quedarme con mis asientos (Priority: P1)

Una persona con un carrito activo, que ya indicó su email de entrega, decide pagar. El sistema le devuelve un enlace a la página de pago de Mercado Pago y le da un minuto extra de retención para completar el pago. La persona paga en esa página con una tarjeta de prueba. Cuando Mercado Pago avisa que el pago se aprobó, el sistema lo confirma consultándolo por su cuenta, marca el carrito como pagado y emite una entrada por cada asiento. Los asientos quedan vendidos para siempre: ya no vencen.

**Why this priority**: Es el objetivo de la feature. Sin esto el carrito retiene asientos que nunca se pueden comprar. Entrega valor solo: aunque el email no llegue, la compra queda registrada y los asientos vendidos.

**Independent Test**: Con el procesador de pagos simulado, se crea un carrito con email, se inicia el pago, se simula un aviso de pago aprobado por el total exacto y se verifica que el carrito queda pagado, que hay una entrada por asiento y que los asientos no vuelven a estar disponibles después del vencimiento original.

**Acceptance Scenarios**:

1. **Given** un carrito activo con C5 y C6, email de entrega y vencimiento a las 20:05 en una función que empieza a las 21:00, **When** su portador inicia el pago a las 20:03, **Then** recibe la URL de pago, el carrito pasa a "pagando" y su vencimiento pasa a 20:06.
2. **Given** un carrito activo que vence a las 20:05 en una función que empieza a las 20:05:30, **When** su portador inicia el pago, **Then** el vencimiento pasa a 20:05:30 (el inicio de la función), no a 20:06.
3. **Given** un carrito en "pagando", **When** se crea el pago en Mercado Pago, **Then** la página de pago muestra los asientos con el total del carrito, ofrece solo medios de pago instantáneos y deja de aceptar pagos en el mismo instante en que vence el carrito.
4. **Given** un carrito en "pagando" y vigente, **When** llega un aviso válido de un pago aprobado por el total exacto del carrito, **Then** el carrito pasa a "pagado", se emite una entrada por asiento y queda pendiente el envío del email de entrega.
5. **Given** un carrito "pagado", **When** pasa su vencimiento, **Then** el carrito sigue "pagado" y sus asientos siguen sin figurar como disponibles en el mapa.
6. **Given** un carrito "pagado", **When** su portador lo consulta con el token, **Then** ve el estado "pagado" y sus entradas, cada una con su código, película, sucursal, sala, horario, asiento y precio unitario.

---

### User Story 2 - Volver de Mercado Pago y saber en qué quedó mi compra (Priority: P1)

Después de pagar, Mercado Pago devuelve a la persona al sitio, a veces antes de que el aviso de pago haya llegado al sistema. La persona consulta su carrito y necesita distinguir "todavía estamos confirmando tu pago" de "ya está pagado" o de "venció sin pago", para que la pantalla espere y se actualice en lugar de mostrar un error.

**Why this priority**: Sin esto, la persona que acaba de pagar ve un estado ambiguo y puede creer que perdió el dinero o pagar dos veces. Es parte del recorrido mínimo de compra.

**Independent Test**: Se inicia el pago, se consulta el carrito antes de simular el aviso (debe decir "pagando"), se simula el aviso aprobado y se consulta otra vez (debe decir "pagado"). En otro carrito se deja pasar el vencimiento sin aviso y se verifica "expirado".

**Acceptance Scenarios**:

1. **Given** un carrito en "pagando" y vigente sin aviso de pago aprobado, **When** su portador lo consulta, **Then** ve el estado "pagando" con su vencimiento y la misma URL de pago, y ninguna entrada.
2. **Given** un carrito en "pagando" que recibió un aviso de pago aprobado, **When** su portador lo consulta, **Then** ve "pagado" con sus entradas.
3. **Given** un carrito en "pagando" cuyo vencimiento pasó sin un pago aprobado, **When** su portador lo consulta, **Then** ve "expirado" y sus asientos figuran disponibles en el mapa.
4. **Given** un carrito en "pagando", **When** su portador vuelve a iniciar el pago, **Then** recibe la misma URL de pago y el vencimiento no cambia.
5. **Given** un carrito en "pagando" con un pago aprobado en Mercado Pago cuyo aviso todavía no llegó (o nunca llega), **When** su portador consulta el carrito, **Then** el sistema pregunta a Mercado Pago, procesa ese pago igual que un aviso y la consulta ya muestra "pagado" con sus entradas.
6. **Given** el caso anterior, **When** después llega el aviso de ese mismo pago, **Then** no se emiten entradas, emails ni devoluciones de nuevo.

---

### User Story 3 - Recibir mis entradas por email (Priority: P2)

Una vez confirmado el pago, la persona recibe en su email de entrega un mensaje con sus entradas: código de cada una, película, sucursal, sala, horario, asiento y precio. Si el envío falla, el sistema reintenta, pero la compra no se deshace.

**Why this priority**: Es la forma en que la persona se lleva las entradas, pero la compra ya es válida sin el email (las entradas se pueden consultar con el token), así que se puede entregar después de la User Story 1.

**Independent Test**: En el entorno de desarrollo, con el atrapador de emails levantado, se completa una compra con el procesador simulado y se verifica que llega un único email al email de entrega con todas las entradas. Luego se simula una falla del envío y se verifica que se reintenta y que el carrito sigue "pagado".

**Acceptance Scenarios**:

1. **Given** un carrito que acaba de pasar a "pagado" con email `ana@example.com` y dos asientos, **When** se procesa el envío pendiente, **Then** `ana@example.com` recibe un email con las dos entradas y sus datos.
2. **Given** un envío de email que falla, **When** se reintenta más tarde, **Then** el email se envía en un reintento posterior y el carrito permaneció "pagado" todo el tiempo.
3. **Given** un aviso de pago aprobado que llega dos veces, **When** se procesan ambos, **Then** se envía un solo email de entrega.

---

### User Story 4 - Que un pago tardío o incorrecto no se quede con el dinero (Priority: P2)

Puede pasar que Mercado Pago apruebe un pago cuando el carrito ya venció, o que el monto aprobado no coincida con el total del carrito. En esos casos el sistema no entrega entradas: devuelve el pago completo automáticamente y deja registro de la devolución.

**Why this priority**: Protege al comprador de pagar sin recibir nada y al cine de vender asientos que ya liberó. Es un caso borde, pero un error acá es caro y silencioso.

**Independent Test**: Con el procesador simulado, se deja vencer un carrito en "pagando" y se simula un aviso de pago aprobado; se verifica que el carrito no pasa a "pagado", que se pidió la devolución del pago una sola vez y que queda registrada. Se repite con un pago aprobado por un monto distinto del total.

**Acceptance Scenarios**:

1. **Given** un carrito en "pagando" que venció a las 20:06, **When** llega un aviso de un pago aprobado a las 20:07, **Then** el carrito queda "expirado", no se emiten entradas ni email, y el pago se devuelve completo y queda registrado con el motivo "pago tardío".
2. **Given** un carrito en "pagando" que venció a las 20:06, **When** a las 20:06:20 llega el aviso de un pago aprobado a las 20:05:50 y ningún otro carrito tomó sus asientos, **Then** el carrito pasa a "pagado" y se emiten sus entradas.
3. **Given** el caso anterior pero con uno de sus asientos ya retenido o vendido por otro carrito, **When** se procesa el aviso, **Then** el carrito queda "expirado" y el pago se devuelve con el motivo "asientos no disponibles".
4. **Given** un carrito que ya está "pagado" con el pago P1, **When** llega un aviso de otro pago aprobado P2 para el mismo carrito, **Then** P2 se devuelve completo y se registra; el carrito y sus entradas no cambian.
5. **Given** un carrito en "pagando" y vigente con total $9.000, **When** llega un aviso de un pago aprobado por $8.000, **Then** el carrito no pasa a "pagado", el pago se devuelve y se registra con el motivo "monto distinto".
6. **Given** un pago que ya se devolvió, **When** llega otra vez el aviso de ese pago, **Then** no se pide una segunda devolución.
7. **Given** un pedido de devolución que falla al comunicarse con Mercado Pago, **When** se reintenta, **Then** la devolución se completa en un reintento posterior y se registra una sola vez.

---

### User Story 5 - Que nadie pueda fingir un pago (Priority: P1)

El aviso de pago llega por un canal público. El sistema no puede confiar en su contenido: alguien podría mandar un aviso falso diciendo "este carrito está pagado". El sistema solo acepta avisos con una firma válida y, aun así, consulta el estado real del pago directamente a Mercado Pago antes de hacer nada.

**Why this priority**: Sin esto cualquiera obtiene entradas gratis. Es inseparable de la User Story 1.

**Independent Test**: Se envía un aviso sin firma, con firma alterada y con firma de otra clave, y se verifica que se rechazan sin cambios. Se envía un aviso con firma válida cuyo pago, consultado a Mercado Pago, está rechazado, y se verifica que el carrito sigue en "pagando".

**Acceptance Scenarios**:

1. **Given** un carrito en "pagando", **When** llega un aviso sin firma, con firma inválida o con una marca de tiempo alterada, **Then** se rechaza y nada cambia.
2. **Given** un aviso con firma válida que dice "aprobado", **When** el sistema consulta el pago a Mercado Pago y este figura rechazado o pendiente, **Then** el carrito sigue en "pagando" y no se emite nada.
3. **Given** un aviso válido de un pago rechazado, **When** se procesa, **Then** el carrito sigue en "pagando" hasta su vencimiento y la persona puede reintentar con otra tarjeta en la misma página de pago.
4. **Given** un aviso válido de un pago cuyo carrito de referencia no existe o no corresponde a este sistema, **When** se procesa, **Then** se acepta el aviso sin cambiar nada y se registra para diagnóstico.
5. **Given** un aviso válido de un tipo distinto de pago (por ejemplo, de otro recurso de Mercado Pago), **When** llega, **Then** se acepta sin procesarlo.

---

### Edge Cases

- ¿Qué pasa si se intenta iniciar el pago de un carrito sin email de entrega? Se rechaza con un error de negocio explícito ("falta el email de entrega") y el carrito sigue activo sin cambios.
- ¿Qué pasa si se intenta iniciar el pago de un carrito expirado, abandonado o pagado? Se rechaza indicando el estado actual. Un carrito pagado no se vuelve a pagar.
- ¿Qué pasa si se intenta cambiar la selección, cambiar el email de entrega o abandonar un carrito en "pagando"? Se rechaza indicando que el carrito está en proceso de pago. Así los asientos y el total que ve Mercado Pago son siempre los del carrito.
- ¿Qué pasa si la creación del pago en Mercado Pago falla (por ejemplo, Mercado Pago no responde)? El inicio del pago se rechaza con un error indicando que el pago no está disponible por el momento; el carrito sigue "activo", sin extensión de vencimiento, y la persona puede reintentar.
- ¿Qué pasa si dos pedidos de iniciar el pago del mismo carrito llegan a la vez? Se procesan uno detrás del otro: el carrito entra a "pagando" una sola vez, el vencimiento se extiende una sola vez y ambos reciben la misma URL de pago.
- ¿Qué pasa si el carrito vence mientras la persona está en la página de Mercado Pago? Mercado Pago deja de aceptar el pago en ese instante. Si aun así se aprueba un pago después del vencimiento (por desfase de relojes o un pago que quedó en revisión), se devuelve como pago tardío.
- ¿Qué pasa si el aviso aprobado llega justo cuando vence el carrito o poco después? Cuenta la hora de aprobación que informa Mercado Pago al consultarle el pago, no la hora de llegada del aviso: si se aprobó a tiempo y los asientos siguen libres, se paga; si no, se devuelve. Nunca se emiten entradas y se devuelve el mismo pago.
- ¿Qué pasa si un pago queda pendiente o en revisión? El carrito sigue en "pagando". Si se aprueba antes del vencimiento, se paga; si se aprueba después, se devuelve; si nunca se aprueba, el carrito expira.
- ¿Qué pasa si llegan dos avisos del mismo pago aprobado a la vez? Se procesan uno detrás del otro: uno solo genera entradas y email; el otro no hace nada.
- ¿Qué pasa si el email de entrega no existe o rebota? El sistema no lo verifica: el envío se da por hecho cuando el servicio de email lo acepta. La compra no se deshace y las entradas siguen consultables con el token.
- ¿Qué pasa si se agotan los reintentos del email? Queda registrado como envío fallido para diagnóstico; la compra sigue válida.
- ¿Qué pasa si el aviso de pago nunca llega? Si el portador consulta el carrito, el sistema le pregunta a Mercado Pago y procesa el pago igual que un aviso. Si nadie consulta y el aviso no llega, el carrito vence y libera los asientos; si más tarde aparece el pago (por un aviso o una consulta), se aplica FR-015: se paga si se aprobó a tiempo y los asientos siguen libres, y si no, se devuelve.
- ¿Qué pasa si Mercado Pago no responde cuando se consulta el carrito? La consulta responde con el estado que el sistema ya conoce ("pagando" o "expirado") sin error; se vuelve a preguntar en la próxima consulta.
- ¿Qué pasa si se usan credenciales de producción por error? Es un error de configuración fuera del contrato de esta feature: solo se documentan y configuran credenciales de prueba, y ninguna configuración versionada contiene credenciales (ver FR-028).

## Requirements *(mandatory)*

### Functional Requirements

**Iniciar el pago**

- **FR-001**: El portador de un carrito DEBE poder iniciar su pago. El sistema DEBE crear un pago en Mercado Pago con los asientos del carrito, el total, la referencia al carrito, las direcciones de vuelta al sitio y la dirección de aviso, y DEBE devolver la URL de la página de pago.
- **FR-002**: Iniciar el pago DEBE exigir que el carrito esté "activo" y tenga email de entrega. Si falta el email, DEBE rechazarse con un código de error de negocio propio. Si el carrito está expirado, abandonado o pagado, DEBE rechazarse indicando el estado actual.
- **FR-003**: Al iniciar el pago con éxito, el carrito DEBE pasar de "activo" a "pagando" y su vencimiento DEBE pasar a lo que ocurra primero entre el vencimiento que tenía más 1 minuto y el inicio de la función. La retención base de 5 minutos no cambia.
- **FR-004**: Si crear el pago en Mercado Pago falla, el carrito NO DEBE cambiar de estado ni de vencimiento, y el pedido DEBE rechazarse con un error que indique que el pago no está disponible por el momento.
- **FR-005**: Iniciar el pago de un carrito en "pagando" DEBE devolver la misma URL de pago sin crear otro pago en Mercado Pago y sin modificar el vencimiento. El estado "pagando" se alcanza una sola vez por carrito.
- **FR-006**: El pago creado en Mercado Pago DEBE vencer exactamente en el vencimiento del carrito ya extendido, de modo que Mercado Pago rechace pagos posteriores.
- **FR-007**: El pago creado DEBE excluir los medios de pago no instantáneos (efectivo en puntos de pago y cajero), de modo que solo se ofrezcan medios que se aprueban o rechazan en el momento.
- **FR-008**: El monto cobrado DEBE ser el total del carrito en el momento de iniciar el pago, con la moneda del sistema, y con un ítem por asiento a su precio unitario.

**Carrito en "pagando"**

- **FR-009**: En "pagando" NO DEBE poder cambiarse la selección de asientos, cambiar el email de entrega ni abandonarse el carrito; los intentos DEBEN rechazarse indicando que el carrito está en proceso de pago.
- **FR-010**: Un carrito en "pagando" DEBE seguir reteniendo sus asientos hasta su vencimiento, igual que uno activo, y contar para el tope global de asientos retenidos por función.
- **FR-011**: Un carrito en "pagando" cuyo vencimiento pasó sin pago aprobado DEBE considerarse expirado a todos los efectos desde ese instante, y sus asientos disponibles, sin que nadie intervenga.
- **FR-012**: Un pago rechazado NO DEBE cambiar el estado ni el vencimiento del carrito. La persona puede reintentar en la misma página de pago mientras el carrito esté vigente.

**Aviso de pago**

- **FR-013**: El sistema DEBE recibir avisos de Mercado Pago por un canal público, declarado explícitamente como tal, protegido por la validación de la firma del aviso con la clave secreta de avisos. Un aviso sin firma o con firma inválida DEBE rechazarse sin cambiar nada.
- **FR-014**: El sistema NO DEBE confiar en el contenido del aviso más allá del identificador del pago: DEBE consultar a Mercado Pago el estado, el monto y la referencia del carrito de ese pago, y decidir con esos datos.
- **FR-015**: Si el pago consultado está aprobado, su monto es igual al total del carrito, el carrito de referencia está en "pagando", la hora de aprobación informada por Mercado Pago es anterior o igual al vencimiento del carrito y, si el carrito ya venció al procesar el aviso, ninguno de sus asientos está retenido ni vendido por otro carrito, el carrito DEBE pasar a "pagado", DEBEN emitirse sus entradas y DEBE quedar pendiente el envío del email de entrega, todo en una única operación atómica.
- **FR-016**: Si el pago consultado está rechazado, pendiente o en revisión, el sistema NO DEBE cambiar el carrito.
- **FR-017**: El procesamiento de avisos DEBE ser idempotente y seguro ante avisos concurrentes: el mismo pago procesado varias veces NO DEBE generar entradas, emails ni devoluciones duplicadas. Un carrito DEBE pasar a "pagado" a lo sumo una vez.
- **FR-017b**: Al consultar un carrito en "pagando" (vigente o vencido sin pago procesado), el sistema DEBE preguntar a Mercado Pago por los pagos de ese carrito y procesar cada uno con las mismas reglas e idempotencia que un aviso (FR-014 a FR-021). Si Mercado Pago no responde, la consulta DEBE devolver el estado conocido sin error. No hay barrido periódico en segundo plano.
- **FR-018**: Los avisos válidos que no corresponden a un pago, o cuyo pago referencia un carrito inexistente, DEBEN aceptarse sin cambiar nada y registrarse para diagnóstico.

**Devoluciones automáticas**

- **FR-019**: Un pago aprobado DEBE devolverse completo automáticamente cuando no cumple FR-015: aprobado después del vencimiento del carrito, carrito abandonado o ya pagado con otro pago, algún asiento tomado por otro carrito tras el vencimiento, o monto distinto del total del carrito.
- **FR-020**: Cada devolución DEBE registrarse con el pago, el carrito, el monto, el motivo ("pago tardío", "carrito ya pagado", "asientos no disponibles", "monto distinto") y el momento. Un pago se devuelve a lo sumo una vez.
- **FR-021**: La devolución DEBE hacerse fuera de la operación que registra la decisión, con reintentos si Mercado Pago falla, hasta completarse o agotar los reintentos, quedando registrado el resultado.

**Entradas**

- **FR-022**: Al pasar a "pagado", el sistema DEBE emitir exactamente una entrada por asiento del carrito, con un código único en todo el sistema y no adivinable, la película, la sucursal, la sala, el horario de la función, el asiento (fila y número) y el precio unitario congelado del carrito.
- **FR-023**: Los asientos de un carrito "pagado" DEBEN quedar vendidos de forma permanente: no vencen, no se liberan y no figuran como disponibles en el mapa.

**Consulta del carrito**

- **FR-024**: Consultar el carrito con su token DEBE mostrar el estado entre "activo", "pagando", "pagado", "expirado" y "abandonado". En "pagando" DEBE incluir la URL de pago y el vencimiento; en "pagado", las entradas emitidas. Así el cliente puede distinguir pago en proceso de pagado y esperar actualizando; cada consulta en "pagando" verifica el pago con Mercado Pago (FR-017b).

**Email de entrega**

- **FR-025**: El envío del email con las entradas DEBE ocurrir después de confirmar el pago, fuera de la operación que lo confirma, con reintentos ante fallas. Una falla del envío NO DEBE deshacer la compra.
- **FR-026**: Por cada carrito pagado DEBE enviarse a lo sumo un email de entrega exitoso, al email de entrega del carrito, con todas sus entradas.

**Acceso y seguridad**

- **FR-027**: Iniciar el pago y consultar el carrito DEBEN exigir el token del carrito, con las mismas respuestas que la feature 007 ante token ausente, inválido o ajeno. El aviso de pago es la única operación pública nueva.
- **FR-028**: El sistema DEBE operar solo con credenciales de prueba de Mercado Pago. NO DEBE recibir, procesar ni guardar datos de tarjeta: el pago ocurre íntegramente en la página de Mercado Pago. Las credenciales y la clave secreta de avisos NO DEBEN estar en el repositorio ni en respuestas o registros.
- **FR-029**: DEBE existir una forma de operar sin Mercado Pago real (procesador simulado) para pruebas automáticas y para desarrollo local sin un acceso público a la máquina, que permita simular avisos de pago aprobados, rechazados, tardíos y con monto distinto.

### Key Entities

- **Carrito** (cambia): suma los estados "pagando" y "pagado". Transiciones válidas: activo → pagando, activo → expirado, activo → abandonado, pagando → pagado, pagando → expirado. "Pagado", "expirado" y "abandonado" son finales; la única excepción es un carrito en "pagando" ya vencido cuyo pago se aprobó antes del vencimiento (FR-015), que pasa a "pagado" si sus asientos siguen libres. Guarda además el identificador del pago creado en Mercado Pago, la URL de pago, el momento en que se inició el pago y el momento en que se pagó.
- **Pago** : el pago aprobado que pagó el carrito, con su identificador en Mercado Pago, monto y momento de aprobación. Un carrito tiene a lo sumo un pago que lo pagó.
- **Entrada**: derecho de un comprador a un asiento de una función. Atributos: código único, carrito de origen, película, sucursal, sala, horario, asiento y precio unitario congelado. Una por asiento del carrito pagado.
- **Devolución**: devolución automática de un pago aprobado que no pudo pagar un carrito. Atributos: identificador del pago, carrito de referencia, monto, motivo, estado (pendiente, completada, fallida) y momentos de registro y de resultado.
- **Aviso de pago procesado**: registro de qué pagos ya se procesaron, para garantizar la idempotencia.
- **Email de entrega**: mensaje pendiente de envío con las entradas de un carrito pagado, con su estado de envío y reintentos.

## Impacto sobre las features anteriores

La feature 007 cambia en lo siguiente: el carrito suma los estados "pagando" y "pagado"; el vencimiento deja de ser fijo una única vez, al iniciar el pago (+1 minuto, sin superar el inicio de la función); y el email de entrega deja de poder corregirse desde que se inicia el pago. El resto de la 007 (retención de 5 minutos, reemplazo atómico, topes, límite por origen, token) no cambia. El mapa de asientos (003) sigue igual en forma, pero ahora excluye también los asientos de carritos "pagando" vigentes y de carritos "pagados". Las modalidades de acceso de la 007 se mantienen y se suman "iniciar el pago" (portador del carrito) y "aviso de pago" (pública, protegida por firma).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una persona con un carrito activo y email de entrega completa la compra en un solo paso desde el sitio (iniciar el pago) más el pago en la página de Mercado Pago, sin registrarse ni ingresar datos de tarjeta en el sistema.
- **SC-002**: En el 100% de los pagos aprobados, por el total exacto y procesados con el carrito vigente, el carrito queda pagado con exactamente una entrada por asiento.
- **SC-003**: El 100% de los avisos sin firma válida, y el 100% de los avisos cuyo pago no figura aprobado al consultarlo, no producen entradas.
- **SC-004**: Ante avisos repetidos o simultáneos del mismo pago, en el 100% de los casos se emiten entradas, se envía el email y se devuelve el dinero como mucho una vez.
- **SC-005**: El 100% de los pagos aprobados a tiempo cuyos asientos siguen libres se convierten en compra aunque el aviso llegue después del vencimiento, y el 100% de los pagos aprobados tardíos, sin asientos libres o por un monto distinto se devuelven completos sin intervención manual y quedan registrados.
- **SC-006**: Ningún carrito extiende su vencimiento más de una vez ni más allá del inicio de la función.
- **SC-007**: Después de volver de la página de pago, la persona ve "pagado" con sus entradas dentro de los segundos siguientes a la llegada del aviso, y mientras tanto ve "pagando" en lugar de un error.
- **SC-008**: El 100% de las compras confirmadas producen un email de entrega en el entorno de desarrollo, y ninguna compra se deshace por una falla de email.

## Assumptions

- Se usa exclusivamente el entorno de pruebas de Mercado Pago (Argentina, pesos argentinos); el dinero es ficticio. Las credenciales de producción quedan fuera de alcance.
- La experiencia de pago es Checkout Pro: la página de pago la provee Mercado Pago. La URL de pago devuelta es la del entorno de pruebas.
- La extensión de 1 minuto no es configurable en esta feature; la retención base de 5 minutos sigue siendo configurable como en la 007.
- El token del carrito sigue vigente 60 minutos (configuración de la 007), tiempo suficiente para consultar el resultado del pago después de volver de Mercado Pago. Consultar las entradas después de que vence el token queda fuera de alcance; el email es la entrega duradera.
- Las direcciones de vuelta al sitio (pago aprobado, rechazado, pendiente) y la dirección pública de avisos son configurables por entorno. En desarrollo local sin acceso público se usa el procesador simulado.
- Mercado Pago entra como un puerto nuevo del sistema con dos implementaciones (real y simulada), según los Principios I y IV de la constitución. Las credenciales y la clave de avisos se configuran por secreto o variable de entorno.
- El email de entrega se envía por el puerto de envío de email existente, disparado desde el Outbox (Principio VI). Hoy el Outbox se registra pero no existe un proceso en background que lo procese: esta feature incluye ese procesamiento con reintentos. Las devoluciones también se disparan desde el Outbox.
- En Development se usa un atrapador de emails local (Mailpit) agregado al entorno de contenedores. El proveedor real de email (Resend) queda fuera de esta feature o como implementación configurable por entorno.
- **Dependencia de gobernanza**: la constitución (v4.0.1) nombra a Stripe como pasarela de pago en el Principio I y en "Restricciones de Plataforma y Tecnología". Esta feature reemplaza Stripe por Mercado Pago, lo que requiere una enmienda de la constitución (MINOR o PATCH) antes o junto con el plan.
- Fuera de alcance: código QR de las entradas, panel de administración, devoluciones pedidas por el comprador, cancelación de una compra pagada, cambio de los 5 minutos de retención, credenciales de producción, recuperación de un token perdido y reenvío manual del email.
