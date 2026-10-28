<!--
Sync Impact Report
==================
Version change: 4.0.0 → 4.0.1
Rationale: PATCH — refinamiento no semántico. Los ejemplos de los Principios II y VI nombraban el
           agregado `Reserva`, que la feature 007-carrito-anonimo renombró a `Carrito`. Las reglas no
           cambian.

Modified principles:
  - II. "DDD Ligero — Invariantes en el Dominio": la lista de agregados y el ejemplo de nombres en
    español pasan de `Reserva` a `Carrito`; el ejemplo de invariante habla del vencimiento del carrito.
  - VI. "Consistencia de Datos vía Outbox + Eventos": el ejemplo "reserva creada" pasa a "carrito creado".

Added sections: ninguna

Removed sections: ninguna

Templates / archivos dependientes revisados:
  ✅ .specify/templates/plan-template.md, spec-template.md, tasks-template.md — sin referencias
  ✅ Código: el agregado ya se llama `Carrito` (feature 007)

Follow-up TODOs: ninguno.

Historia: 3.0.0 → 4.0.0 (2026-10-28, MAJOR): Principio VII redefinido para el comprador anónimo con
token de carrito; email solo de entrega; defensas por carrito, origen y función.
-->

# Sistema de Reserva de Entradas de Cine — Constitución

Backend puro en .NET 10 / C# 12 / ASP.NET Core que expone una API para un cliente externo
(sin frontend propio). Esta constitución define los principios no negociables del proyecto.
Las palabras **DEBE**, **NO DEBE**, **DEBERÍA** y **PUEDE** se interpretan según su sentido
normativo habitual (RFC 2119).

## Core Principles

### I. Arquitectura Hexagonal — Regla de Dependencia hacia Adentro

La solución se organiza en capas `Domain`, `Application`, `Infrastructure` y `Api` (host HTTP).
La regla de dependencia apunta siempre hacia adentro:

- La capa `Domain` **NO DEBE** referenciar EF Core, ASP.NET Core, SignalR, Stripe, el SDK de
  OIDC ni ningún otro framework de infraestructura. Solo depende del BCL de .NET.
- La capa `Application` (casos de uso) **DEBE** depender únicamente de tipos de `Domain` y de
  interfaces (puertos) declaradas en `Domain` o `Application`. **NO DEBE** referenciar tipos de
  `Infrastructure`.
- La capa `Infrastructure` **DEBE** implementar esos puertos (EF Core, Stripe, SignalR, OIDC,
  reloj, mensajería) y **NO DEBE** ser referenciada por `Domain` ni `Application`.
- La composición (registro en el DI container, `Program.cs`) es el único lugar donde se
  conocen las implementaciones concretas.

**Rationale**: aislar las reglas de negocio de la tecnología permite sustituir infraestructura,
testear el dominio sin arranque de framework y razonar sobre el sistema por capas.

### II. DDD Ligero — Invariantes en el Dominio

Los agregados (`Pelicula`, `Sucursal`, `Sala`, `Funcion`, `Carrito`) encapsulan y protegen sus
propias invariantes.

- Reglas como "un asiento no puede reservarse dos veces para una función" o "un carrito vence
  a los N minutos si no se confirma el pago" **DEBEN** vivir dentro del agregado o en un domain
  service, nunca en controllers, handlers de infraestructura ni configuraciones del ORM.
- El estado de un agregado **DEBE** ser inválido de construir: los constructores y métodos
  mutadores rechazan transiciones ilegales lanzando excepciones de dominio o devolviendo
  resultados explícitos de error.
- Se **DEBEN** usar Value Objects donde aporten claridad y seguridad de tipos, como mínimo
  `Asiento` y `Dinero`. Los Value Objects son inmutables y validan sus argumentos.
- El ORM **PUEDE** mapear estos tipos, pero la persistencia no define las reglas.
- Los tipos del dominio (agregados, entidades y Value Objects) **DEBEN** nombrarse en español
  (ej. `Pelicula`, `Carrito`, `Asiento`), reflejando el lenguaje ubicuo del negocio en su idioma
  original.

