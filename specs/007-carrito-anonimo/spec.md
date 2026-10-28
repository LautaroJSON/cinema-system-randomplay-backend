# Feature Specification: Carrito Anónimo con Retención Corta

**Feature Branch**: `007-carrito-anonimo`

**Created**: 2026-10-28

**Status**: Draft

**Input**: User description: "Carrito anónimo con retención corta. Reemplaza el modelo de comprador invitado con email verificado de la feature 006 (ya en master), que no frena el acaparamiento: una persona con muchos emails o alias verifica cada uno y retiene 10 asientos con cada uno. Al elegir asientos de una función se crea un carrito anónimo con un token no adivinable, sin login ni verificación de email. Un solo carrito activo por sesión y función; elegir otros asientos reemplaza la selección y libera los viejos en el mismo instante. Retención de 5 minutos desde el primer asiento, que no se reinicia al cambiar la selección. Máximo 10 asientos por carrito. Se puede abandonar. Solo quien tiene el token puede ver, modificar o abandonar el carrito. El email se pide al confirmar la compra, solo para enviar la entrada, sin verificación. Defensas: límite de carritos por origen, tope global de asientos retenidos por función, punto de extensión para CAPTCHA. Fuera de alcance: Stripe, cuentas con contraseña, login social."

## Clarifications

### Session 2026-10-28

- Q: Stripe queda fuera de alcance. ¿Qué hace "confirmar la compra" en esta feature? → A: Solo registra el email de entrega en el carrito vigente (validado y normalizado, corregible). No cambia el estado ni el reloj. La entrada se envía en la feature de pago.
- Q: ¿Qué alcance tiene el token? → A: Un token por carrito; cada carrito pertenece a una sola función. Con el token se reemplaza la selección; sin token se crea un carrito nuevo. Desaparece "listar mis reservas".
- Q: ¿Valores por defecto de las defensas? → A: Tope global del 50% de los asientos de la sala retenidos a la vez por función; 10 carritos nuevos por origen cada 10 minutos. Ambos configurables.
- Q: ¿Qué se responde sin token o con un token inválido? → A: "No autenticado" para cualquier identificador; con el token válido de otro carrito, "carrito no encontrado".
- Q: ¿Cómo se llama el concepto? → A: Carrito, en todo el sistema; la reserva pendiente de la 004 pasa a ser el carrito.
- Q: Si el reemplazo falla, ¿qué pasa con la selección anterior? → A: Todo o nada: el pedido se rechaza entero y el carrito conserva intacta la selección anterior. Los asientos del propio carrito cuentan como disponibles para él. Una selección vacía es inválida.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Elegir asientos y retenerlos sin identificarme (Priority: P1)

Una persona entra a una función, elige uno o más asientos y el sistema se los retiene durante unos minutos, sin pedirle cuenta, login ni email. Recibe un carrito con un token que solo ella tiene y que prueba que el carrito es suyo. Con el token puede ver el carrito: sus asientos, el total y cuánto falta para que venza.

**Why this priority**: Es el núcleo de la feature y el reemplazo directo del flujo de la 006. Sin esto nadie puede reservar. Entrega valor solo: una persona puede asegurarse asientos en un paso.

**Independent Test**: Se puede probar eligiendo asientos disponibles de una función sin ninguna credencial previa, verificando que se obtiene un carrito con token, que los asientos dejan de figurar como disponibles en el mapa y que el carrito vence y los libera a los 5 minutos.

**Acceptance Scenarios**:

1. **Given** una función futura con C5 y C6 disponibles, **When** una persona sin credencial elige C5 y C6, **Then** se crea un carrito activo con esos asientos, su total y su vencimiento (5 minutos después, o el inicio de la función si es antes), y se le entrega un token para operarlo.
2. **Given** un carrito activo con C5 y C6, **When** cualquiera consulta el mapa de asientos de la función, **Then** C5 y C6 no figuran como disponibles.
3. **Given** un carrito activo, **When** su portador lo consulta con el token, **Then** ve sus asientos, el total, el estado y el momento de vencimiento.
4. **Given** un carrito activo cuyo vencimiento ya pasó, **When** se consulta el mapa o el carrito, **Then** el carrito figura como expirado y sus asientos vuelven a estar disponibles, sin que nadie intervenga.
5. **Given** una persona que pide más de 10 asientos, asientos repetidos, asientos inexistentes en la sala o asientos ya retenidos por otro carrito, **When** envía el pedido, **Then** se rechaza con el mismo criterio y los mismos códigos de error que la feature 004, y no se crea ningún carrito.
6. **Given** dos personas que piden a la vez el mismo asiento libre, **When** llegan ambos pedidos, **Then** exactamente una obtiene el carrito con ese asiento y la otra recibe "asientos no disponibles".

---

### User Story 2 - Cambiar mi selección sin perder el lugar en la fila (Priority: P2)

Con su carrito activo, la persona cambia de idea y elige otros asientos de la misma función. El carrito reemplaza la selección completa: los asientos que ya no quiere se liberan en el mismo instante en que se retienen los nuevos, y nunca quedan retenidos los dos juegos. El reloj sigue corriendo desde el primer asiento elegido: cambiar la selección no da más tiempo.

**Why this priority**: Es lo que hace al carrito distinto de una reserva fija y lo que impide acumular asientos con un mismo carrito. Depende de que exista el carrito (User Story 1).

**Independent Test**: Se puede probar creando un carrito con C5 y C6, reemplazando la selección por D1 y D2, y verificando que C5 y C6 quedan disponibles, D1 y D2 retenidos, y que el vencimiento no cambió.

**Acceptance Scenarios**:

1. **Given** un carrito activo con C5 y C6 que vence a las 20:05, **When** su portador elige D1 y D2 a las 20:03, **Then** el carrito pasa a tener D1 y D2, C5 y C6 quedan disponibles, y el vencimiento sigue siendo 20:05.
2. **Given** un carrito activo con C5 y C6, **When** su portador elige C6 y C7, **Then** el carrito queda con C6 y C7: C6 se mantiene sin quedar libre ni un instante y C5 se libera.
3. **Given** un carrito activo con C5 y C6, **When** su portador elige D1 y D2 pero D2 está retenido por otro carrito, **Then** el pedido se rechaza indicando que D2 no está disponible y el carrito conserva C5 y C6 sin cambios.
4. **Given** un carrito activo, **When** su portador envía una selección vacía, con más de 10 asientos o con asientos repetidos, **Then** se rechaza como pedido inválido y el carrito no cambia.
5. **Given** un carrito expirado o abandonado, **When** su portador intenta cambiar la selección, **Then** se rechaza indicando que el carrito ya no está activo; para seguir hay que crear un carrito nuevo.
6. **Given** un carrito de la función F, **When** su portador quiere asientos de otra función G, **Then** tiene que crear un carrito nuevo para G; la selección del carrito de F no cambia, porque un carrito pertenece a una sola función.

---

### User Story 3 - Abandonar el carrito o dejar el email de entrega (Priority: P3)

La persona puede abandonar su carrito y liberar los asientos al instante. También puede indicar el email al que se le enviará la entrada cuando pague, sin verificarlo. Solo quien tiene el token puede hacer cualquiera de las dos cosas: conocer el identificador de un carrito ajeno no da acceso a nada.

**Why this priority**: Libera asientos antes de que venzan y deja listo el dato que necesita la futura feature de pago, pero no es imprescindible para retener asientos.

**Independent Test**: Se puede probar creando dos carritos, abandonando uno con su token y verificando que sus asientos quedan libres; registrando un email en el otro; y verificando que con el token de un carrito nunca se puede ver, cambiar ni abandonar el otro.

**Acceptance Scenarios**:

1. **Given** un carrito activo con C5 y C6, **When** su portador lo abandona, **Then** pasa a "abandonado" y C5 y C6 quedan disponibles de inmediato.
2. **Given** un carrito expirado o ya abandonado, **When** su portador intenta abandonarlo, **Then** se rechaza informando el estado actual.
3. **Given** un carrito activo, **When** su portador indica ` Ana@Example.com `, **Then** el carrito guarda `ana@example.com` como email de entrega, el estado y el vencimiento no cambian, y lo puede corregir mientras el carrito siga activo.
4. **Given** un carrito activo, **When** su portador indica un email sin formato válido, **Then** se rechaza como pedido inválido y el email anterior (si había) no cambia.
5. **Given** la persona A conoce el identificador del carrito de B, **When** intenta consultarlo, cambiarlo, abandonarlo o indicarle un email con el token de su propio carrito, **Then** el sistema responde "carrito no encontrado", igual que si no existiera.
6. **Given** un pedido sin token, o con un token alterado, vencido o emitido por otro sistema, **When** se usa sobre cualquier identificador de carrito, exista o no, **Then** se rechaza como "no autenticado", con la misma respuesta en todos los casos, sin revelar si el carrito existe.

---

### User Story 4 - Que nadie acapare la sala (Priority: P2)

El cine necesita que una persona o un bot no pueda retener una porción grande de una función abriendo muchos carritos. El sistema limita cuántos carritos se pueden crear desde un mismo origen en una ventana de tiempo y cuántos asientos de una función pueden estar retenidos a la vez sumando todos los carritos. La creación de carritos queda preparada para sumar un desafío anti-bots.

**Why this priority**: Es la razón de ser de la feature frente a la 006, pero se puede entregar después del carrito básico sin romperlo.

**Independent Test**: Se puede probar creando carritos desde un mismo origen hasta superar el límite y verificando el rechazo; y llenando una sala hasta el tope global y verificando que un pedido más se rechaza mientras los carritos sigan activos, y que vuelve a aceptarse cuando alguno se abandona o vence.

**Acceptance Scenarios**:

1. **Given** un origen que ya hizo 10 pedidos de creación de carrito en los últimos 10 minutos, **When** intenta crear otro, **Then** se rechaza indicando que se superó el límite y cuándo puede reintentar, y no se crea ningún carrito.
2. **Given** un origen en el límite, **When** cambia la selección o abandona un carrito que ya tiene, **Then** esas operaciones se permiten: el límite cuenta solo carritos nuevos.
3. **Given** una sala de 100 asientos con 48 retenidos por carritos activos, **When** alguien pide 3 asientos más, **Then** se rechaza porque superaría el 50% retenido, con un código estable que no revela cuántos asientos retiene cada carrito ni quiénes los tienen.
4. **Given** el caso anterior, **When** se abandona o vence un carrito que retenía 2 asientos, **Then** un pedido de 3 asientos se acepta.
5. **Given** un carrito con 4 asientos en una función que está en el tope, **When** su portador reemplaza la selección por otros 4, **Then** se acepta: el tope se calcula descontando los asientos que el propio carrito libera.
6. **Given** el sistema sin desafío anti-bots configurado, **When** se crea un carrito, **Then** el paso del desafío se aprueba siempre; **Given** un desafío configurado que rechaza el pedido, **When** se crea un carrito, **Then** se rechaza con un código estable y no se crea nada.

---

### Edge Cases

- ¿Qué pasa si el cliente pierde el token? El carrito queda inaccesible para todos y vence solo a los 5 minutos, liberando sus asientos. No hay recuperación: el token es la única credencial.
- ¿Qué pasa si una persona crea un segundo carrito para la misma función sin usar el token del primero? Se crea otro carrito: el servidor no puede saber que es la misma persona. El primero sigue retenido hasta vencer o ser abandonado. Lo acotan el límite por origen, el tope global por función y la retención corta.
- ¿Qué pasa si la función empieza antes de los 5 minutos? El carrito vence en el inicio de la función. No se pueden crear carritos ni cambiar la selección en una función que ya comenzó.
- ¿Qué pasa si el reemplazo llega justo cuando el carrito vence? Si en el momento del pedido el carrito ya venció, se rechaza como "carrito no activo" y no retiene nada.
- ¿Qué pasa si dos pedidos de reemplazo del mismo carrito llegan a la vez? Se procesan uno detrás del otro. El último en aplicarse define la selección y nunca quedan retenidos asientos de ambos.
- ¿Qué pasa si dos carritos distintos intentan reemplazar su selección por el mismo asiento libre? Exactamente uno lo obtiene; el otro recibe "asientos no disponibles" y conserva su selección anterior.
- ¿Qué pasa con el tope global si la sala tiene un número impar de asientos? El tope se redondea hacia abajo, con un mínimo de 1 asiento.
- ¿Qué pasa si varios clientes comparten un origen (por ejemplo, la red de una oficina)? Comparten el límite de creación. Es una limitación aceptada; el límite es configurable.
- ¿Qué pasa con las reservas existentes de la feature 006, que tienen un email como dueño y ningún token? Nadie podría acceder a ellas. Se descartan de forma explícita al aplicar la feature (ver FR-024).
- ¿Qué pasa con el email de entrega de un carrito que expira o se abandona? Queda guardado con el carrito, pero no se usa ni se envía nada.

