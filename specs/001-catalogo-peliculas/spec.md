# Feature Specification: Catálogo de Películas

**Feature Branch**: `001-catalogo-peliculas`

**Created**: 2026-09-17

**Status**: Draft

**Input**: User description: "Como usuario quiero ver una Catalogo de Peliculas"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver el listado de películas en cartelera (Priority: P1)

Un Cliente entra a la sección "Catálogo" y ve la lista de todas las películas activas disponibles, cada una mostrando al menos su título, duración y clasificación, para poder explorar la oferta antes de elegir una.

**Why this priority**: Es el punto de entrada de todo el flujo de reserva — sin poder ver el catálogo, ningún otro paso (elegir sucursal, función, asiento) es alcanzable. Es la funcionalidad mínima que ya entrega valor por sí sola (permite descubrir qué películas hay, incluso antes de reservar).

**Independent Test**: Se puede probar completamente navegando a la sección Catálogo y verificando que se listan las películas activas con su información básica, sin depender de ninguna otra feature (Sucursales, Funciones, Reservas).

**Acceptance Scenarios**:

1. **Given** existen películas activas cargadas en el sistema, **When** el usuario entra a la sección Catálogo, **Then** ve la lista completa de esas películas con título, duración y clasificación.
2. **Given** una película fue desactivada por un `CinemaManager`/`Admin`, **When** el usuario entra a la sección Catálogo, **Then** esa película no aparece en la lista.
3. **Given** no hay ninguna película activa en el sistema, **When** el usuario entra a la sección Catálogo, **Then** ve un mensaje claro indicando que no hay películas disponibles, sin errores.
4. **Given** una película activa no tiene ninguna función programada en ninguna sucursal, **When** el usuario entra a la sección Catálogo, **Then** esa película igual aparece en el listado, pero marcada visualmente como "sin funciones disponibles".

---

### User Story 2 - Ver el detalle de una película (Priority: P2)

Desde el listado del Catálogo, el Cliente selecciona una película puntual y ve su información completa (título, duración, clasificación, sinopsis), para decidir si quiere continuar el flujo de reserva con esa película.

**Why this priority**: Depende de que exista el listado (User Story 1), pero agrega el valor de decisión informada — es el siguiente paso natural del flujo antes de elegir sucursal/función. No es bloqueante para que el listado por sí solo entregue valor.

**Independent Test**: Se puede probar seleccionando cualquier película del listado y verificando que se muestra su información completa, de forma independiente a Sucursales/Funciones/Reservas (que son features futuras).

**Acceptance Scenarios**:

1. **Given** el usuario está viendo el listado del Catálogo, **When** selecciona una película activa, **Then** ve el detalle completo: título, duración, clasificación y sinopsis.
2. **Given** el usuario tiene abierto el detalle de una película, **When** esa película es desactivada por un `CinemaManager`/`Admin` mientras la está viendo, **Then** un intento posterior de acceder a ese detalle (ej. recargar) indica que la película ya no está disponible, en vez de mostrar datos obsoletos.

---

### User Story 3 - Buscar películas por título (Priority: P3)

Desde la sección Catálogo, el Cliente escribe parte de un título en un buscador y ve la lista filtrada a las películas activas que coinciden, para encontrar más rápido una película puntual cuando el catálogo es grande.

**Why this priority**: Es una mejora de usabilidad sobre el listado ya existente (User Story 1), no una capacidad nueva de negocio — el catálogo ya es completamente usable (aunque menos cómodo) sin esta historia.

**Independent Test**: Se puede probar tipeando un texto en el buscador sobre un catálogo ya cargado y verificando que el listado se reduce a las coincidencias, sin depender de las demás historias.

**Acceptance Scenarios**:

1. **Given** el catálogo tiene varias películas activas cargadas, **When** el usuario escribe un texto que coincide parcialmente con el título de alguna, **Then** el listado se filtra mostrando solo las películas cuyo título contiene ese texto.
2. **Given** el usuario escribe un texto que no coincide con ningún título activo, **When** se aplica el filtro, **Then** ve un mensaje indicando que no se encontraron resultados, sin errores.

---

### Edge Cases

