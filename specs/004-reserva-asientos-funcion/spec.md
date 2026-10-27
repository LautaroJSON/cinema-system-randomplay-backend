# Feature Specification: Reserva de Asientos de una Función

**Feature Branch**: `004-reserva-asientos-funcion`

**Created**: 2026-10-27

**Status**: Draft

**Input**: User description: "Reserva de Asientos de una Función. Un Cliente autenticado (rol Customer), después de ver el mapa de asientos de una función, selecciona uno o más asientos y los reserva. La reserva queda en estado pendiente de pago y retiene esos asientos durante un tiempo limitado (N minutos, por ejemplo 10). Si en ese tiempo no se confirma, la reserva expira y los asientos vuelven a estar disponibles. El pago y su confirmación quedan fuera de alcance: solo se crea la reserva pendiente, que puede expirar o ser cancelada por el propio Cliente. Un asiento no puede quedar reservado dos veces para la misma función, ni siquiera ante pedidos simultáneos; la reserva es todo o nada; no se reserva en funciones ya comenzadas ni asientos inexistentes; máximo de asientos por reserva (10); el Cliente consulta sus reservas y cancela una pendiente; el mapa de asientos existente refleja como ocupados los asientos con reservas pendientes vigentes sin cambiar su contrato. Primera feature que exige autenticación: tokens emitidos por un proveedor de identidad externo y autorización por rol explícita por endpoint; crear, cancelar y consultar las propias reservas requieren el rol Customer; los endpoints públicos existentes siguen siendo públicos. Fuera de alcance: pago, confirmación, emisión de entradas, notificaciones en tiempo real y administración de funciones."

## Clarifications

### Session 2026-10-27

- Q: ¿Cuántos asientos puede retener en total un mismo Cliente en una misma función? → A: Hasta 10 en total por Cliente y función, sumando todas sus reservas pendientes vigentes; puede tener varias reservas mientras no supere ese total.
- Q: ¿Qué pasa con una reserva pendiente si la función empieza antes de que se cumplan sus 10 minutos? → A: El vencimiento es lo que ocurra primero: 10 minutos desde la creación o el horario de inicio de la función.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reservar asientos de una función (Priority: P1)

Un Cliente autenticado con rol `Customer` ya vio el mapa de asientos de una función (feature `003-mapa-asientos-funcion`) y eligió uno o más asientos disponibles. Los reserva en un solo paso. El sistema crea una reserva **pendiente de pago** a su nombre, que retiene esos asientos durante 10 minutos. Mientras la reserva esté vigente, nadie más puede reservar esos asientos para esa función.

**Why this priority**: Es el corazón de la feature y del negocio: sin la posibilidad de retener asientos no hay venta posible. Entrega valor por sí sola: el Cliente asegura dónde va a sentarse antes de pagar.

**Independent Test**: Se puede probar completamente autenticándose como un Cliente, reservando asientos disponibles de una función sembrada y verificando que la reserva queda pendiente con su vencimiento, y que un segundo intento (del mismo o de otro Cliente) sobre esos asientos es rechazado indicando cuáles no están disponibles.

**Acceptance Scenarios**:

1. **Given** una función futura con los asientos C5 y C6 disponibles, **When** un Cliente reserva C5 y C6, **Then** se crea una reserva a su nombre en estado "pendiente de pago", con esos dos asientos, la función a la que corresponde y su momento de vencimiento (10 minutos después de su creación).
2. **Given** una función que empieza dentro de 4 minutos, **When** un Cliente reserva asientos disponibles, **Then** la reserva se crea y su vencimiento coincide con el horario de inicio de la función, no con los 10 minutos.
3. **Given** el asiento C5 tiene una reserva pendiente vigente de otro Cliente, **When** un Cliente intenta reservar C4, C5 y C6, **Then** el sistema rechaza el pedido completo indicando que C5 no está disponible, y C4 y C6 siguen disponibles (no se reserva ninguno).
4. **Given** dos Clientes envían al mismo tiempo pedidos de reserva que incluyen el asiento D8 de la misma función, **When** el sistema procesa ambos, **Then** exactamente uno obtiene la reserva y el otro recibe un error indicando que D8 ya no está disponible.
5. **Given** una función cuyo horario de inicio ya pasó, **When** un Cliente intenta reservar asientos en ella, **Then** el sistema rechaza el pedido con un error de "función no disponible".
6. **Given** una Sala con filas A a H de 12 asientos, **When** un Cliente intenta reservar Z1 o A15, **Then** el sistema rechaza el pedido indicando qué asientos no existen en la Sala, y no se reserva ninguno.
7. **Given** un Cliente pide 11 asientos en una sola reserva, **When** envía el pedido, **Then** el sistema lo rechaza indicando que el máximo por reserva es 10.
8. **Given** un Cliente ya retiene 7 asientos en reservas pendientes vigentes de una función, **When** intenta reservar 4 asientos más en esa función, **Then** el sistema rechaza el pedido indicando que superaría el máximo de 10 asientos retenidos por Cliente en la función y cuántos puede reservar todavía (3); si pide 3, la reserva se crea.
9. **Given** un visitante sin autenticar, o un usuario autenticado sin el rol `Customer`, **When** intenta reservar asientos, **Then** el sistema rechaza el pedido (no autenticado o no autorizado, respectivamente) y no se crea ninguna reserva.

