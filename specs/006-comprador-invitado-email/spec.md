# Feature Specification: Comprador Invitado con Email Verificado

**Feature Branch**: `006-comprador-invitado-email`

**Created**: 2026-10-27

**Status**: Draft

**Input**: User description: "Identidad del comprador invitado con email verificado. Hoy la reserva de asientos (feature 004) exige un usuario autenticado con rol Customer, pero no existe ningún login ni forma de obtener un token, y en un cine real no se necesita una cuenta para comprar un boleto. La constitución v3.0.0 (Principio VII) redefinió esto: el comprador es un invitado identificado por un email que debe verificarse; el rol Customer no existe; el acceso a una reserva se prueba con una credencial verificable y no debe ser posible acceder a una reserva ajena conociendo solo su id o su email; OIDC/JWT queda solo para personal interno. Esta feature reemplaza el mecanismo de identidad de la feature 004 sin cambiar su comportamiento de negocio (reservar 1 a 10 asientos, retención de 10 minutos, tope de 10 asientos retenidos por comprador por función, listar, consultar y cancelar). Lo nuevo: el invitado indica su email, el sistema verifica que le pertenece (código o enlace de un solo uso), la verificación otorga una credencial de acceso con vencimiento que permite ver y cancelar solo las reservas de ese email, el tope de 10 asientos se aplica por email normalizado, y las reservas existentes en desarrollo se migran o descartan de forma explícita. Fuera de alcance: pago, cuentas con contraseña, login social, historial entre dispositivos, administración de personal y envío real de emails en producción (se necesita un puerto de envío de email con una implementación de desarrollo que registre el mensaje)."

## Clarifications

### Session 2026-10-27

- Q: ¿El invitado debe verificar su email antes de poder reservar, o puede reservar sin verificar y verificar solo después? → A: Verificar primero: reservar exige una credencial vigente obtenida con el código.
- Q: ¿El límite de pedidos de código se aplica solo por email, o también por origen de la solicitud? → A: Por email (5/hora) y además por origen (tope configurable, 20/hora por defecto).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Verificar mi email y obtener acceso de invitado (Priority: P1)

Una persona que quiere comprar entradas no tiene ni quiere crear una cuenta. Indica su email, el sistema le envía un código de un solo uso, ella lo ingresa y recibe una credencial de acceso con vencimiento. Esa credencial prueba que controla ese email y es lo que usa después para reservar y para ver o cancelar sus reservas. No hay contraseña ni registro previo.

**Why this priority**: Es la base de toda la feature: sin una forma de probar quién es el comprador, no se pueden asignar ni proteger las reservas. Hoy es el hueco más grande del sistema, porque nadie puede obtener un token válido. Entrega valor por sí sola: convierte a cualquier persona en un comprador identificado en un par de pasos.

**Independent Test**: Se puede probar pidiendo la verificación de un email, tomando el código del mensaje registrado por la implementación de desarrollo, confirmándolo y verificando que se obtiene una credencial válida; y que con un código incorrecto, vencido o ya usado no se obtiene nada.

**Acceptance Scenarios**:

1. **Given** una persona sin cuenta, **When** pide la verificación de `Ana@Example.com`, **Then** el sistema genera un código de un solo uso con vencimiento, lo envía a ese email (en desarrollo, lo registra en lugar de enviarlo) y responde que la solicitud fue aceptada, sin revelar nada más.
2. **Given** un código vigente enviado a `ana@example.com`, **When** la persona ingresa ese código con ese email, **Then** el sistema le entrega una credencial de acceso con su momento de vencimiento, asociada a `ana@example.com`.
3. **Given** un código ya usado con éxito, **When** se intenta usar de nuevo, **Then** el sistema lo rechaza y no entrega ninguna credencial.
4. **Given** un código cuyo vencimiento ya pasó, **When** se ingresa, **Then** el sistema lo rechaza indicando que venció y que debe pedir uno nuevo.
5. **Given** un código incorrecto, **When** se ingresa más veces que el máximo de intentos permitido, **Then** el código queda invalidado y hay que pedir uno nuevo; ningún intento entrega una credencial.
6. **Given** un email con formato inválido, **When** se pide la verificación, **Then** el sistema rechaza el pedido como inválido y no genera ningún código.
7. **Given** dos personas distintas, **When** una de ellas conoce el email de la otra pero no recibe su código, **Then** no puede obtener una credencial para ese email.