- ¿Qué pasa si el catálogo tiene una cantidad muy grande de películas activas? (se asume paginación o scroll, ver Assumptions)
- ¿Qué pasa si el título o la sinopsis de una película son inusualmente largos? El listado debe truncar visualmente sin romper el layout; el detalle debe mostrar el texto completo.
- ¿Qué pasa si dos películas activas tienen el mismo título? Ambas se listan igual, se distinguen por su identidad interna, no por el título (el título no es una clave única de negocio).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE mostrar en la sección Catálogo la lista de todas las películas activas, incluyendo al menos título, duración y clasificación de cada una.
- **FR-002**: El sistema NO DEBE mostrar películas inactivas/desactivadas en el listado del Catálogo.
- **FR-003**: Los usuarios DEBEN poder seleccionar una película del listado para ver su detalle completo.
- **FR-004**: El sistema DEBE mostrar en el detalle de una película: título, duración, clasificación y sinopsis.
- **FR-005**: El sistema DEBE mostrar un mensaje claro de "sin resultados" cuando el catálogo (o el filtro de búsqueda) no tiene ninguna película para mostrar, en vez de una lista vacía sin explicación.
- **FR-006**: Los usuarios DEBEN poder filtrar el listado del Catálogo por coincidencia parcial de título.
- **FR-007**: El acceso de lectura al Catálogo (listado y detalle) NO DEBE requerir autenticación — el rol `Customer` (y visitantes no autenticados) puede navegarlo libremente.
- **FR-008**: El Catálogo DEBE mostrar todas las películas activas, tengan o no funciones programadas en alguna sucursal.
- **FR-009**: El sistema DEBE marcar visualmente en el listado y en el detalle a las películas activas que no tengan ninguna `Funcion` programada en ninguna `Sucursal`, indicando claramente que están "sin funciones disponibles".

### Key Entities

- **Pelicula**: representa una película del catálogo. Atributos relevantes para esta feature: título, duración, clasificación, sinopsis, y estado activo/inactivo (solo las activas son visibles en el Catálogo). La gestión de creación/edición/desactivación de una `Pelicula` es responsabilidad de otra feature (administración de catálogo, rol `CinemaManager`/`Admin`) — fuera de alcance aquí.
- **Funcion**: no se gestiona en esta feature, pero el Catálogo necesita saber, de forma solo-lectura, si existe al menos una `Funcion` programada para una `Pelicula` (en cualquier `Sucursal`), únicamente para decidir si mostrar la marca "sin funciones disponibles".

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un usuario puede ver el listado completo de películas activas en menos de 2 segundos desde que entra a la sección Catálogo.
- **SC-002**: El 100% de las películas mostradas en el Catálogo están activas; ninguna película desactivada aparece jamás en el listado.
- **SC-003**: Un usuario puede llegar al detalle completo de una película específica en 2 pasos o menos desde que entra al Catálogo (entrar a la sección + seleccionar la película).
- **SC-004**: Cuando el catálogo o una búsqueda no tiene resultados, el 100% de las veces el usuario ve un mensaje explicativo en vez de una pantalla vacía sin contexto.
- **SC-005**: El 100% de las películas activas sin funciones programadas se muestran con la marca "sin funciones disponibles", sin excepciones.

## Assumptions

- El Catálogo es de acceso público (no requiere que el usuario esté autenticado) para poder explorarlo antes de decidir reservar, siguiendo el patrón estándar de apps de cine.
- El detalle de una película incluye título, duración, clasificación y sinopsis; no se incluyen pósters/imágenes ni trailers en esta primera versión — se pueden sumar en una iteración futura sin romper esta spec.
- Esta feature no depende de la gestión de `Sucursal` ni `Funcion` (esas siguen siendo features futuras separadas): el Catálogo lista/filtra películas de forma independiente a si están programadas. La única dependencia es de **lectura**: saber si existe o no al menos una `Funcion` para marcar la película como "sin funciones disponibles" (FR-009); mientras esa feature no exista, se asume que ninguna película tiene funciones y todas se muestran con esa marca.
- Se asume que la cantidad de películas activas en un momento dado es moderada (decenas, no miles); si el volumen creciera mucho, se resolvería con paginación estándar, sin cambiar el alcance de esta spec.
- La búsqueda por título (User Story 3) es una mejora de UX de prioridad baja, no bloqueante para considerar esta feature entregable con valor real.