---

### User Story 2 - Ver en el mapa los asientos retenidos (Priority: P2)

Cualquier persona (autenticada o no) que consulta el mapa de asientos de una función ve como ocupados los asientos retenidos por reservas pendientes vigentes. Cuando una reserva vence o se cancela, sus asientos vuelven a aparecer como disponibles. La forma de la respuesta del mapa no cambia.

**Why this priority**: Sin esto, el mapa ofrecería asientos que en realidad no se pueden reservar y los Clientes chocarían con errores al intentarlo. Depende de la User Story 1 (tiene que haber reservas que reflejar).

**Independent Test**: Se puede probar creando una reserva sobre asientos de una función, consultando el mapa y verificando que esos asientos ya no figuran entre los disponibles; luego dejando vencer (o cancelando) la reserva y verificando que vuelven a figurar.

**Acceptance Scenarios**:

1. **Given** una función en una Sala de 50 asientos y una reserva pendiente vigente sobre B3 y B4, **When** cualquier persona consulta el mapa, **Then** la lista de disponibles contiene 48 asientos, sin B3 ni B4, y la cantidad de disponibles informada es 48.
2. **Given** una reserva pendiente sobre B3 y B4 cuyo vencimiento ya pasó, **When** se consulta el mapa, **Then** B3 y B4 vuelven a figurar como disponibles.
3. **Given** una reserva pendiente sobre B3 y B4 que el Cliente canceló, **When** se consulta el mapa, **Then** B3 y B4 vuelven a figurar como disponibles.
4. **Given** una reserva pendiente vigente sobre B3 en una función, **When** se consulta el mapa de otra función de la misma Sala, **Then** B3 figura como disponible en esa otra función.
5. **Given** un cliente del sistema que ya consumía el mapa de asientos antes de esta feature, **When** consulta el mapa después de esta feature, **Then** recibe una respuesta con exactamente la misma forma y sigue sin necesitar autenticación.

---

### User Story 3 - Consultar mis reservas (Priority: P3)

Un Cliente autenticado quiere ver sus reservas para saber qué tiene retenido y hasta cuándo. Ve cada reserva con su estado (pendiente, expirada o cancelada), la función a la que corresponde, los asientos y el momento de vencimiento.

**Why this priority**: Le da al Cliente visibilidad sobre lo que retuvo y es el paso previo natural a cancelar. No es imprescindible para retener asientos.

**Independent Test**: Se puede probar creando reservas para dos Clientes distintos y verificando que cada uno, al consultar sus reservas, ve solo las propias, con el estado correcto.

**Acceptance Scenarios**:

1. **Given** un Cliente con una reserva pendiente vigente, una cancelada y una vencida, **When** consulta sus reservas, **Then** ve las tres, con los estados "pendiente de pago", "cancelada" y "expirada" respectivamente, ordenadas de la más reciente a la más antigua.
2. **Given** dos Clientes con reservas cada uno, **When** uno de ellos consulta sus reservas, **Then** no ve ninguna reserva del otro.
3. **Given** un Cliente consulta una reserva puntual propia, **When** recibe el resultado, **Then** ve su estado, sus asientos, su vencimiento y el contexto de la función: película, Sucursal, Sala y horario de inicio.
4. **Given** un Cliente intenta consultar una reserva puntual que pertenece a otro Cliente, **When** envía la consulta, **Then** el sistema responde "reserva no encontrada", igual que si no existiera.
5. **Given** un Cliente sin reservas, **When** consulta sus reservas, **Then** recibe una lista vacía, sin error.