---

### User Story 2 - Reservar asientos como invitado verificado (Priority: P2)

Un invitado con credencial vigente reserva uno o más asientos de una función, con exactamente las reglas de la feature 004: de 1 a 10 asientos por reserva, retención de 10 minutos (o hasta el inicio de la función, lo que ocurra primero), todo o nada, sin doble reserva de un asiento, y un tope de 10 asientos retenidos en total por comprador en cada función. La diferencia es quién es el comprador: el dueño de la reserva es el email verificado de su credencial, y el tope se cuenta por email.

**Why this priority**: Es el objetivo de negocio de la feature, pero depende de que exista la verificación (User Story 1). Reemplaza la identidad de la 004 sin cambiar su comportamiento visible.

**Independent Test**: Se puede probar obteniendo una credencial para un email, reservando asientos disponibles de una función y verificando que la reserva queda a nombre de ese email, y que sin credencial o con una vencida la reserva se rechaza.

**Acceptance Scenarios**:

1. **Given** una credencial vigente para `ana@example.com` y una función futura con los asientos C5 y C6 disponibles, **When** reserva C5 y C6, **Then** se crea una reserva pendiente de pago a nombre de `ana@example.com`, con los mismos vencimiento y reglas que en la feature 004.
2. **Given** `ana@example.com` retiene 7 asientos en una función, **When** reserva 4 más en esa función con una credencial de `ANA@example.com `, **Then** el sistema trata ambos como el mismo comprador y rechaza el pedido indicando que puede reservar 3 más.
3. **Given** `ana@example.com` retiene 10 asientos en una función, **When** `beto@example.com` reserva otros asientos de esa función, **Then** la reserva se crea: el tope es por comprador, no por función.
4. **Given** un pedido de reserva sin credencial, con una credencial vencida o con una credencial alterada, **When** se envía, **Then** el sistema lo rechaza como no autenticado y no crea ninguna reserva.
5. **Given** una credencial vigente, **When** el pedido de reserva incluye un email distinto en sus datos, **Then** el dueño de la reserva sigue siendo el email de la credencial; el pedido nunca puede elegir a nombre de quién reservar.
6. **Given** cualquier rechazo de reserva definido en la feature 004 (asientos no disponibles, inexistentes, función comenzada, máximo superado, pedido inválido), **When** ocurre, **Then** el comportamiento y el contenido del error son los mismos que en la 004.

---

### User Story 3 - Ver y cancelar solo mis reservas (Priority: P3)

Un invitado con credencial vigente ve la lista de sus reservas, consulta una puntual y cancela una pendiente vigente, igual que en la feature 004. Solo ve y toca las reservas de su email. Conocer el identificador de una reserva ajena, o el email de otra persona, no le da acceso a nada.

**Why this priority**: Completa la experiencia del comprador y es donde se pone a prueba la seguridad (no ver lo ajeno), pero no es imprescindible para retener asientos.

**Independent Test**: Se puede probar creando reservas para dos emails con sus credenciales, y verificando que cada uno ve y cancela solo las propias, y que una credencial de uno nunca da acceso a las del otro aunque se conozca el identificador.

**Acceptance Scenarios**:

1. **Given** `ana@example.com` con una reserva pendiente, una cancelada y una vencida, **When** consulta sus reservas con su credencial, **Then** ve las tres, con el mismo contenido y orden que en la feature 004.
2. **Given** `ana@example.com` y `beto@example.com` con reservas cada uno, **When** Ana consulta sus reservas, **Then** no ve ninguna de Beto.
3. **Given** Ana conoce el identificador exacto de una reserva de Beto, **When** intenta consultarla o cancelarla con su propia credencial, **Then** el sistema responde "reserva no encontrada", igual que si no existiera.
4. **Given** una reserva pendiente vigente de Ana, **When** la cancela con su credencial, **Then** pasa a "cancelada" y sus asientos quedan disponibles de inmediato.
5. **Given** la credencial de Ana ya venció, **When** intenta consultar o cancelar, **Then** el sistema la rechaza como no autenticada; sus reservas siguen existiendo y vencen o se retienen según sus propias reglas, y Ana puede volver a verificar su email para recuperar el acceso.
6. **Given** Ana verifica su email desde otro dispositivo o en otro momento, **When** obtiene una nueva credencial, **Then** ve las mismas reservas de su email.

---

### Edge Cases