**Rationale**: si las invariantes viven en un solo lugar y no se pueden eludir, la corrección
del negocio no depende de que cada caller recuerde validar.

### III. Bordes Explícitos con DTOs

Ningún endpoint expone entidades de dominio ni entidades mapeadas por EF Core directamente.

- Cada caso de uso **DEBE** recibir y devolver DTOs o `record` propios de la capa `Application`.
- El mapeo entre dominio y DTO **DEBE** ser explícito. Si se emplea una librería de mapeo, su
  configuración **DEBE** ser explícita (perfiles declarados) y **DEBE** estar cubierta por
  tests; queda prohibido el mapeo por convención implícita que oculte lógica.
- Los contratos HTTP (request/response models de la capa `Api`) **PUEDEN** ser distintos de los
  DTOs de `Application` si el borde HTTP lo requiere, y se mapean explícitamente igual que arriba.

**Rationale**: los DTOs desacoplan el esquema público del modelo interno, evitan fugas de
detalles de persistencia y hacen que los cambios de contrato sean visibles y revisables.

### IV. Inversión de Dependencias por Interfaces

Toda dependencia hacia el exterior del proceso o hacia infraestructura se consume a través de
una interfaz.

- Repositorios, pasarela de pago, notificador de eventos, publicador de outbox, proveedor de
  fecha/hora y cualquier servicio externo **DEBEN** definirse como interfaz en `Domain` o
  `Application`.
- Las implementaciones concretas viven en `Infrastructure` y se resuelven exclusivamente con el
  DI container nativo de ASP.NET Core. **NO DEBE** usarse un contenedor de terceros ni
  service-locator manual.
- Los tiempos de vida (`Singleton` / `Scoped` / `Transient`) se declaran de forma deliberada en
  el registro de servicios.

**Rationale**: programar contra interfaces habilita el testing, la sustitución de proveedores y
mantiene la regla de dependencia del Principio I.

### V. Testing por Criterio — Donde Aporta Valor

Los tests se escriben cuando reducen un riesgo real, no por obligación general. La decisión de
testear (o no) una tarea se toma con juicio, aplicando los criterios siguientes.

- Una tarea **DEBERÍA** llevar tests cuando incluye al menos uno de estos casos:
  - invariantes o reglas de negocio no triviales del dominio (validaciones, transiciones de
    estado, cálculos);
  - un caso de uso que modifica estado;
  - consultas con lógica de filtrado, ordenamiento o manejo de fechas/horas;
  - cualquier comportamiento donde un bug sería caro, difícil de detectar o silencioso.
- Una tarea **NO NECESITA** tests cuando su código es trivial: DTOs y `record` sin lógica,
  registro en el DI container, mapeos 1 a 1, constructores que solo asignan, y cambios de
  configuración o documentación. Agregar tests en estos casos se considera ruido, no calidad.
- Única obligación sin excepción: la **reserva concurrente de asientos** **DEBE** tener un test
  de integración de concurrencia (pedidos simultáneos sobre el mismo asiento) contra Postgres
  real.
- Cuando se testee persistencia, unicidad, restricciones o concurrencia, el test **DEBE** correr
  contra un motor de base de datos real levantado con Testcontainers; **NO DEBE** usarse el
  provider InMemory de EF Core para eso, porque no refleja el comportamiento del motor real.
- Los tests de `Domain` **DEBERÍAN** ejercitar los objetos reales, sin mocks pesados de
  colaboradores de dominio.
- En `/speckit-plan`, el plan **DEBE** indicar qué se testea y qué no, con una línea de
  justificación por cada decisión. En `/speckit-tasks`, las tareas de test se incluyen cuando la
  tarea cumple los criterios anteriores —sin necesidad de que la spec las pida explícitamente— y
  se omiten cuando no aportan valor.

