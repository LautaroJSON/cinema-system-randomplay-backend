# Feature Specification: Mapa de Asientos de una Función

**Feature Branch**: `003-mapa-asientos-funcion`

**Created**: 2026-09-23

**Status**: Draft

**Input**: User description: "Mapa de asientos de una función: dado un funcionId, el usuario ve la disposición de la sala (filas y asientos) indicando cuáles están disponibles y cuáles ocupados."

## Clarifications

### Session 2026-09-23

- Q: ¿De dónde sale el estado "ocupado" mientras no exista la feature de Reservas? → A: Por ahora todos los asientos se consideran disponibles. La consulta devuelve **solo los asientos disponibles**. Todo asiento de la Sala que no figure entre los disponibles se considera ocupado, y el cliente lo muestra como tal. Cuando exista la feature de Reservas, los asientos reservados dejarán de figurar entre los disponibles sin que cambie la forma de la respuesta.
- Derivado de la respuesta anterior: para que el cliente pueda dibujar los asientos ocupados (que no vienen en la lista), la respuesta incluye también la **disposición de la Sala**: sus filas y la cantidad de asientos de cada una. Sin ese dato, un asiento ocupado al final de una fila, o una fila entera ocupada, sería indistinguible de uno que no existe.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver la disposición de la Sala de una función (Priority: P1)

Un Cliente que ya eligió una película, una Sucursal y un horario (feature `002-horarios-sucursal-hoy`) quiere ver cómo está distribuida la Sala donde se proyecta esa función: qué filas tiene y cuántos asientos hay en cada una, con cada asiento identificado por su fila y su número (por ejemplo, "F7"). Así puede ubicarse espacialmente antes de elegir dónde sentarse.

**Why this priority**: Es la base del mapa. Sin conocer la disposición física de la Sala, el cliente no puede dibujarla ni deducir qué asientos están ocupados. Entrega valor por sí sola: responde "¿cómo es la sala de esta función?".

**Independent Test**: Se puede probar completamente tomando una función sembrada cuya Sala tiene una disposición conocida (por ejemplo, filas A a H con cantidades de asientos distintas por fila) y verificando que la disposición devuelta refleja exactamente esas filas y cantidades, en orden.

**Acceptance Scenarios**:

1. **Given** una función se proyecta en una Sala con filas A a E de 10 asientos cada una, **When** el Cliente consulta el mapa de asientos de esa función, **Then** ve 5 filas, ordenadas de la A a la E, cada una con 10 asientos numerados del 1 al 10.
2. **Given** una Sala cuyas filas tienen distinta cantidad de asientos (por ejemplo, la fila A tiene 8 y la fila H tiene 14), **When** el Cliente consulta el mapa de una función en esa Sala, **Then** cada fila informa exactamente la cantidad de asientos que tiene en realidad.
3. **Given** el Cliente consulta el mapa de asientos de una función, **When** recibe el resultado, **Then** además de la disposición ve a qué función corresponde: película, Sucursal, Sala y horario de inicio.
4. **Given** el id de función consultado no existe, **When** el Cliente consulta el mapa de asientos, **Then** el sistema responde con un error explícito de "función no encontrada".

---

### User Story 2 - Ver qué asientos están disponibles (Priority: P2)

Sobre la disposición de la Sala, el Cliente quiere saber qué asientos están **disponibles** para esa función, para saber dónde podrá sentarse cuando reserve. El sistema informa solo los asientos disponibles; el cliente considera ocupado cualquier asiento de la disposición que no figure en esa lista.

**Why this priority**: Es lo que convierte la disposición en un mapa útil para decidir. Depende de la User Story 1 (el cliente necesita la disposición para deducir los ocupados) y prepara el terreno para la futura feature de Reservas.

**Independent Test**: Se puede probar consultando una función y verificando que la lista de disponibles contiene exactamente los asientos de la disposición que no están ocupados. Mientras no exista la feature de Reservas, eso equivale a todos los asientos de la Sala.

**Acceptance Scenarios**:

1. **Given** una función en una Sala de 50 asientos y ninguna reserva en el sistema, **When** el Cliente consulta su mapa, **Then** la lista de disponibles contiene los 50 asientos de la Sala.
2. **Given** el Cliente recibe el mapa de una función, **When** compara la disposición con la lista de disponibles, **Then** cada asiento disponible pertenece a la disposición (no aparece ningún asiento inexistente en la Sala) y ninguno se repite.
3. **Given** el Cliente consulta el mapa de una función, **When** recibe el resultado, **Then** ve el total de asientos de la Sala y la cantidad de asientos disponibles, y esa cantidad coincide con el largo de la lista de disponibles.
4. **Given** (a futuro, con la feature de Reservas) una función no tiene ningún asiento disponible, **When** el Cliente consulta su mapa, **Then** la lista de disponibles está vacía, la disposición se sigue informando completa y el Cliente puede identificar que la función está agotada, sin errores.

---

### Edge Cases

- ¿Qué pasa si se consulta el mapa de una función cuyo horario de inicio ya pasó? El sistema responde con un error explícito de "función no disponible", distinto de "función no encontrada", en coherencia con la feature 002, que ya no ofrece como disponibles las funciones que comenzaron (ver FR-010).
- ¿Qué pasa si se consulta el mapa de una función programada para otro día (no hoy) que todavía no pasó? Se muestra normalmente. Esta feature parte de un id de función concreto y no restringe a "hoy".
- ¿Qué pasa si la función pertenece a una película inactiva? El sistema responde con "función no encontrada", igual que si la función no existiera (ver FR-009).
- ¿Qué pasa si la Sala tiene filas con cantidades distintas de asientos? Cada fila informa su cantidad real. El mapa no asume una grilla rectangular.
- ¿Qué pasa si el mismo asiento (misma fila y número) aparece dos veces en la definición de una Sala? No puede ocurrir: la identificación fila + número es única dentro de una Sala, y el sistema lo impide al definir la Sala.
- ¿Qué pasa si una fila entera está ocupada (a futuro)? La fila sigue apareciendo en la disposición con su cantidad de asientos, y ninguno de sus asientos figura entre los disponibles. El cliente la dibuja completa como ocupada.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE, dado un id de función, devolver la disposición de la Sala donde se proyecta esa función: la lista de sus filas con la cantidad de asientos de cada una.
- **FR-002**: Las filas DEBEN identificarse por una letra y estar ordenadas alfabéticamente (A, B, C, …).
- **FR-003**: Dentro de cada fila, los asientos DEBEN numerarse con enteros consecutivos desde 1 hasta la cantidad de asientos de esa fila.
- **FR-004**: Cada asiento DEBE identificarse de forma única dentro de una Sala por la combinación de fila y número. El sistema NO DEBE permitir definir una Sala con dos filas con la misma letra.
- **FR-005**: El sistema DEBE devolver, junto con la disposición, la lista de asientos **disponibles** para esa función, cada uno identificado por fila y número, ordenados por fila y luego por número.
- **FR-006**: El sistema NO DEBE devolver los asientos ocupados. Todo asiento de la disposición que no figure en la lista de disponibles se considera ocupado.
- **FR-007**: La lista de disponibles NO DEBE contener asientos que no pertenezcan a la disposición de la Sala, ni asientos repetidos.
- **FR-008**: La disponibilidad DEBE evaluarse por función. Que un asiento no esté disponible en una función NO DEBE afectar su disponibilidad en otra función de la misma Sala.
- **FR-009**: El sistema DEBE responder con un error explícito de "función no encontrada" cuando el id de función no existe o cuando la función pertenece a una película inactiva.
- **FR-010**: El sistema DEBE responder con un error explícito de "función no disponible" cuando la función existe pero su horario de inicio ya pasó respecto del momento de la consulta. Este error DEBE ser distinguible de "función no encontrada".
- **FR-011**: El mapa DEBE incluir información de contexto de la función: título de la película, nombre de la Sucursal, nombre de la Sala y horario de inicio.
- **FR-012**: El mapa DEBE incluir el total de asientos de la Sala y la cantidad de asientos disponibles. Cuando la cantidad de disponibles es cero, DEBE ser posible identificar que la función está agotada.
- **FR-013**: Mientras no exista la feature de Reservas, el sistema DEBE considerar disponibles todos los asientos de la Sala para cualquier función. La incorporación futura de Reservas NO DEBE requerir cambiar la forma de la respuesta de esta consulta.
- **FR-014**: La consulta del mapa de asientos NO DEBE requerir autenticación ni rol. El `Customer` y los visitantes no autenticados pueden consultarlo libremente, igual que el Catálogo y los horarios.
- **FR-015**: Esta feature NO DEBE permitir seleccionar, bloquear ni reservar asientos. Es exclusivamente de consulta.
- **FR-016**: La disposición de cada Sala DEBE existir como dato sembrado (seed) en la base. Esta feature NO DEBE incluir funcionalidad de alta ni edición de la disposición de una Sala.