- ¿Qué pasa si se piden muchos códigos para el mismo email o desde el mismo origen en poco tiempo? Se limita la cantidad de solicitudes por email y por origen en una ventana de tiempo; las solicitudes que exceden el límite se rechazan sin enviar más mensajes.
- ¿Qué pasa si se pide un código para un email que nunca compró? Se procesa igual: la respuesta es idéntica exista o no el email en el sistema. El sistema no revela qué emails tienen reservas.
- ¿Qué pasa si se pide un código nuevo mientras hay uno vigente? El código anterior se invalida; solo el último es válido.
- ¿Qué pasa si dos emails se escriben distinto pero son el mismo (`Ana@Example.com` y ` ana@example.com `)? Se normalizan (se recortan espacios y se pasa a minúsculas) y son el mismo comprador a todos los efectos: tope, propiedad de las reservas y credencial.
- ¿Qué pasa con los alias de proveedor (`ana+1@example.com`, puntos en una dirección de Gmail)? No se normalizan: son emails distintos. Es una limitación conocida: con alias, un mismo buzón real puede aparecer como varios compradores y evadir el tope por comprador.
- ¿Qué pasa si la credencial vence mientras el invitado tiene reservas pendientes? Las reservas no se ven afectadas: siguen retenidas hasta su propio vencimiento. Solo se pierde el acceso hasta que el invitado vuelva a verificar su email.
- ¿Qué pasa si alguien con la credencial de un email intenta usarla para actuar sobre otro email? Cada operación toma el email únicamente de la credencial; no hay forma de indicar otro.
- ¿Qué pasa con las reservas creadas en desarrollo antes de esta feature, que pertenecen a identificadores de usuario y no a emails? No hay forma de asignarlas a un email, y no hay datos reales que preservar: se descartan de forma explícita al aplicar la feature (ver FR-021).
- ¿Qué pasa si el email del comprador no existe o el mensaje no llega? El sistema no puede saberlo; el invitado simplemente no recibe el código y puede pedir otro dentro del límite. En desarrollo, el mensaje queda registrado.
- ¿Qué pasa si un endpoint personal (para `CinemaManager` o `Admin`) todavía no existe? No se crea ninguno en esta feature; la política de acceso para el personal queda definida pero sin endpoints que la usen.

## Requirements *(mandatory)*

### Functional Requirements

**Verificación del email**

- **FR-001**: El sistema DEBE permitir a cualquier persona pedir la verificación de un email, sin cuenta ni autenticación previa. Ante el pedido, DEBE generar un código de un solo uso, de vencimiento corto y configurable (por defecto 10 minutos), y enviarlo a ese email.
- **FR-002**: Un email DEBE normalizarse antes de usarse en cualquier regla: se recortan los espacios al inicio y al final y se pasa a minúsculas. Dos emails que difieren solo en eso DEBEN considerarse el mismo comprador. El sistema DEBE rechazar como inválido un pedido con un email vacío o sin formato de email.
- **FR-003**: La respuesta a un pedido de verificación DEBE ser la misma para cualquier email válido, haya tenido o no reservas, para no revelar qué emails existen en el sistema.
- **FR-004**: Un código DEBE poder usarse una sola vez. Pedir un código nuevo para un email DEBE invalidar el anterior. Un código vencido DEBE rechazarse indicando que venció.
- **FR-005**: El sistema DEBE limitar los intentos fallidos de ingreso de un código (por defecto 5): al superarlos, el código queda invalidado y hay que pedir uno nuevo.
- **FR-006**: El sistema DEBE limitar la cantidad de pedidos de verificación en una ventana de tiempo configurable, tanto por email normalizado (por defecto 5 por hora) como por origen de la solicitud (por defecto 20 por hora, configurable), rechazando los que excedan cualquiera de los dos límites sin enviar más mensajes. El límite por email se aplica igual exista o no el email en el sistema, para no violar FR-003.
- **FR-007**: El envío del mensaje con el código DEBE hacerse a través de un servicio intercambiable. En desarrollo y pruebas, la implementación DEBE registrar el mensaje (destinatario y código) en lugar de enviarlo. El envío real en producción queda fuera de alcance.

**Credencial de acceso**