**Rationale**: testear todo por obligación encarece cada cambio sin reducir riesgo en el código
trivial; testear por criterio concentra el esfuerzo donde un bug es caro. La concurrencia de
reservas se mantiene obligatoria porque sus condiciones de carrera solo aparecen contra el motor
real y descubrirlas en producción es inaceptable para un sistema de reservas.

### VI. Consistencia de Datos vía Outbox + Eventos

Los efectos secundarios externos no se disparan desde dentro de una transacción de negocio.

- Los cambios de estado relevantes (carrito creado, asiento liberado, pago confirmado) **DEBEN**
  registrarse como filas en una tabla `outbox` dentro de la **misma transacción** que persiste
  el cambio de negocio.
- Un proceso en background **DEBE** leer la tabla outbox y publicar los eventos hacia el hub de
  SignalR (u otro transporte) fuera de la transacción original.
- Dentro de una transacción de base de datos **NO DEBE** invocarse una llamada de red, un envío
  a SignalR, una petición a Stripe ni ningún otro efecto no transaccional.
- La publicación de eventos **DEBE** ser idempotente o tolerar reintentos (entrega at-least-once).

**Rationale**: escribir estado y notificar en la misma transacción crea inconsistencias cuando
una de las dos partes falla; el patrón outbox garantiza que la notificación siga al hecho
confirmado.

### VII. Autenticación y Autorización Explícitas

- El comprador (quien reserva y paga entradas) es **anónimo**: **NO DEBE** necesitar cuenta,
  contraseña, login ni verificación de email para reservar. El rol `Customer` no existe.
- Al elegir asientos el comprador obtiene un **carrito** (una reserva pendiente con retención
  corta) identificado por un **token de acceso** aleatorio y no adivinable. Ese token es la única
  credencial del comprador: solo quien lo presenta **PUEDE** ver, modificar, abandonar o pagar ese
  carrito o la reserva que resulte de él.
- **NO DEBE** ser posible acceder a un carrito o reserva ajena conociendo solo su id ni ningún
  otro dato del comprador. Ante una credencial ausente o ajena, la respuesta **DEBE** ser la misma
  que para un recurso inexistente.
- El email del comprador se pide recién al confirmar la compra, **solo** para entregar la
  entrada. **NO DEBE** usarse como credencial ni como requisito para reservar, y no se verifica.
- El sistema **DEBE** defenderse del acaparamiento de asientos con límites explícitos y
  configurables, como mínimo: tope de asientos por carrito, límite de carritos creados por origen
  en una ventana de tiempo y tope global de asientos retenidos por función. La creación de
  carritos **DEBE** exponer un punto de extensión para un desafío anti-bots (CAPTCHA). Los errores
  de estos límites **DEBEN** usar códigos estables que no revelen datos de otros compradores.
- El personal interno se autentica vía OIDC con JWT bearer; la `Api` valida el token y no
  gestiona contraseñas. Su autorización **DEBE** ser RBAC declarada explícitamente por endpoint o
  caso de uso, usando los roles `CinemaManager` y `Admin`.
- **NO DEBE** existir autorización implícita: un endpoint sin modalidad de acceso declarada
  (público, portador del carrito o personal con rol) se considera un defecto, no un endpoint
  público. El fallback **DEBE** ser cerrado y los endpoints públicos se marcan de forma
  explícita.

**Rationale**: en un cine se compra un boleto sin crear una cuenta ni verificar nada; exigirlo
agrega fricción sin frenar el acaparamiento, porque una persona puede verificar tantos emails o
alias como quiera. El token prueba la posesión del carrito, que es lo único que hace falta para
operarlo; el acaparamiento se controla donde de verdad se produce, limitando cuántos asientos
retiene cada carrito, cuántos carritos abre cada origen y cuántos asientos de una función pueden
estar retenidos a la vez. La seguridad implícita es la principal fuente de exposición accidental;
exigir declaración explícita convierte cada omisión en algo visible en revisión.