---

### User Story 4 - Cancelar una reserva pendiente (Priority: P3)

Un Cliente se arrepiente o quiere cambiar de asientos: cancela una de sus reservas pendientes vigentes. Los asientos quedan libres de inmediato para cualquier otro Cliente.

**Why this priority**: Evita que los asientos queden retenidos innecesariamente hasta el vencimiento y permite cambiar de asientos. Sin ella, la feature sigue funcionando gracias al vencimiento automático.

**Independent Test**: Se puede probar creando una reserva, cancelándola y verificando que su estado pasa a "cancelada" y que otro Cliente puede reservar inmediatamente los mismos asientos.

**Acceptance Scenarios**:

1. **Given** un Cliente con una reserva pendiente vigente sobre E1 y E2, **When** la cancela, **Then** la reserva pasa a "cancelada" y E1 y E2 quedan disponibles de inmediato para cualquier Cliente.
2. **Given** una reserva ya cancelada o ya vencida, **When** su dueño intenta cancelarla, **Then** el sistema responde con un error explícito de "la reserva no se puede cancelar" indicando su estado actual, y nada cambia.
3. **Given** una reserva de otro Cliente, **When** un Cliente intenta cancelarla, **Then** el sistema responde "reserva no encontrada" y la reserva no cambia.

---

### Edge Cases

- ¿Qué pasa si el pedido de reserva no incluye ningún asiento? Se rechaza como pedido inválido.
- ¿Qué pasa si el pedido incluye el mismo asiento dos veces? Se rechaza como pedido inválido, indicando el asiento repetido, sin reservar nada.
- ¿Qué pasa si el pedido mezcla asientos inexistentes y asientos ocupados? Se rechaza todo. El error informa los asientos inexistentes; la validación de existencia se hace antes que la de disponibilidad.
- ¿Qué pasa si la función no existe o pertenece a una película inactiva? Se responde "función no encontrada", en coherencia con la feature 003.
- ¿Qué pasa si un Cliente reserva asientos que él mismo ya tiene retenidos en otra reserva pendiente vigente de esa función? Se rechaza: esos asientos no están disponibles, sin importar quién los retiene.
- ¿Qué pasa si un Cliente ya tiene una reserva pendiente vigente en la función y quiere otra? Se permite, siempre que los asientos estén disponibles y el Cliente no supere 10 asientos retenidos en total en esa función (ver FR-008).
- ¿Qué pasa si la reserva vence justo mientras el Cliente la cancela? El resultado es coherente con el momento en que se procesa: si ya venció, se informa que no se puede cancelar porque está expirada; en ningún caso los asientos quedan retenidos.
- ¿Qué pasa si la función comienza mientras una reserva sigue pendiente? No puede ocurrir que siga vigente: su vencimiento nunca es posterior al inicio de la función (ver FR-002), así que en ese instante pasa a "expirada" y no se pueden crear reservas nuevas en esa función.
- ¿Qué pasa con una reserva pendiente vencida que nadie volvió a tocar? Se considera "expirada" para todo efecto (mapa, consulta, cancelación y nuevas reservas) desde el instante de su vencimiento, aunque ningún proceso la haya actualizado todavía.
- ¿Qué pasa si el token es inválido, está vencido o fue emitido por otro proveedor? Se responde "no autenticado" y no se procesa el pedido.

## Requirements *(mandatory)*

### Functional Requirements

**Creación de reservas**

