# Feature Specification: Precios de las Funciones

**Feature Branch**: `005-precios-funciones`

**Created**: 2026-10-27

**Status**: Draft

**Input**: User description: "Precios de las Funciones. Hoy ninguna función ni reserva tiene precio: no existe el concepto de monto en el sistema. Esta feature agrega el precio de la entrada para que el Cliente sepa cuánto va a pagar y para que, más adelante, la feature de pagos tenga un monto que cobrar. Cada Función tiene un precio por entrada, en una única moneda (ARS); todos los asientos de una función valen lo mismo. El precio se representa con el Value Object `Dinero` (monto y moneda): inmutable, sin montos negativos ni más de 2 decimales, nunca con punto flotante. El Cliente ve el precio antes de reservar: los horarios de hoy por Sucursal y el mapa de asientos informan el precio por entrada de cada función, con cambios solo aditivos en las respuestas. Al crear una reserva, esta congela el precio unitario vigente y calcula su total (precio unitario × cantidad de asientos); si el precio de la función cambia después, las reservas ya creadas mantienen su precio. Las respuestas de las reservas del Cliente (crear, listar y consultar) informan el precio unitario y el total. El precio se carga en los datos sembrados de desarrollo. Fuera de alcance: pago y confirmación (Stripe), administración de precios vía API, descuentos, promociones, precios por tipo de asiento o por día, impuestos y múltiples monedas."

## Clarifications

### Session 2026-10-27

- Q: ¿Puede una función tener precio cero (función gratuita), o el precio por entrada debe ser siempre mayor que cero? → A: Siempre mayor que cero; el Value Object `Dinero` en sí admite cero.
- Q: ¿Cómo debería verse un monto en las respuestas para el cliente externo? → A: Como un objeto con el monto en texto con exactamente 2 decimales y la moneda, ej. `{ "monto": "5500.00", "moneda": "ARS" }`.
- Q: ¿Qué se hace con las reservas que ya existen en la base de desarrollo al aplicar esta feature? → A: Se les asigna el precio vigente de su función como precio unitario y se calcula su total; no se borran.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver el precio de la entrada antes de reservar (Priority: P1)

Un visitante (autenticado o no) que consulta los horarios de hoy de una película en una Sucursal (feature `002-horarios-sucursal-hoy`) ve, junto a cada horario, cuánto cuesta una entrada para esa función. Al abrir el mapa de asientos de una función (feature `003-mapa-asientos-funcion`) ve también el precio por entrada. Así puede decidir qué función le conviene y cuántos asientos reservar sabiendo cuánto va a pagar.

**Why this priority**: Es la información de precio que el Cliente necesita para decidir; sin ella, el resto de la feature (precio en la reserva) no tiene contexto. Entrega valor por sí sola aunque las reservas todavía no muestren montos.

**Independent Test**: Se puede probar sembrando funciones con precios distintos, consultando los horarios de hoy y el mapa de asientos sin autenticación, y verificando que cada función informa su precio por entrada en ARS y que todos los campos que ya existían siguen presentes con el mismo nombre y valor.

**Acceptance Scenarios**:

1. **Given** una película con dos funciones hoy en una Sucursal, una a ARS 5.500,00 y otra a ARS 7.250,50, **When** cualquier persona consulta los horarios de hoy de esa película en esa Sucursal, **Then** cada horario informa su precio por entrada: monto 5500.00 y moneda ARS para la primera, monto 7250.50 y moneda ARS para la segunda.
2. **Given** una función con precio ARS 6.000,00, **When** cualquier persona consulta su mapa de asientos, **Then** la respuesta informa el precio por entrada (monto 6000.00, moneda ARS) además de todo lo que ya informaba.
3. **Given** un cliente del sistema que ya consumía los horarios de hoy y el mapa de asientos antes de esta feature, **When** los consulta después de esta feature, **Then** encuentra todos los campos que ya existían, con los mismos nombres y valores, y sigue sin necesitar autenticación; el precio aparece solo como información adicional.

---

### User Story 2 - La reserva congela el precio y calcula su total (Priority: P2)

Un Cliente autenticado con rol `Customer` reserva asientos de una función (feature `004-reserva-asientos-funcion`). La reserva guarda el precio por entrada vigente en ese momento y su total, que es el precio unitario multiplicado por la cantidad de asientos reservados. La respuesta de la creación ya le informa al Cliente cuánto debe pagar. Si después cambia el precio de la función, la reserva conserva el precio con el que se hizo.

**Why this priority**: Es el monto que la futura feature de pagos va a cobrar y la garantía de que al Cliente no le cambian el precio después de reservar. Depende de que las funciones tengan precio (User Story 1).

**Independent Test**: Se puede probar reservando 3 asientos de una función a ARS 5.500,00 y verificando que la reserva informa precio unitario 5500.00 y total 16500.00 en ARS; luego cambiando el precio de la función en los datos y verificando que esa reserva sigue informando los mismos montos, mientras que una reserva nueva usa el precio nuevo.

