# Feature Specification: Horarios por Sucursal — Funciones de Hoy

**Feature Branch**: `002-horarios-sucursal-hoy`

**Created**: 2026-09-21

**Status**: Draft

**Input**: User description: "Como Cliente, después de ver el catálogo y elegir una película, quiero ver en qué Sucursales esa película tiene funciones programadas para HOY (aunque tenga funciones otros días, solo me interesa hoy), y al elegir una Sucursal, quiero ver por defecto los horarios de hoy disponibles en esa sucursal para esa película. Sucursal, Sala y Funcion no tienen todavía funcionalidad de alta ni administración — sus datos se cargan sembrados (seed) directamente en la base de datos para esta feature, sin necesidad de autenticación ni roles. El alcance de esta feature es puramente de consulta/lectura."

## Clarifications

### Session 2026-09-21

- Q: ¿Qué debe pasar si se consulta esta feature con un id de Película que no existe o está inactiva? → A: El sistema responde con un error explícito de "película no encontrada", distinto del mensaje de "sin funciones hoy" que aplica cuando la película es válida pero no tiene programación hoy.
- Q: ¿Qué debe pasar si el Cliente elige una Sucursal que no está entre las devueltas para esa película (id inexistente, o válida pero sin funciones hoy para esa película)? → A: El sistema responde con un error explícito de "Sucursal no válida para esta película", distinto del mensaje de "sin horarios disponibles" que aplica cuando la Sucursal sí es válida pero sus funciones de hoy ya pasaron.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver Sucursales con funciones hoy para una película (Priority: P1)

Un Cliente que ya eligió una película desde el Catálogo quiere ver en qué Sucursales esa película tiene al menos una función programada para **hoy**, para decidir a cuál ir. Si la película tiene funciones en otros días pero no hoy en una Sucursal dada, esa Sucursal no debe aparecer.

**Why this priority**: Es el primer paso del flujo — sin saber en qué Sucursales hay funciones hoy, el Cliente no puede avanzar a elegir horario. Entrega valor por sí sola: responde "¿dónde puedo ver esta película hoy?".

**Independent Test**: Se puede probar completamente tomando una película con funciones sembradas en la base (algunas hoy, algunas en otros días, en distintas Sucursales) y verificando que solo aparecen las Sucursales con al menos una función hoy para esa película, sin depender de ningún otro paso del flujo.

**Acceptance Scenarios**:

1. **Given** una película tiene funciones programadas hoy en la Sucursal A y en la Sucursal B, pero no en la Sucursal C, **When** el Cliente consulta las Sucursales para esa película, **Then** ve únicamente la Sucursal A y la Sucursal B.
2. **Given** una película tiene funciones programadas solo para mañana (no hoy) en la Sucursal D, **When** el Cliente consulta las Sucursales para esa película, **Then** la Sucursal D no aparece en el resultado.
3. **Given** una película no tiene ninguna función programada para hoy en ninguna Sucursal (tenga o no funciones otros días), **When** el Cliente consulta las Sucursales para esa película, **Then** ve un mensaje claro indicando que no hay funciones hoy para esa película, sin errores.
4. **Given** el id de película consultado no existe o corresponde a una película inactiva, **When** el Cliente consulta las Sucursales para esa película, **Then** el sistema responde con un error explícito de "película no encontrada", distinto del mensaje de "sin funciones hoy".

---

### User Story 2 - Ver horarios de hoy en la Sucursal elegida (Priority: P2)

Después de elegir una Sucursal de la lista, el Cliente quiere ver por defecto los horarios de hoy disponibles para esa película en esa Sucursal, para poder elegir la función a la que va a ir.

**Why this priority**: Depende de la User Story 1 (necesita una Sucursal ya filtrada por "tiene funciones hoy"), y es el siguiente paso natural que completa el propósito de la consulta: saber exactamente a qué hora ir.

**Independent Test**: Se puede probar seleccionando una Sucursal que ya se sabe tiene funciones hoy para la película (dato sembrado) y verificando que se listan los horarios de hoy en esa Sucursal para esa película, ordenados cronológicamente, sin necesidad de elegir otra fecha.

**Acceptance Scenarios**:

1. **Given** el Cliente eligió una Sucursal que tiene 3 funciones hoy para la película elegida, **When** entra a la vista de horarios de esa Sucursal, **Then** ve por defecto las 3 funciones de hoy, ordenadas de la más temprana a la más tardía, cada una indicando al menos el horario y la Sala.
2. **Given** una función de hoy en la Sucursal elegida ya comenzó (su horario ya pasó), **When** el Cliente ve los horarios de hoy disponibles, **Then** esa función ya no aparece en la lista de horarios disponibles.
3. **Given** todas las funciones de hoy de esa película en la Sucursal elegida ya pasaron, **When** el Cliente ve los horarios de esa Sucursal, **Then** ve un mensaje claro indicando que no quedan horarios disponibles hoy en esa Sucursal, sin errores.
4. **Given** el Cliente intenta consultar horarios de una Sucursal cuyo id no existe, o que nunca tuvo funciones hoy para esa película (no formaba parte del listado de Sucursales de la User Story 1), **When** realiza esa consulta, **Then** el sistema responde con un error explícito de "Sucursal no válida para esta película", distinto del mensaje de "sin horarios disponibles".

---

### Edge Cases

- ¿Qué pasa si dos funciones de la misma película, en la misma Sucursal, están programadas exactamente a la misma hora pero en Salas distintas? Ambas se listan como horarios separados, distinguidas por su Sala.
- ¿Qué pasa si, entre el momento en que el Cliente ve la lista de Sucursales y el momento en que elige una, la última función de hoy en esa Sucursal ya pasó (por el paso del tiempo)? El sistema debe mostrar el mensaje de "sin horarios disponibles hoy" en vez de un error o una lista vacía sin explicación.
- ¿Qué pasa si una Sucursal tiene funciones hoy para la película en más de una Sala? Todas esas funciones aparecen en la lista de horarios de esa Sucursal, cada una con su Sala correspondiente.
- ¿Qué pasa si se consulta con un id de Película inexistente o de una película inactiva? El sistema responde con un error explícito de "película no encontrada", distinto del mensaje de "sin funciones hoy" (ver FR-012).
- ¿Qué pasa si el Cliente elige una Sucursal con un id inexistente, o una Sucursal que nunca tuvo funciones hoy para esa película? El sistema responde con un error explícito de "Sucursal no válida para esta película", distinto del mensaje de "sin horarios disponibles" que aplica cuando la Sucursal sí es válida pero sus funciones ya pasaron (ver FR-013).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE, dada una película ya elegida, listar las Sucursales que tienen al menos una Funcion programada para **hoy** para esa película.
- **FR-002**: El sistema NO DEBE incluir en ese listado una Sucursal cuya única programación de esa película sea en días distintos de hoy.
- **FR-003**: Cuando ninguna Sucursal tiene funciones hoy para la película elegida, el sistema DEBE mostrar un mensaje claro indicando que no hay funciones hoy, en vez de una lista vacía sin explicación.
- **FR-004**: Los usuarios DEBEN poder elegir una Sucursal del listado devuelto por FR-001.
- **FR-005**: Al elegir una Sucursal, el sistema DEBE mostrar por defecto los horarios de **hoy** disponibles para esa película en esa Sucursal, sin requerir que el Cliente seleccione una fecha.
- **FR-006**: El listado de horarios DEBE estar ordenado cronológicamente, del más temprano al más tardío.
- **FR-007**: Cada horario listado DEBE indicar al menos la hora de la función y la Sala en la que se proyecta.
- **FR-008**: El sistema NO DEBE incluir en los horarios disponibles de hoy una Funcion cuyo horario de inicio ya pasó respecto del momento de la consulta.
- **FR-009**: Cuando la Sucursal elegida es válida para esa película (formaba parte del listado de FR-001) pero, al momento de la consulta de horarios, ya no queda ningún horario disponible hoy (porque todas sus funciones de hoy ya pasaron), el sistema DEBE mostrar un mensaje claro en vez de una lista vacía sin explicación.
- **FR-010**: El acceso de lectura a esta consulta (Sucursales con funciones hoy y horarios por Sucursal) NO DEBE requerir autenticación ni rol — el `Customer` y los visitantes no autenticados pueden consultarlo libremente.
- **FR-011**: Esta feature NO DEBE incluir funcionalidad de alta, edición ni administración de `Sucursal`, `Sala` ni `Funcion`; esos datos se asumen ya sembrados (seed) directamente en la base de datos.
- **FR-012**: El sistema DEBE responder con un error explícito de "película no encontrada" cuando el id de Pelicula consultado no exista o corresponda a una película inactiva, en vez de tratarla como una película válida sin funciones hoy (FR-003).
- **FR-013**: El sistema DEBE responder con un error explícito de "Sucursal no válida para esta película" cuando el id de Sucursal consultado no exista, o exista pero nunca haya tenido ninguna función hoy para esa película (no formaba parte del listado de FR-001) — distinto del mensaje de "sin horarios disponibles" de FR-009.