- **FR-001**: El sistema DEBE permitir a un Cliente con rol `Customer` reservar uno o más asientos de una función, identificando cada asiento por fila y número.
- **FR-002**: Al crearse, una reserva DEBE quedar en estado "pendiente de pago", asociada al Cliente que la creó, a la función y a los asientos pedidos, con un momento de vencimiento igual a lo que ocurra primero entre el momento de creación más 10 minutos y el horario de inicio de la función. Una reserva pendiente NUNCA DEBE seguir vigente después del inicio de su función.
- **FR-003**: La duración de la retención DEBE ser un valor único y configurable del sistema (por defecto 10 minutos), no un dato que elija el Cliente.
- **FR-004**: Una reserva DEBE ser todo o nada: si cualquiera de los asientos pedidos no puede reservarse, el sistema NO DEBE reservar ninguno.
- **FR-005**: El sistema NO DEBE permitir que un mismo asiento quede retenido por más de una reserva pendiente vigente en la misma función, incluso cuando varios pedidos llegan simultáneamente. Ante pedidos concurrentes que compiten por un asiento, exactamente uno DEBE tener éxito.
- **FR-006**: Cuando un pedido se rechaza por asientos no disponibles, el error DEBE indicar explícitamente qué asientos (fila y número) no están disponibles.
- **FR-007**: El sistema DEBE rechazar el pedido cuando incluye asientos que no existen en la disposición de la Sala de la función, indicando cuáles.
- **FR-008**: Una reserva DEBE contener como mínimo 1 y como máximo 10 asientos. Además, la suma de asientos de todas las reservas pendientes vigentes de un Cliente en una misma función, incluida la nueva, NO DEBE superar 10. El rechazo por este motivo DEBE indicar cuántos asientos puede reservar todavía el Cliente en esa función. Las reservas expiradas o canceladas no cuentan para el total.
- **FR-009**: El sistema DEBE rechazar como pedido inválido un pedido con asientos repetidos.
- **FR-010**: El sistema DEBE rechazar con "función no disponible" la reserva en una función cuyo horario de inicio ya pasó respecto del momento del pedido, y con "función no encontrada" la reserva en una función inexistente o de una película inactiva. Ambos errores DEBEN ser distinguibles.

**Vencimiento y cancelación**

- **FR-011**: Una reserva pendiente cuyo momento de vencimiento ya pasó DEBE considerarse "expirada" a todos los efectos desde ese instante, y sus asientos DEBEN considerarse disponibles, sin depender de que el Cliente u otro proceso intervenga.
- **FR-012**: El Cliente DEBE poder cancelar una reserva propia en estado "pendiente de pago" y vigente. Al cancelarla, pasa a "cancelada" y sus asientos quedan disponibles de inmediato.
- **FR-013**: El sistema DEBE rechazar con un error explícito, que informe el estado actual, la cancelación de una reserva expirada o ya cancelada.
- **FR-014**: Las transiciones de estado válidas son únicamente: "pendiente de pago" → "expirada" y "pendiente de pago" → "cancelada". Los estados "expirada" y "cancelada" son finales en esta feature.

**Consulta de reservas**

- **FR-015**: El Cliente DEBE poder consultar la lista de sus reservas, ordenadas de la más reciente a la más antigua, cada una con su estado, asientos, momento de creación, momento de vencimiento y contexto de la función (título de la película, nombre de la Sucursal, nombre de la Sala y horario de inicio).
- **FR-016**: El Cliente DEBE poder consultar una reserva puntual propia por su identificador, con el mismo detalle que en la lista.
- **FR-017**: Un Cliente NO DEBE poder ver ni cancelar reservas de otro Cliente. Ante un intento, el sistema DEBE responder "reserva no encontrada", sin revelar que la reserva existe.

**Mapa de asientos**

- **FR-018**: El mapa de asientos de una función (feature 003) DEBE excluir de la lista de disponibles los asientos retenidos por reservas pendientes vigentes de esa función, y la cantidad de disponibles DEBE reflejarlo.
- **FR-019**: Los asientos de reservas expiradas o canceladas DEBEN volver a figurar como disponibles en el mapa.
- **FR-020**: La forma de la respuesta del mapa de asientos y su acceso público NO DEBEN cambiar. Este requisito reemplaza a FR-013 de la feature 003 ("todos los asientos se consideran disponibles").

**Autenticación y autorización**

- **FR-021**: Las operaciones de crear reservas, consultar las propias y cancelarlas DEBEN exigir un usuario autenticado mediante un token emitido por el proveedor de identidad externo de confianza, y con el rol `Customer`.
- **FR-022**: Un pedido sin token, o con un token inválido, vencido o de un emisor no reconocido, DEBE rechazarse como "no autenticado". Un pedido autenticado sin el rol `Customer` DEBE rechazarse como "no autorizado". Ambos rechazos DEBEN ser distinguibles.
- **FR-023**: La identidad del Cliente dueño de una reserva DEBE tomarse del token autenticado, nunca de datos enviados en el pedido.
- **FR-024**: Las consultas públicas existentes (catálogo de películas, horarios por Sucursal y mapa de asientos) DEBEN seguir siendo accesibles sin autenticación, declaradas explícitamente como públicas.
- **FR-025**: Toda operación expuesta DEBE tener declarada explícitamente su política de acceso (pública o rol requerido). Una operación sin política declarada se considera un defecto.