### VIII. Calidad de Código

- Nullable reference types **DEBE** estar habilitado en todos los proyectos de la solución.
- Todo I/O (base de datos, red, disco, pasarela de pago) **DEBE** ser `async`/`await` de punta a
  punta; **NO DEBE** bloquearse con `.Result`, `.Wait()` ni `GetAwaiter().GetResult()`.
- Los controllers (o endpoints minimal API) **NO DEBEN** contener lógica de negocio: solo
  traducen la petición HTTP a una invocación de caso de uso y el resultado a una respuesta HTTP.
- Las advertencias del compilador relacionadas con nulabilidad y análisis **DEBERÍAN** tratarse
  como errores en la build de CI.

**Rationale**: estas restricciones son baratas de mantener si se aplican desde el principio y
caras de introducir después; mantienen el código predecible y escalable.

## Restricciones de Plataforma y Tecnología

- Stack fijo: .NET 10, C# 12, ASP.NET Core. Cambiar la versión mayor de la plataforma requiere
  una enmienda a esta constitución.
- El proyecto es un **backend puro**: no incluye frontend ni renderizado de vistas; expone API
  (REST/JSON) y un hub de SignalR para notificaciones en tiempo real hacia clientes externos.
- Persistencia mediante EF Core contra un motor relacional real (el mismo motor en desarrollo,
  pruebas de integración y producción).
- Integraciones externas actuales: Stripe (pagos), un servicio de envío de email (entrega de
  la entrada al comprador) y un proveedor OIDC (identidad del personal interno). Todas se consumen a
  través de puertos según el Principio IV.
- Estructura de proyectos: `Domain`, `Application`, `Infrastructure`, `Api`, más proyectos de
  test (`*.UnitTests`, `*.IntegrationTests`).

## Flujo de Desarrollo y Puertas de Calidad

- Toda contribución llega mediante Pull Request. El PR **DEBE** describir qué principios toca y
  cómo los respeta.
- Puertas de CI obligatorias antes de merge:
  1. La solución compila con nullable habilitado y sin advertencias nuevas.
  2. Pasan todos los tests existentes (unitarios y de integración con Testcontainers).
  3. Si el cambio toca la reserva de asientos, existe y pasa su test de integración de
     concurrencia (Principio V).
- La revisión de código **DEBE** verificar explícitamente la regla de dependencia (Principio I),
  la ausencia de lógica de negocio en controllers (Principio VIII) y la ausencia de efectos
  externos dentro de transacciones (Principio VI).
- Cualquier atajo pedagógico o simplificación consciente **DEBE** documentarse en el PR/commit
  con su razón (ver Gobernanza).

## Governance

- Esta constitución tiene precedencia sobre cualquier otra práctica o convención del proyecto.
- **Enmiendas**: cualquier cambio a un principio o sección se propone en un PR que modifica este
  archivo, explica la motivación y actualiza la versión y las fechas. Requiere aprobación de un
  mantenedor del proyecto.
- **Política de versionado** (SemVer del documento):
  - MAJOR: eliminación o redefinición incompatible de un principio o de la gobernanza.
  - MINOR: adición de un principio o sección, o ampliación material de una guía existente.
  - PATCH: aclaraciones, correcciones de redacción y refinamientos no semánticos.
- **Cumplimiento**: toda revisión de PR verifica el cumplimiento de estos principios. Una
  desviación deliberada de un principio **DEBE** documentarse explícitamente en el PR o el
  mensaje de commit, indicando el principio afectado y la razón; **NO DEBE** introducirse en
  silencio. Las desviaciones no documentadas son motivo de rechazo del PR.
- Para guía de desarrollo en runtime, los comandos de Spec Kit (`/speckit-plan`,
  `/speckit-tasks`, `/speckit-implement`) leen esta constitución como fuente de verdad.

**Version**: 4.0.1 | **Ratified**: 2026-09-06 | **Last Amended**: 2026-10-28