## Requirements *(mandatory)*

### Functional Requirements

**Carrito**

- **FR-001**: Cualquier persona DEBE poder elegir de 1 a 10 asientos de una función futura sin cuenta, login ni verificación de email. Al hacerlo, el sistema DEBE crear un carrito activo de esa función con esos asientos y entregarle un token de acceso.
- **FR-002**: El token DEBE ser aleatorio, no adivinable y no derivable del identificador del carrito ni de ningún otro dato visible. Es la única credencial del carrito.
- **FR-003**: Un carrito pertenece a una sola función. Un pedido de selección enviado con el token de un carrito activo de esa función DEBE reemplazar la selección de ese carrito, no crear otro.
- **FR-004**: El máximo de asientos por carrito DEBE ser configurable (por defecto 10). El mínimo es 1.

**Retención y vencimiento**

- **FR-005**: El vencimiento de un carrito DEBE fijarse al crearse: lo que ocurra primero entre el momento de creación más la retención configurada (por defecto 5 minutos) y el inicio de la función. Un carrito NUNCA DEBE seguir activo después del inicio de su función.
- **FR-006**: Cambiar la selección NO DEBE modificar el vencimiento del carrito.
- **FR-007**: Un carrito cuyo vencimiento ya pasó DEBE considerarse expirado a todos los efectos desde ese instante, y sus asientos disponibles, sin depender de que alguien intervenga.

**Reemplazo de la selección**

- **FR-008**: El reemplazo DEBE ser atómico: en ningún instante observable el carrito retiene a la vez asientos de la selección anterior que no están en la nueva y asientos nuevos. Los asientos presentes en ambas selecciones se mantienen retenidos sin interrupción.
- **FR-009**: El reemplazo DEBE ser todo o nada: si la nueva selección se rechaza por cualquier motivo, el carrito DEBE conservar sin cambios la selección anterior.
- **FR-010**: Al validar disponibilidad y topes en un reemplazo, los asientos que ya retiene el propio carrito DEBEN contar como disponibles para él.
- **FR-011**: Una selección vacía, con asientos repetidos o con más asientos que el máximo por carrito DEBE rechazarse como pedido inválido.

**Disponibilidad y concurrencia**

- **FR-012**: Un asiento NO DEBE quedar retenido por más de un carrito activo de la misma función, incluso con pedidos simultáneos de creación o de reemplazo. Ante pedidos concurrentes que compiten por un asiento, exactamente uno DEBE tener éxito.
- **FR-013**: Los rechazos de una selección DEBEN conservar el criterio y los códigos de la feature 004: "asientos no disponibles" e "inexistentes" con la lista de asientos (fila y número), "función no disponible" si ya comenzó y "función no encontrada" si no existe o su película está inactiva.
- **FR-014**: El mapa de asientos de una función DEBE excluir de los disponibles los asientos retenidos por carritos activos, con la misma forma de respuesta y acceso público que hoy.

**Abandono y email de entrega**