### Key Entities

- **Asiento**: posición física dentro de una Sala, identificada por fila (letra) y número. Es un Value Object: dos asientos con la misma fila y número en la misma Sala son el mismo asiento.
- **Sala**: entidad ya existente (feature 002). En esta feature se amplía con su **disposición**: sus filas y la cantidad de asientos de cada una, de la que se derivan todos sus Asientos. Se asume sembrada; no se gestiona alta ni edición.
- **Funcion**: entidad ya existente (feature 002). Es el punto de entrada del mapa. Determina qué Sala se muestra y respecto de qué proyección se evalúa la disponibilidad.
- **Disponibilidad de asientos de una función**: conjunto de Asientos de la Sala que están libres para una Funcion concreta. En esta feature coincide siempre con todos los Asientos de la Sala; la futura feature de Reservas restará de ese conjunto los asientos reservados.
- **Pelicula** y **Sucursal**: entidades ya existentes. En esta feature solo aportan datos de contexto (título y nombre).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Desde que el Cliente eligió un horario, obtiene la disposición de la Sala y los asientos disponibles de esa función en un solo paso (una sola consulta), sin pasos intermedios.
- **SC-002**: El 100% de las disposiciones devueltas coinciden con la Sala real: no falta ni sobra ninguna fila, y cada fila informa su cantidad real de asientos.
- **SC-003**: El 100% de los asientos devueltos como disponibles pertenecen a la disposición de la Sala, sin repetidos.
- **SC-004**: Mientras no exista la feature de Reservas, el 100% de las consultas devuelven como disponibles todos los asientos de la Sala.
- **SC-005**: La cantidad de disponibles informada coincide en el 100% de los casos con el largo de la lista de disponibles.
- **SC-006**: El 100% de las consultas con una función inexistente, de película inactiva o ya comenzada devuelven un error explícito que permite distinguir "no existe" de "ya no está disponible".
- **SC-007**: Con solo la respuesta de esta consulta, el cliente puede dibujar el mapa completo de la Sala e identificar cada asiento ocupado, sin consultas adicionales.

## Assumptions

- El punto de partida es un id de función que el Cliente obtuvo al elegir un horario en la feature `002-horarios-sucursal-hoy`.
- Las filas se identifican con letras mayúsculas (A, B, C, …) y los asientos con números consecutivos desde 1 dentro de cada fila. Las Salas de esta feature tienen como máximo 26 filas.
- La disposición es una lista de filas con cantidad de asientos por fila. No se modelan pasillos, huecos, asientos para personas con movilidad reducida, butacas dobles ni categorías de precio; eso queda para features futuras.
- El cliente (front) es responsable de mostrar como "ocupado" todo asiento de la disposición que no venga en la lista de disponibles.
- Solo existen dos estados de asiento desde el punto de vista del cliente: disponible (viene en la lista) y ocupado (no viene). Estados como "bloqueado temporalmente mientras otro Cliente paga" corresponden a la futura feature de Reservas.
- El mapa refleja el estado al momento de la consulta. No se actualiza en tiempo real; las notificaciones en vivo corresponden a una feature futura.
- El acceso es público, en coherencia con las features 001 y 002.
- "Ya pasó" se determina comparando el horario de inicio de la función con la fecha y hora actuales del servidor en el momento de la consulta, igual que en la feature 002.
