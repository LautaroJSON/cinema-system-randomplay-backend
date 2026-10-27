# Research: Precios de las Funciones

El stack está fijado por la constitución y las reglas de negocio se cerraron en la clarificación de la
spec (precio > 0, monto como texto, reservas previas con el precio actual), así que el Technical
Context del plan no tiene `NEEDS CLARIFICATION`. Este documento registra las decisiones de diseño
previas a Phase 1. Las más delicadas son cómo representar el dinero sin punto flotante de punta a punta
(Decisiones 1, 3 y 5) y cómo migrar los datos existentes (Decisión 6).

## Decisión 1: `Dinero` como Value Object en `Domain/Compartido`, con `decimal` y enum `Moneda`

**Decision**: `Dinero` es un `sealed record` en `Domain/Compartido` con `decimal Monto` y
`Moneda Moneda`. `Moneda` es un enum con un único valor, `ARS`. El constructor rechaza con
`DomainException` los montos negativos y los que tienen más de 2 decimales significativos
(`decimal.Round(monto, 2) != monto`), y normaliza la escala a 2 decimales (`5500` y `5500.000` quedan
como `5500.00`). Expone `Multiplicar(int cantidad)`, que rechaza cantidades negativas y devuelve un
`Dinero` nuevo en la misma moneda. Tiene una fábrica `Dinero.Ars(decimal monto)` por legibilidad.

**Rationale**: `decimal` es exacto en base 10, que es la única forma de cumplir "nunca punto
flotante" (Principio II, FR-002) sin inventar un tipo propio. Un `record` da inmutabilidad e igualdad
por valor, igual que `Asiento`. Como `decimal` compara por valor (`5500.0m == 5500.00m`), la igualdad
del record no depende de la escala. Va en `Compartido` porque lo usan `Funciones` y `Reservas`, y lo va
a usar la feature de pagos. Un enum hace imposible construir una moneda inválida y deja explícito que
hoy solo existe ARS. Agregar una moneda es agregar un valor, no cambiar el tipo.

**Alternatives considered**:
- Monto en centavos como `long` → rechazado: es exacto, pero obliga a convertir en cada borde y la
  clarificación eligió un monto en texto con 2 decimales, no centavos.
- Moneda como `string` validado ("ARS") → rechazado: es igual de flexible pero menos seguro de tipos,
  y abre la puerta a comparaciones de texto por todo el código.
- Que `Dinero` rechace el cero → rechazado: el cero es un monto válido (por ejemplo, un futuro
  descuento total). La regla "> 0" es de la función, no del dinero (Decisión 2).
- `Sumar`/`Restar` y comparación entre montos → no se agregan: ningún caso de uso de esta feature los
  necesita. Se suman cuando aparezcan (pagos, descuentos).

## Decisión 2: `Funcion.PrecioEntrada` obligatorio, mayor que cero y en ARS

**Decision**: `Funcion` agrega la propiedad `Dinero PrecioEntrada`, que pasa a ser un parámetro
obligatorio del constructor. El constructor lanza `DomainException` si es `null`, si el monto es cero
o si la moneda no es `ARS` (FR-001, FR-003). No se agrega un método para cambiar el precio: nada en la
API lo necesita (la administración de precios está fuera de alcance), y en los tests el cambio se hace
directo sobre la base.

**Rationale**: la invariante "toda función cuesta algo" pertenece a la función (Principio II). Hacer
el parámetro obligatorio obliga al compilador a señalar cada lugar que construye funciones: el
sembrado y los tests.

**Alternatives considered**:
- Un `CambiarPrecio` en `Funcion` → rechazado por ahora: sería código sin caller productivo. Se agrega
  cuando exista la administración de funciones.
- Un parámetro opcional con precio por defecto → rechazado: ocultaría funciones sin precio real y
  contradice FR-001.

## Decisión 3: La `Reserva` guarda solo el precio unitario; el total es derivado

**Decision**: `Reserva` agrega `Dinero PrecioUnitario`, que se persiste. `Total` es una propiedad
calculada, `PrecioUnitario.Multiplicar(Asientos.Count)`, que no se persiste. `ReservadorDeAsientos`
pasa `funcion.PrecioEntrada` a `Reserva.Crear`, y `Crear` rechaza un precio `null`.

**Rationale**: los asientos de una reserva no cambian después de crearla, así que el total derivado
queda congelado igual que el precio unitario (FR-008, FR-009) y no puede quedar inconsistente con él.
Guardar los dos crearía un dato redundante que habría que mantener coherente. Al ser `decimal × int`,
el total es exacto y no necesita redondeo.

**Alternatives considered**:
- Persistir también `Total` → rechazado: redundante. Si la feature de pagos necesita consultar totales
  por SQL, se agrega como columna calculada o se persiste en ese momento.
- Que la reserva lea el precio de la función al consultarla → rechazado: viola FR-009 (un cambio de
  precio afectaría reservas ya creadas).

## Decisión 4: El precio leído está protegido por el mismo bloqueo de la reserva

**Decision**: no hace falta un mecanismo nuevo. El caso de uso de reservar ya carga la función con
`ObtenerConSalaBloqueando` (`SELECT ... FOR UPDATE`, feature 004, Decisión 2). El precio que congela la
reserva es el de esa fila bloqueada. Un `UPDATE` concurrente del precio espera a que la reserva
confirme, o la reserva espera al `UPDATE` y lee el precio nuevo.