**Acceptance Scenarios**:

1. **Given** una función con precio ARS 5.500,00 y los asientos C5, C6 y C7 disponibles, **When** un Cliente los reserva, **Then** la reserva creada informa precio unitario ARS 5.500,00 y total ARS 16.500,00.
2. **Given** una función con precio ARS 7.250,50, **When** un Cliente reserva 1 asiento, **Then** la reserva informa precio unitario y total ARS 7.250,50.
3. **Given** una reserva creada sobre 2 asientos cuando la función costaba ARS 5.500,00, **When** el precio de la función pasa a ARS 6.000,00, **Then** esa reserva sigue informando precio unitario ARS 5.500,00 y total ARS 11.000,00, y una reserva nueva de 2 asientos en esa función informa precio unitario ARS 6.000,00 y total ARS 12.000,00.
4. **Given** un pedido de reserva que el sistema rechaza (asientos no disponibles, inexistentes, función comenzada, máximo superado, etc.), **When** el Cliente recibe el error, **Then** el rechazo es igual al que existía antes de esta feature y no se calcula ni guarda ningún monto.

---

### User Story 3 - Ver los montos en mis reservas (Priority: P3)

Un Cliente autenticado que consulta la lista de sus reservas o una reserva puntual ve, en cada una, el precio unitario y el total con los que fue creada, cualquiera sea su estado (pendiente de pago, expirada o cancelada).

**Why this priority**: Le da al Cliente visibilidad sobre cuánto le va a costar lo que retuvo, pero no es imprescindible: el monto ya se le informa al crear la reserva.

**Independent Test**: Se puede probar creando reservas con distintas cantidades de asientos y en funciones con distintos precios, y verificando que la lista y la consulta puntual informan para cada una el precio unitario y el total correctos.

**Acceptance Scenarios**:

1. **Given** un Cliente con una reserva de 2 asientos a ARS 5.500,00 y otra de 4 asientos a ARS 7.250,50, **When** consulta sus reservas, **Then** la primera informa precio unitario ARS 5.500,00 y total ARS 11.000,00, y la segunda precio unitario ARS 7.250,50 y total ARS 29.002,00.
2. **Given** un Cliente consulta una reserva puntual propia, **When** recibe el resultado, **Then** ve el precio unitario y el total, además de todo lo que ya informaba.
3. **Given** una reserva cancelada o expirada, **When** su dueño la consulta, **Then** sigue informando el precio unitario y el total con los que fue creada.
4. **Given** un cliente del sistema que ya consumía las respuestas de reservas antes de esta feature, **When** las consulta después, **Then** encuentra todos los campos que ya existían con los mismos nombres y valores.

---

### Edge Cases

- ¿Qué pasa si el precio tiene centavos? Se informa como texto con exactamente 2 decimales y sin pérdida de precisión (ej. `"7250.50"`, nunca `7250.4999…`); un precio entero también lleva sus 2 decimales (`"5500.00"`).
- ¿Puede el total tener más de 2 decimales? No: el precio unitario tiene a lo sumo 2 decimales y la cantidad de asientos es un entero, así que el total es exacto y no requiere redondeo.
- ¿Qué pasa si se intenta cargar un precio negativo, con más de 2 decimales o en una moneda distinta de ARS? Se rechaza; el dato no puede quedar cargado en el sistema.
- ¿Qué pasa si se intenta cargar una función sin precio o con precio cero? Se rechaza: toda función tiene un precio por entrada mayor que cero.
- ¿Qué pasa con las reservas que ya existían antes de esta feature (datos de desarrollo)? Se les asigna el precio vigente de su función como precio unitario y su total correspondiente, de modo que toda reserva informe montos.
- ¿Qué pasa si el precio de una función cambia mientras se procesa un pedido de reserva? La reserva usa un único precio unitario, el vigente al momento de crearse, y su total es coherente con ese precio.
- ¿Qué pasa en una película sin funciones hoy en la Sucursal? Los horarios siguen respondiendo igual que antes (sin horarios); no aparece ningún precio.

## Requirements *(mandatory)*

### Functional Requirements

**Precio de la función**

- **FR-001**: Toda función DEBE tener un precio por entrada, expresado como un monto en pesos argentinos (ARS). Todos los asientos de una función DEBEN valer ese mismo precio.
- **FR-002**: Un monto de dinero DEBE tener una moneda, NO DEBE ser negativo y NO DEBE tener más de 2 decimales. Es inmutable: un cambio de precio es un monto nuevo, no la modificación de uno existente. Los montos DEBEN representarse y calcularse de forma exacta, sin aritmética de punto flotante.
- **FR-003**: El precio por entrada de una función DEBE ser mayor que cero. El sistema DEBE rechazar una función sin precio, con precio cero o negativo, con más de 2 decimales, o en una moneda distinta de ARS.
- **FR-004**: El precio de las funciones DEBE cargarse en los datos sembrados de desarrollo, con al menos dos precios distintos entre las funciones sembradas, de modo que se pueda verificar que cada función informa el suyo.

