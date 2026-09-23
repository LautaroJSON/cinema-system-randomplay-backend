# Research: Mapa de Asientos de una Función

No quedaron `NEEDS CLARIFICATION` en el Technical Context del plan: el stack está fijado por la
constitución y el origen de la ocupación se resolvió en la sesión de clarificación de la spec. Este
documento registra las decisiones de diseño que había que tomar antes de Phase 1.

## Decisión 1: Modelar la disposición como filas con cantidad de asientos, no como lista de asientos

**Decision**: `Sala` pasa a tener una colección `Filas` de `FilaSala` (Value Object: `Letra` +
`CantidadAsientos`). Los `Asiento`s de la Sala no se persisten uno por uno: se derivan de las filas
(fila `C` con 12 asientos → `C1` … `C12`). `Asiento` es un Value Object (`Fila` + `Numero`) que existe
en el dominio para identificar posiciones y, a futuro, para que `Reserva` las referencie.

**Rationale**: La spec (Assumptions) fija que la disposición es "una lista de filas con cantidad de
asientos por fila", con numeración consecutiva desde 1 y sin pasillos ni huecos. Con esa regla, guardar
cada asiento sería redundante: una Sala de 12 filas × 14 asientos serían 168 filas en la base contra
12. Además, el formato filas + cantidad hace imposible por construcción que haya huecos en la
numeración o asientos duplicados (FR-003, FR-004), sin validaciones extra.

**Alternatives considered**:
- Tabla `Asientos` con una fila por asiento físico → rechazado por ahora: solo aporta si hay huecos,
  pasillos o tipos de butaca, que la spec deja explícitamente fuera. Si una feature futura los
  necesita, la migración es aditiva (se generan las filas de asiento a partir de la disposición).
- Disposición como columna JSON en `Salas` → rechazado: esconde la estructura en un blob, complica el
  `HasData` del seed y no aporta nada frente a una tabla hija pequeña.

## Decisión 2: Persistir `FilaSala` como colección owned de `Sala`

**Decision**: En EF Core, `Sala.Filas` se mapea con `OwnsMany` a una tabla `SalaFilas`
(`SalaId`, `Letra`, `CantidadAsientos`), con clave `(SalaId, Letra)`. Se carga automáticamente con la
`Sala`, sin `Include` explícito.

**Rationale**: `FilaSala` no tiene identidad propia ni sentido fuera de su `Sala`: es parte del
agregado. `OwnsMany` expresa exactamente eso, y la clave compuesta `(SalaId, Letra)` hace que la base
también rechace dos filas con la misma letra en una Sala (FR-004), además de la invariante de dominio.

**Alternatives considered**:
- Entidad independiente con su propio `Id` y `DbSet` → rechazado: le daría identidad a algo que es un
  Value Object y permitiría consultarla o modificarla por fuera del agregado `Sala`.

## Decisión 3: Puerto `IOcupacionAsientos` con un stub que no ocupa nada

**Decision**: Se define `IOcupacionAsientos` en `Application/Funciones/Ports` con
`Task<IReadOnlySet<Asiento>> ObtenerOcupados(Guid funcionId, CancellationToken)`. En esta feature se
implementa en `Infrastructure/Funciones/SinAsientosOcupados.cs`, que siempre devuelve un conjunto
vacío. La feature de Reservas reemplazará el registro en `Program.cs` por una implementación que lea
las reservas, sin tocar el handler ni el contrato HTTP (FR-013).