**Rationale**: resuelve el punto que la clarificación dejó para el plan (cambio de precio durante una
reserva en curso). La reserva siempre usa un único precio coherente, el vigente al crearse.

**Alternatives considered**: concurrencia optimista sobre el precio → innecesaria: el bloqueo
pesimista ya existe y cubre este caso.

## Decisión 5: Persistencia con `ComplexProperty` y `numeric(12,2)`

**Decision**: en EF Core, `PrecioEntrada` (en `Funcion`) y `PrecioUnitario` (en `Reserva`) se mapean
como complex types (`ComplexProperty`), con columnas en la misma tabla:
`PrecioEntrada_Monto numeric(12,2) NOT NULL` + `PrecioEntrada_Moneda varchar(3) NOT NULL`, y lo mismo
para `PrecioUnitario_*` en `Reservas`. La moneda se guarda como texto (`HasConversion<string>()`),
igual que `EstadoReserva`.

**Rationale**: los complex types de EF Core son la forma nativa de mapear Value Objects sin identidad
(ya usados como owned en `Asiento`, pero acá no hace falta tabla propia). `numeric(12,2)` es exacto en
Postgres, admite montos de hasta 9.999.999.999,99 y garantiza en el motor los 2 decimales. La moneda
como texto deja la base legible.

**Alternatives considered**:
- `OwnsOne` → rechazado: funciona, pero la semántica de owned entity (identidad oculta, tracking
  propio) es más de lo que un Value Object necesita. `ComplexProperty` es la recomendada para esto
  desde EF Core 8.
- `HasConversion` a un único `string` "5500.00 ARS" → rechazado: no permite consultar ni sumar por
  SQL, que la feature de pagos va a necesitar.

## Decisión 6: Migración con valor por defecto para las funciones y backfill de reservas

**Decision**: una migración `AddPrecios`:
1. Agrega a `Funciones` las columnas `PrecioEntrada_Monto` y `PrecioEntrada_Moneda` como `NOT NULL`
   con valores por defecto `5500.00` y `'ARS'`, de modo que las funciones ya existentes en la base de
   desarrollo queden con precio. Después quita los defaults: las filas nuevas deben traer su precio.
2. Agrega a `Reservas` las columnas `PrecioUnitario_Monto` y `PrecioUnitario_Moneda` como nullable.
3. Ejecuta `UPDATE "Reservas" r SET ... FROM "Funciones" f WHERE r."FuncionId" = f."Id"` para copiar el
   precio actual de la función (FR-012).
4. Pasa las dos columnas de `Reservas` a `NOT NULL`.

**Rationale**: es la clarificación de la spec (asignar el precio actual, no borrar) y deja la base
siempre con precios. Los pasos 2 a 4 evitan inventar un default arbitrario para las reservas. Las bases
de los tests de integración nacen vacías, así que solo afecta a desarrollo.

**Alternatives considered**: borrar reservas o recrear la base → descartado en la clarificación.

## Decisión 7: Monto como texto con 2 decimales solo en el borde HTTP

**Decision**: Domain y Application trabajan con `Dinero` y `DineroDto(decimal Monto, string Moneda)`.
En la Api, `DineroResponse(string Monto, string Moneda)` se construye con
`Monto.ToString("0.00", CultureInfo.InvariantCulture)` y `Moneda.ToString()`. Los campos nuevos son:

| Respuesta | Campo nuevo |
|---|---|
| Horario de hoy (`HorarioFuncionResponse`) | `precioEntrada` |
| Mapa de asientos (`MapaAsientosResponse`) | `precioEntrada` |
| Reserva (`ReservaResponse`, en crear, listar y consultar) | `precioUnitario`, `total` |

**Rationale**: la clarificación fijó el monto como texto (`"5500.00"`) para que ningún cliente lo
reciba como número flotante (FR-013). Hacerlo con un mapeo explícito en el `DesdeDto` de la Api, y no
con un `JsonConverter` global, respeta el Principio III (mapeo explícito, sin convención oculta) y no
afecta otros `decimal` que pudieran aparecer. `InvariantCulture` garantiza el punto decimal en
cualquier cultura del servidor. Los campos se agregan al final de cada record, así que el cambio es solo
aditivo (FR-007, FR-010).

**Alternatives considered**:
- Un `JsonConverter<decimal>` global que serialice como string → rechazado: es una convención
  implícita que cambiaría cualquier `decimal` futuro sin que se note en el contrato.
- Exponer `Dinero` del dominio directo en la respuesta → rechazado por el Principio III.

## Decisión 8: El evento `ReservaCreada` lleva precio unitario y total

**Decision**: `ReservaCreada` agrega `Dinero PrecioUnitario` y `Dinero Total`. Se serializan en el
payload del outbox (JSON) como objetos con monto y moneda.

**Rationale**: el consumidor natural del evento es la futura feature de pagos, que necesita el monto.
Agregar campos al payload es compatible: hoy no hay consumidores (feature 004, Complexity Tracking).

**Alternatives considered**: no tocar el evento → posible, pero obligaría a la feature de pagos a
reabrir este caso de uso.

## Decisión 9: Precios sembrados

**Decision**: `DevDataSeeder` siembra las tres funciones demo con precios distintos: Centro Sala 1 a
`5500.00`, Centro Sala 2 a `7250.50` y Norte Sala 1 a `6000.00` (FR-004).

**Rationale**: dos precios distintos como mínimo permiten verificar que cada función informa el suyo,
y `7250.50` ejercita los centavos de punta a punta.