- **FR-015**: El portador DEBE poder abandonar un carrito activo. Al hacerlo, pasa a "abandonado" y sus asientos quedan disponibles de inmediato. Abandonar un carrito expirado o abandonado DEBE rechazarse informando su estado actual.
- **FR-016**: Las transiciones válidas son únicamente activo → expirado y activo → abandonado. Ambos estados son finales en esta feature. Cambiar la selección o el email de un carrito no activo DEBE rechazarse indicando que no está activo.
- **FR-017**: El portador DEBE poder indicar, en un carrito activo, el email al que se enviará la entrada. El email DEBE normalizarse (sin espacios en los extremos, en minúsculas) y validarse en formato, sin verificación. Se puede reemplazar mientras el carrito siga activo y NO DEBE cambiar ni el estado ni el vencimiento.
- **FR-018**: El email de entrega NO DEBE usarse como credencial ni para ninguna regla de acceso o de topes.

**Acceso**

- **FR-019**: Consultar, cambiar la selección, abandonar e indicar el email de un carrito DEBEN exigir el token de ese carrito. Sin token o con un token alterado, vencido o de otro sistema, la respuesta DEBE ser "no autenticado", idéntica para cualquier identificador, exista o no. Con un token válido de otro carrito, la respuesta DEBE ser "carrito no encontrado", indistinguible de un carrito inexistente.
- **FR-020**: Toda operación expuesta DEBE declarar su modalidad de acceso: pública, portador del carrito o personal con rol. Las consultas existentes (catálogo, horarios, mapa) siguen públicas. Crear un carrito es pública. Una operación sin modalidad declarada es un defecto y DEBE quedar restringida por defecto. El rol `Customer` no existe y la autenticación del personal (`CinemaManager`, `Admin`) no cambia.

**Defensas contra el acaparamiento**

- **FR-021**: El sistema DEBE limitar la cantidad de pedidos de creación de carrito por origen de la solicitud en una ventana de tiempo configurable (por defecto 10 cada 10 minutos). Cuentan todos los intentos de creación, prosperen o no, para que probar asientos al azar también consuma el límite. El rechazo DEBE indicar cuándo reintentar. Cambiar la selección, consultar, abandonar e indicar el email no cuentan para el límite.
- **FR-022**: El sistema DEBE limitar los asientos retenidos a la vez en una función, sumando todos sus carritos activos, a un porcentaje configurable de los asientos de la sala (por defecto 50%, redondeado hacia abajo, mínimo 1). Un pedido que lo superaría DEBE rechazarse con un código estable. En un reemplazo se descuentan los asientos que el propio carrito libera.
- **FR-023**: La creación de un carrito DEBE pasar por un punto de extensión para un desafío anti-bots. Sin desafío configurado, el paso se aprueba siempre. Un desafío que rechaza DEBE producir un rechazo con código estable y no crear nada. Ningún mensaje de error de las defensas DEBE revelar asientos, carritos ni datos de otros compradores.

**Migración y retiro de la 006**

- **FR-024**: Al aplicar la feature, las reservas existentes (con email como dueño y sin token) DEBEN descartarse de forma explícita, junto con sus mensajes pendientes de publicación. La migración NO DEBE aplicarse en silencio sobre una base con datos: exige una decisión explícita, como en la feature 006.
- **FR-025**: Se retiran la verificación de email de la feature 006 (pedir y confirmar un código), sus datos guardados, el tope de asientos por email y el límite de pedidos de código por email. Las operaciones de listar reservas propias, consultar y cancelar una reserva por email dejan de existir y se reemplazan por las del carrito.

### Key Entities

- **Carrito**: retención temporal de asientos de una función por un comprador anónimo. Atributos: identificador, función, asientos seleccionados, precio unitario y total, estado (activo, expirado, abandonado), momento de creación, vencimiento fijo, momento de abandono y email de entrega opcional. Es la evolución de la reserva pendiente de la feature 004.
- **Token de acceso al carrito**: prueba de posesión de un carrito concreto. Lo recibe solo quien creó el carrito; no se deriva de datos visibles.
- **Email de entrega**: dirección normalizada a la que se enviará la entrada. No se verifica ni es credencial.
- **Origen**: la procedencia de una solicitud, usada solo para contar carritos nuevos en una ventana de tiempo.
- **Desafío anti-bots**: paso opcional en la creación de un carrito que aprueba o rechaza el pedido. En esta feature solo existe el punto de extensión.