**Rationale**: Es la opción A elegida en la clarificación de la spec. Repite el patrón que ya funcionó
en `001-catalogo-peliculas` con `IFuncionAvailabilityChecker` / `NingunaFuncionDisponibleChecker`. El
puerto devuelve **ocupados** y no disponibles, porque es la pregunta que Reservas sabe responder ("qué
asientos tienen reserva para esta función"). Calcular los disponibles restando de la disposición es
regla de dominio y queda en `Sala` (ver Decisión 4).

**Alternatives considered**:
- Que el puerto devuelva directamente los disponibles → rechazado: obligaría a la futura
  implementación de Reservas a conocer la disposición de la Sala, mezclando dos responsabilidades.
- No crear puerto y devolver todos los asientos directo desde el handler → rechazado: la feature de
  Reservas tendría que modificar el handler, y los tests de esta feature no podrían ejercitar el caso
  "hay asientos ocupados" (User Story 2) con un fake.

## Decisión 4: El cálculo de disponibles vive en `Sala`

**Decision**: `Sala.AsientosDisponibles(IEnumerable<Asiento> ocupados)` devuelve, por cada fila en
orden alfabético, los números de asiento que no están en `ocupados`. Los ocupados que no pertenecen a
la Sala se ignoran.

**Rationale**: El Principio II exige que las reglas vivan en el dominio. "Disponible = de la
disposición, menos lo ocupado" es regla de negocio, y ponerla en `Sala` la hace testeable con tests
unitarios puros, sin mocks. Ignorar ocupados ajenos a la Sala garantiza FR-007 (nunca se devuelve un
asiento que no existe) aunque la fuente de ocupación tenga datos inconsistentes.

**Alternatives considered**:
- Hacer la resta en el query handler → rechazado: sacaría la regla del dominio (Principio II) y
  obligaría a testearla a través del handler.

## Decisión 5: Forma de la respuesta: disponibles agrupados por fila

**Decision**: La respuesta lista las filas de la Sala y, dentro de cada una, `cantidadAsientos` y el
arreglo de números disponibles (`"asientosDisponibles": [1, 2, 5, …]`). Junto a eso van los datos de
contexto de la función y los totales. Ver [contracts/mapa-asientos-api.md](./contracts/mapa-asientos-api.md).

**Rationale**: Cumple la decisión de la clarificación (solo se envían los disponibles; lo que falta
está ocupado) y le da al front la disposición completa para dibujar los ocupados. Agrupar por fila
evita repetir la letra en cada asiento y coincide con cómo se dibuja un mapa de sala: fila por fila,
del asiento 1 a `cantidadAsientos`, marcando como ocupado cada número que no esté en la lista.

**Alternatives considered**:
- Dos listas separadas, `filas` y `asientosDisponibles` con objetos `{fila, numero}` → rechazado: más
  verboso y obliga al front a cruzar las dos listas.
- Códigos de texto (`"C7"`) → rechazado: el front tendría que parsearlos para ubicar cada asiento.

## Decisión 6: Códigos HTTP y resultado explícito del caso de uso

**Decision**: El query handler devuelve un resultado explícito con tres casos: mapa encontrado,
función no encontrada y función no disponible. El controller los traduce a `200 OK`, `404 Not Found` y
`410 Gone`, respectivamente.

**Rationale**: FR-010 exige que "no disponible" se distinga de "no encontrada". `410 Gone` es el código
estándar para un recurso que existió pero ya no está disponible y no va a volver a estarlo, que es
justamente el caso de una función que ya comenzó. Un resultado con tres casos, en vez de `null` como en
la feature 002, sigue la indicación del Principio II de "devolver resultados explícitos de error" y
mantiene la decisión fuera del controller (Principio VIII).

**Alternatives considered**:
- `409 Conflict` → rechazado: describe un conflicto con el estado actual que el cliente podría
  resolver, y acá no hay nada que resolver.
- `404` para ambos casos con un body que los diferencie → rechazado: obliga al front a leer el body
  para distinguir los casos, cuando el código de estado ya puede decirlo.
- `null` para "no encontrada" más una excepción para "no disponible" → rechazado: usar excepciones
  para un resultado esperado del negocio.

## Decisión 7: Navegación `Sala.Sucursal` para el nombre de la Sucursal

**Decision**: `Sala` expone `Sucursal? Sucursal { get; private set; }` como navegación de solo
lectura. `FuncionRepository.ObtenerConSala(funcionId)` carga `Funcion → Sala → Sucursal` con
`Include`/`ThenInclude` (las `Filas` vienen solas por ser owned). El título de la película se obtiene
con `IPeliculaRepository.ObtenerPorId`, que además valida que esté activa (FR-009).

**Rationale**: Es el mismo criterio que la Decisión 4 de la feature 002 (`Funcion.Sala`). La FK
`Salas.SucursalId` ya existe, así que solo cambia la configuración de EF (`HasOne(s => s.Sucursal)` en
vez de `HasOne<Sucursal>()`) y no el esquema. Se evita crear un `ISucursalRepository` solo para leer un
nombre.

**Alternatives considered**:
- Método `ObtenerSucursalPorId` en `IFuncionRepository` → rechazado: una segunda ida a la base y un
  método fuera de lugar en el repositorio de funciones.

## Decisión 8: Seed de la disposición

**Decision**: La disposición de las 4 Salas ya sembradas se agrega con `HasData` en la nueva migración.
Se usan disposiciones distintas entre sí (grilla uniforme, filas de largo variable, sala chica) para
poder validar a mano FR-001 a FR-003 y el caso de filas irregulares. `DevDataSeeder` no cambia: las
Funciones que ya siembra referencian esas Salas.

**Rationale**: La disposición es dato de referencia estático, igual que `Sucursal` y `Sala`
(Decisión 2 de la feature 002), así que corresponde sembrarla en la migración. FR-016 excluye
cualquier ABM de disposición.

## Nota fuera de alcance

Siguen pendientes dos deudas de features anteriores que esta feature no toca, para no mezclar
alcances:
- `NingunaFuncionDisponibleChecker` del Catálogo sigue devolviendo `false` siempre.
- No hay CORS configurado para un front en otro origen.