- **FR-008**: Al ingresar un código válido, el sistema DEBE entregar una credencial de acceso asociada al email normalizado, con un momento de vencimiento (por defecto 60 minutos, configurable). La credencial DEBE poder verificarse sin consultar a una persona y NO DEBE poder falsificarse ni alterarse.
- **FR-009**: Toda operación del comprador DEBE tomar el email únicamente de la credencial. Ningún dato enviado en el pedido DEBE poder cambiar a nombre de quién se actúa.
- **FR-010**: Un pedido sin credencial, con una credencial vencida, alterada o emitida por otro sistema DEBE rechazarse como "no autenticado". No existe un rol de usuario para el comprador: poseer una credencial vigente alcanza.
- **FR-011**: Se PUEDEN tener varias credenciales vigentes para un mismo email a la vez (por ejemplo, desde dos dispositivos), y todas dan acceso a las mismas reservas.

**Reservas del invitado**

- **FR-012**: Las operaciones de crear una reserva, listar las propias, consultar una puntual y cancelar una DEBEN requerir una credencial de invitado vigente. El email se verifica antes de poder reservar: no se puede reservar con un email sin verificar. Su comportamiento de negocio DEBE ser el de la feature 004 (cantidad de asientos, retención, vencimiento, todo o nada, unicidad de asientos, rechazos y sus contenidos de error, orden de la lista, detalle de cada reserva).
- **FR-013**: El dueño de una reserva DEBE ser el email normalizado de la credencial con la que se creó.
- **FR-014**: El tope de 10 asientos retenidos en total por comprador en una misma función DEBE contarse por email normalizado, sumando todas sus reservas pendientes vigentes en esa función. Las reservas expiradas o canceladas no cuentan.
- **FR-015**: Un comprador NO DEBE poder ver, consultar ni cancelar reservas de otro email. Ante un intento sobre una reserva ajena, el sistema DEBE responder "reserva no encontrada", sin revelar que existe. Conocer el identificador de la reserva o el email de su dueño NO DEBE bastar para acceder a ella.
- **FR-016**: Las reservas existentes NO DEBEN verse afectadas por el vencimiento de la credencial con la que se crearon: siguen retenidas hasta su propio vencimiento.

**Modalidades de acceso**

- **FR-017**: Toda operación expuesta DEBE tener declarada explícitamente su modalidad de acceso: pública, invitado verificado o personal con rol. Una operación sin modalidad declarada se considera un defecto, no una operación pública.
- **FR-018**: Las operaciones de consulta ya existentes (catálogo de películas, sucursales y horarios de hoy, mapa de asientos) DEBEN seguir siendo públicas, con la misma forma de respuesta. Las dos operaciones de verificación de email (pedir el código e ingresarlo) DEBEN ser públicas, porque su función es dar acceso a quien todavía no lo tiene.
- **FR-019**: El rol `Customer` DEJA de existir. La autenticación por proveedor de identidad externo con roles (`CinemaManager`, `Admin`) se reserva para operaciones del personal; esta feature NO crea ninguna operación de ese tipo. Toda operación sin modalidad declarada DEBE seguir exigiendo acceso restringido por defecto.

**Migración**

- **FR-020**: Los contratos de la feature 004 DEBEN actualizarse: las operaciones de reservas dejan de requerir el rol `Customer` y pasan a requerir una credencial de invitado; sus formas de pedido y de respuesta no cambian salvo lo necesario para identificar al dueño por email.
- **FR-021**: Al aplicar la feature, las reservas existentes en bases de desarrollo (que pertenecen a identificadores de usuario y no a emails) DEBEN descartarse de forma explícita, junto con los mensajes pendientes de publicación asociados a ellas, de modo que no queden reservas sin un dueño por email. Esta migración NO DEBE aplicarse a una base con datos reales sin una decisión explícita aparte.

### Key Entities

- **Comprador invitado**: persona identificada solo por un email normalizado. No es una cuenta: no tiene contraseña, perfil ni registro previo, y el sistema no guarda más datos suyos que el email como dueño de sus reservas.
- **Verificación de email**: intento de probar que alguien controla un email. Atributos: email normalizado, código de un solo uso, momento de creación, vencimiento, cantidad de intentos fallidos y estado (vigente, usada, vencida, invalidada). Un email tiene a lo sumo una vigente.
- **Credencial de acceso**: prueba, con vencimiento, de que su portador verificó un email. Atributos: email normalizado y momento de vencimiento. No se almacena un listado de credenciales; se verifica por sí misma.
- **Reserva**: agregado existente (feature 004). Cambia su dueño: pasa de un identificador de usuario del proveedor externo a un email normalizado del comprador invitado.
- **Mensaje de verificación**: el aviso con el código dirigido al email. En desarrollo se registra en lugar de enviarse.