### Key Entities

- **Sucursal**: representa una ubicación física del cine. Atributos relevantes para esta feature: nombre (para identificarla en el listado). Se asume sembrada directamente en la base de datos; no se gestiona alta/edición en esta feature.
- **Sala**: representa una sala dentro de una Sucursal donde se proyecta una Funcion. Atributo relevante para esta feature: nombre/identificador, usado para distinguir horarios simultáneos dentro de la misma Sucursal. Se asume sembrada directamente en la base de datos; no se gestiona alta/edición en esta feature.
- **Funcion**: representa una proyección programada de una `Pelicula`, en una `Sala` de una `Sucursal`, en una fecha y hora determinadas. Es la entidad central de esta feature: se consulta (solo lectura) para determinar qué Sucursales tienen programación hoy para una película, y qué horarios de hoy siguen disponibles en la Sucursal elegida. Se asume sembrada directamente en la base de datos; no se gestiona alta/edición en esta feature.
- **Pelicula**: entidad ya existente (feature de Catálogo). En esta feature es solo el punto de partida ya elegido por el Cliente; no se agregan ni modifican atributos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Desde que el Cliente eligió una película, puede ver el listado de Sucursales con funciones hoy en un solo paso (una sola consulta), sin pasos intermedios de selección de fecha.
- **SC-002**: El 100% de las Sucursales mostradas para una película tienen al menos una función hoy para esa película; ninguna Sucursal sin funciones hoy aparece jamás en el listado.
- **SC-003**: Desde que el Cliente elige una Sucursal, ve los horarios de hoy disponibles para esa película sin necesidad de ninguna acción adicional (no tiene que filtrar ni elegir "hoy" manualmente).
- **SC-004**: El 100% de los horarios mostrados como "disponibles hoy" corresponden a funciones cuyo horario de inicio no pasó aún respecto del momento de la consulta.
- **SC-005**: Cuando no hay Sucursales con funciones hoy, o no quedan horarios disponibles hoy en la Sucursal elegida, el 100% de las veces el Cliente ve un mensaje explicativo en vez de una pantalla vacía sin contexto.
- **SC-006**: El 100% de las consultas hechas con una película o una Sucursal inválida (inexistente, o sin ninguna relación con la otra) devuelven un error explícito, distinguible del mensaje de "sin resultados hoy", permitiendo al Cliente diferenciar entre "no existe" y "existe pero no tiene funciones/horarios hoy".

## Assumptions

- El punto de partida de esta feature es una película ya elegida por el Cliente desde el Catálogo (feature `001-catalogo-peliculas`); esta feature no repite esa selección, solo la consume como dato de entrada.
- `Sucursal`, `Sala` y `Funcion` no tienen en esta feature funcionalidad de creación, edición ni administración: sus datos existen porque fueron sembrados (seed) directamente en la base de datos, tal como lo indica la descripción de la feature.
- El acceso a esta consulta es público, igual que el Catálogo (`001-catalogo-peliculas`): no requiere autenticación ni un rol específico.
- "Hoy" se determina por la fecha actual del servidor en el momento de la consulta.
- Una función cuyo horario de inicio ya pasó no se considera "disponible hoy": se excluye tanto del listado de horarios de la Sucursal (FR-008) como, si era la última función de hoy de esa Sucursal, de la lista de Sucursales con funciones hoy (ya que esa Sucursal dejaría de tener funciones disponibles hoy para esa película).
- El listado de Sucursales no depende de datos de geolocalización (no se ordena por cercanía ni se filtra por distancia); se asume orden alfabético por nombre como comportamiento por defecto razonable.
- No se valida en esta feature disponibilidad de asientos ni estado de reserva de una Funcion — eso corresponde a una feature futura de Reservas; aquí "disponible" se refiere únicamente a que el horario no haya pasado todavía.