### Key Entities

- **Reserva**: retención temporal de uno o más Asientos de una Funcion a nombre de un Cliente. Atributos: identificador, Cliente dueño, Funcion, Asientos (1 a 10, sin repetidos), estado ("pendiente de pago", "expirada", "cancelada"), momento de creación y momento de vencimiento. Protege sus invariantes: transiciones válidas, cantidad de asientos y unicidad dentro de la reserva.
- **Asiento**: Value Object ya existente (feature 003), identificado por fila y número. Una Reserva referencia Asientos que deben pertenecer a la disposición de la Sala de la Funcion.
- **Funcion**: entidad ya existente. Determina la Sala (y por lo tanto qué Asientos existen) y su horario de inicio, que define si admite nuevas reservas.
- **Cliente**: persona autenticada con rol `Customer`, identificada por el identificador estable que provee el proveedor de identidad externo. El sistema no gestiona sus datos personales ni credenciales; solo guarda su identificador como dueño de las reservas.
- **Ocupación de asientos de una función**: conjunto de Asientos retenidos por reservas pendientes vigentes de una Funcion. Es lo que el mapa de asientos resta de la disposición para obtener los disponibles.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Desde el mapa de asientos, un Cliente autenticado retiene los asientos elegidos en un solo paso (una sola operación).
- **SC-002**: Ante 20 o más pedidos simultáneos que compiten por el mismo asiento de una función, en el 100% de las ejecuciones exactamente uno tiene éxito y todos los demás reciben un error que identifica ese asiento como no disponible.
- **SC-003**: En el 100% de los pedidos rechazados no queda reservado ningún asiento del pedido (atomicidad verificable consultando el mapa antes y después).
- **SC-004**: En el 100% de los rechazos por asientos no disponibles o inexistentes, el error lista exactamente los asientos afectados.
- **SC-005**: Los asientos de una reserva vencida o cancelada figuran como disponibles en el mapa en la primera consulta posterior al vencimiento o la cancelación, sin demoras adicionales.
- **SC-006**: El 100% de los intentos de ver o cancelar reservas ajenas se rechazan como "reserva no encontrada".
- **SC-007**: El 100% de las operaciones de reserva sin autenticación o sin rol `Customer` se rechazan, y el 100% de las consultas públicas existentes siguen respondiendo sin autenticación y con la misma forma de respuesta.
- **SC-008**: Un Cliente que se arrepiente libera sus asientos en un solo paso, y otro Cliente puede reservarlos inmediatamente después.

## Assumptions

- La duración de la retención es de 10 minutos, configurable a nivel de sistema.
- El máximo es de 10 asientos por reserva y de 10 asientos retenidos en total por Cliente en una misma función (ver Clarifications).
- No se exige que los asientos de una reserva sean contiguos ni de la misma fila.
- El proveedor de identidad externo ya existe y emite tokens que incluyen un identificador estable del usuario y sus roles. La configuración del proveedor (emisor, audiencia) se provee por configuración del sistema; alta de usuarios y asignación de roles se gestionan en el proveedor, fuera de este sistema.
- Para desarrollo y pruebas automatizadas se puede emitir tokens de prueba equivalentes a los del proveedor; esto no forma parte del comportamiento expuesto.
- "Ya comenzó" se determina comparando el horario de inicio de la función con la fecha y hora actuales del servidor, igual que en las features 002 y 003.
- Las reservas no tienen precio ni monto en esta feature: el cálculo de importes corresponde a la feature de pago.
- El estado "confirmada" (pago acreditado) se incorporará en la feature de pago; esta feature no lo produce.
- Los cambios de estado relevantes (reserva creada, asientos liberados por cancelación) se registran para su futura publicación según la constitución (Principio VI); su publicación en tiempo real hacia otros clientes queda fuera de alcance.
- El vencimiento no requiere que el Cliente vuelva a interactuar: el sistema lo trata como expirada a partir de su vencimiento. Si además se actualiza el estado guardado en segundo plano es una decisión de diseño, no un comportamiento visible.
- Fuera de alcance: pago, confirmación de la reserva, emisión de entradas, notificaciones en tiempo real, y alta o administración de funciones, salas y usuarios.