## Impacto sobre las features anteriores

Las features 001, 002, 003 y 005 no cambian. De la feature 004 se reemplaza el mecanismo de identidad; su comportamiento de negocio se conserva.

| Requisito de la feature 004 | Con esta feature |
|---|---|
| FR-021 (exigir usuario autenticado con rol `Customer`) | Reemplazado por FR-012 y FR-010: se exige una credencial de invitado, sin rol |
| FR-022 (distinguir "no autenticado" de "no autorizado") | Queda solo "no autenticado": no hay roles para el comprador |
| FR-023 (identidad tomada del token) | Se conserva con el mismo espíritu: la identidad sale únicamente de la credencial (FR-009) |
| FR-024 y FR-025 (públicas explícitas, política declarada en toda operación) | Se conservan, ampliadas con la modalidad "invitado verificado" (FR-017) |
| Resto (FR-001 a FR-020) | Sin cambios de comportamiento; "Cliente" pasa a ser "comprador" identificado por email |

Modalidades de acceso resultantes:

| Operación | Modalidad |
|---|---|
| Listar y ver el detalle de películas | Pública |
| Sucursales con funciones hoy y horarios disponibles | Pública |
| Mapa de asientos de una función | Pública |
| Pedir la verificación de un email | Pública |
| Ingresar el código y obtener la credencial | Pública |
| Reservar asientos | Invitado verificado |
| Listar mis reservas | Invitado verificado |
| Consultar una reserva propia | Invitado verificado |
| Cancelar una reserva propia | Invitado verificado |
| Operaciones de administración (futuras) | Personal con rol (`CinemaManager`, `Admin`) |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una persona sin cuenta obtiene acceso para reservar en dos pasos: pedir el código para su email e ingresarlo. No necesita registrarse ni crear una contraseña.
- **SC-002**: En el 100% de los intentos, una persona que no recibió el código de un email no logra acceso a ese email, aunque conozca el email o el identificador de sus reservas.
- **SC-003**: El 100% de los intentos de consultar o cancelar reservas de otro email se rechazan como "reserva no encontrada", sin diferencia observable respecto de una reserva inexistente.
- **SC-004**: Un código sirve para obtener acceso a lo sumo una vez, y el 100% de los códigos vencidos, ya usados o invalidados por intentos fallidos se rechazan.
- **SC-005**: El 100% de los pedidos de reserva sin credencial vigente se rechazan, y el 100% de las consultas públicas existentes siguen respondiendo sin credencial y con la misma forma de respuesta.
- **SC-006**: Las reservas de un mismo comprador nunca superan 10 asientos retenidos por función, aun cuando su email se escriba con distintas mayúsculas o espacios.
- **SC-007**: Todos los rechazos y resultados de reservar, listar, consultar y cancelar coinciden con los de la feature 004 para los mismos casos, verificable repitiendo sus escenarios con credenciales de invitado.
- **SC-008**: Ante una solicitud de verificación, la respuesta es indistinguible entre un email con reservas y uno sin ellas.

## Assumptions

- El código de verificación es numérico y corto, entregado por email y ingresado en la API. Un enlace de verificación no se contempla porque el sistema es un backend sin frontend propio.
- Valores por defecto configurables: el código vence a los 10 minutos, admite 5 intentos fallidos, y la credencial dura 60 minutos. El límite de pedidos de verificación es de 5 por hora por email y de 20 por hora por origen.
- Las credenciales no se revocan antes de su vencimiento en esta feature; cerrar sesión no existe.
- No hay verificación de que un email "exista" más allá de que el código llegue y se ingrese correctamente.
- El Principio VII v3.0.0 de la constitución es el marco: el comprador es un invitado verificado, el personal usa un proveedor de identidad externo. Esta feature implementa lo primero y no agrega lo segundo.
- Las reservas de desarrollo previas se descartan (FR-021); no hay datos de producción que preservar.
- El nombre del comprador no se captura: una reserva queda identificada por su email y su identificador.
- Fuera de alcance: pago y confirmación de la reserva, cuentas con contraseña, login social, historial de reservas entre dispositivos más allá de verificar el mismo email, alta o administración del personal y de sus roles, envío real de emails en producción, y cualquier endpoint administrativo.