## Impacto sobre las features anteriores

Las features 001, 002, 003 y 005 no cambian: el mapa (003) mantiene su forma y su acceso, y los precios (005) siguen aplicando al total del carrito.

| Feature 004 / 006 | Con esta feature |
|---|---|
| Reserva pendiente de 10 minutos | Carrito activo de 5 minutos (configurable), con vencimiento fijo |
| 1 a 10 asientos por reserva, todo o nada | 1 a 10 por carrito (configurable); todo o nada también al reemplazar |
| Tope de 10 asientos por comprador (email) y función | Se elimina. Lo reemplazan el tope por carrito, el tope global por función y el límite por origen |
| Varias reservas por comprador | Un carrito por token y función; la selección se reemplaza |
| Estados pendiente → expirada / cancelada | activo → expirado / abandonado |
| Verificación del email y credencial de 60 minutos | Se elimina. El token del carrito es la credencial |
| Listar mis reservas | Se elimina (no hay identidad que agrupe carritos) |
| Consultar y cancelar mi reserva | Consultar y abandonar el carrito con su token |
| — | Indicar el email de entrega (sin verificación) |

Modalidades de acceso resultantes:

| Operación | Modalidad |
|---|---|
| Catálogo, horarios y mapa de asientos | Pública |
| Crear un carrito (elegir asientos sin token) | Pública, con límite por origen y desafío opcional |
| Consultar el carrito | Portador del carrito |
| Reemplazar la selección | Portador del carrito |
| Abandonar el carrito | Portador del carrito |
| Indicar el email de entrega | Portador del carrito |
| Operaciones de administración (futuras) | Personal con rol (`CinemaManager`, `Admin`) |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una persona sin cuenta retiene asientos en un solo paso (elegirlos), sin registrarse, iniciar sesión ni verificar nada.
- **SC-002**: En el 100% de los reemplazos, ningún observador ve el carrito retener asientos de la selección anterior y de la nueva a la vez, y en el 100% de los reemplazos rechazados el carrito conserva su selección anterior.
- **SC-003**: El vencimiento de un carrito no cambia ante ningún reemplazo, y el 100% de los carritos dejan de retener asientos a lo sumo en el momento de su vencimiento.
- **SC-004**: Ante pedidos simultáneos sobre un mismo asiento, de creación o de reemplazo, exactamente uno lo obtiene en el 100% de los casos.
- **SC-005**: El 100% de los intentos de operar un carrito con el token de otro carrito se responden igual que un carrito inexistente, y el 100% de los intentos sin token válido se responden igual exista o no el carrito.
- **SC-006**: Los asientos retenidos de una función nunca superan el tope global configurado, y un mismo origen nunca hace más pedidos de creación que el límite en la ventana.
- **SC-007**: Para retener toda una sala, una sola persona necesita superar el tope global, lo que no es posible por ningún medio, a diferencia de la 006, donde le alcanzaba con verificar más emails.

## Assumptions

- La constitución v4.0.0 (Principio VII) es el marco: comprador anónimo con token por carrito, email solo para entrega, defensas por carrito, origen y función.
- El token vale solo para su carrito. Una vez expirado o abandonado, el carrito se puede seguir consultando con el token mientras el token esté vigente, pero ya no se puede operar.
- "Origen" es la dirección de red de la solicitud tal como la ve el sistema. Detrás de un proxy hace falta configurar la confianza en encabezados reenviados; el límite es por instancia del sistema.
- El envío de la entrada por email ocurre en la feature de pago. En esta feature no se envía ningún email.
- El desafío anti-bots no se implementa: solo existe el punto de extensión, aprobado por defecto.
- Los carritos expirados o abandonados no se purgan en esta feature; quedan como historial.
- Las reservas de la feature 006 se descartan (FR-024); no hay datos de producción que preservar.
- Fuera de alcance: pago con Stripe y asignación final de asientos (autorizar y capturar), envío real de la entrada, cuentas con contraseña, login social, recuperación de un token perdido, purga de carritos viejos y endpoints de personal.