**Precio visible antes de reservar**

- **FR-005**: La consulta de horarios de hoy de una película por Sucursal DEBE informar, para cada horario, el precio por entrada de esa función (monto y moneda).
- **FR-006**: El mapa de asientos de una función DEBE informar el precio por entrada de esa función (monto y moneda).
- **FR-007**: Los cambios en las respuestas de horarios de hoy y del mapa de asientos DEBEN ser solo aditivos: NO DEBE quitarse, renombrarse ni cambiar de significado ningún campo existente, y ambas consultas DEBEN seguir siendo públicas.

**Precio en la reserva**

- **FR-008**: Al crearse, una reserva DEBE registrar como precio unitario el precio por entrada de la función vigente en ese momento, y como total el precio unitario multiplicado por la cantidad de asientos de la reserva, ambos en la misma moneda.
- **FR-009**: El precio unitario y el total de una reserva NO DEBEN cambiar después de su creación, aunque cambie el precio de la función, y se conservan en cualquier estado de la reserva (pendiente de pago, expirada o cancelada).
- **FR-010**: Las respuestas de crear una reserva, listar las reservas del Cliente y consultar una reserva puntual DEBEN informar el precio unitario y el total (monto y moneda). Estos cambios DEBEN ser solo aditivos respecto de las respuestas de la feature 004.
- **FR-011**: Los rechazos de pedidos de reserva DEBEN mantenerse iguales a los de la feature 004; un pedido rechazado NO DEBE registrar montos.
- **FR-012**: Las reservas existentes antes de esta feature DEBEN quedar con precio unitario igual al precio de su función al aplicarse la feature, y su total correspondiente.

**Formato de los montos**

- **FR-013**: En todas las respuestas, cada monto DEBE informarse como un objeto con dos datos: el monto, como texto con exactamente 2 decimales y punto decimal (ej. `"5500.00"`, nunca un número), y la moneda, con su código ISO 4217 (`"ARS"`). Ejemplo: `{ "monto": "5500.00", "moneda": "ARS" }`. El formato DEBE ser el mismo en horarios, mapa de asientos y reservas.

### Key Entities

- **Dinero**: Value Object que representa un monto en una moneda. Atributos: monto (no negativo, como máximo 2 decimales, exacto) y moneda (en esta feature, solo ARS). Inmutable; permite multiplicar por una cantidad entera de asientos para obtener un total.
- **Funcion**: entidad existente. Incorpora su precio por entrada (un Dinero mayor que cero), único para todos sus asientos.
- **Reserva**: agregado existente (feature 004). Incorpora su precio unitario (copia del precio de la función al crearse) y su total (precio unitario × cantidad de asientos), ambos inmutables después de la creación.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de los horarios de hoy y de los mapas de asientos informan el precio por entrada de su función, y el 100% de los campos que existían antes de la feature siguen presentes con el mismo nombre y valor.
- **SC-002**: El Cliente conoce el precio por entrada antes de reservar, sin consultas adicionales a las que ya hacía (horarios y mapa de asientos).
- **SC-003**: En el 100% de las reservas creadas, el total es exactamente igual al precio unitario multiplicado por la cantidad de asientos, sin diferencias de centavos.
- **SC-004**: El 100% de las reservas creadas antes de un cambio de precio de su función siguen informando el precio y el total originales después del cambio.
- **SC-005**: El 100% de los intentos de cargar un precio inválido (negativo, cero, con más de 2 decimales o en otra moneda) se rechazan.

## Assumptions

- La única moneda es ARS; no hay conversión ni múltiples monedas.
- El precio por entrada de una función debe ser mayor que cero: no existen funciones gratuitas en esta feature. El Value Object `Dinero` en sí admite cero (por ejemplo, como resultado de un cálculo), pero no negativos.
- Como las funciones no tienen administración vía API, "cambiar el precio de una función" ocurre solo modificando los datos (sembrado o directamente en la base); el sistema no expone una operación para hacerlo.
- Los precios sembrados son valores ficticios razonables para desarrollo (del orden de miles de pesos), con al menos dos precios distintos.
- Las reservas preexistentes en entornos de desarrollo reciben el precio actual de su función (FR-012); no hay datos de producción que preservar.
- La forma de cada monto está fijada por FR-013; los nombres de los campos que lo contienen en cada respuesta (ej. precio por entrada, precio unitario, total) se definen en el plan, respetando FR-007 y FR-010.
- Fuera de alcance: pago y confirmación de la reserva (Stripe), administración de precios vía API, descuentos, promociones, precios por tipo de asiento, por fila o por día, impuestos y múltiples monedas.
