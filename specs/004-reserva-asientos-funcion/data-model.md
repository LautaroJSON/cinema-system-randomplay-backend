# Data Model: Reserva de Asientos de una Función

## EstadoReserva (enum, nuevo, Domain/Reservas)

| Valor | Se guarda | Significado |
|---|---|---|
| `PendienteDePago` | Sí | Retiene sus asientos mientras `ahora < VenceEn`. |
| `Cancelada` | Sí | El Cliente la canceló. Estado final. |
| `Expirada` | **No** | Estado efectivo de una `PendienteDePago` con `VenceEn <= ahora`. Se calcula, nunca se escribe ([research.md](./research.md), Decisión 3). |

## ReglasReserva (record, nuevo, Domain/Reservas)

| Campo | Tipo | Valor por defecto | Reglas |
|---|---|---|---|
| `MinutosRetencion` | `int` | `10` | Mayor a 0. |
| `MaximoAsientosPorCliente` | `int` | `10` | Mayor a 0. Tope por reserva **y** por Cliente y función (FR-008). |

Se construye desde la sección de configuración `Reservas` y se registra como `Singleton`. Lanza
`DomainException` si algún valor es inválido.

## Reserva (aggregate root, nuevo, Domain/Reservas)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | Generado al crear. |
| `FuncionId` | `Guid` | Obligatorio. |
| `ClienteId` | `string` | Obligatorio, no vacío, hasta 255 caracteres. Es el `sub` del token (Decisión 9). |
| `Asientos` | `IReadOnlyList<Asiento>` | De 1 a `MaximoAsientosPorCliente`, sin repetidos. Se exponen ordenados por fila y número. |
| `Estado` | `EstadoReserva` | Estado **guardado**: `PendienteDePago` o `Cancelada`. |
| `CreadaEn` | `DateTimeOffset` | Momento de creación (de `IReloj`). |
| `VenceEn` | `DateTimeOffset` | `min(CreadaEn + MinutosRetencion, FechaHoraInicio de la función)` (FR-002). |
| `CanceladaEn` | `DateTimeOffset?` | Se completa al cancelar. |

**Creación**: solo a través de `ReservadorDeAsientos` (constructor `internal` o factory `internal
static`). Valida: `ClienteId` no vacío, cantidad de asientos entre 1 y el máximo, sin repetidos. Lanza
`DomainException` si se viola alguna. El handler valida antes el pedido HTTP para responder `400`, así
que esta excepción indica un bug, no un error del usuario.

**Comportamiento**:
- `EstadoEn(ahora)` → `Cancelada` si `Estado == Cancelada`; `Expirada` si
  `Estado == PendienteDePago && VenceEn <= ahora`; si no, `PendienteDePago`. Hay también una sobrecarga
  `static EstadoEn(EstadoReserva guardado, DateTimeOffset venceEn, DateTimeOffset ahora)` para el
  puerto de lectura (Decisión 12).
- `RetieneAsientosEn(ahora)` → `EstadoEn(ahora) == PendienteDePago`.
- `Cancelar(ahora)` → devuelve un resultado explícito: si `EstadoEn(ahora) == PendienteDePago`, pasa a
  `Cancelada`, guarda `CanceladaEn = ahora` y devuelve éxito; si no, devuelve
  `NoCancelable(estadoActual)` sin cambiar nada (FR-012, FR-013).

**Transiciones** (FR-014):

```text
PendienteDePago ──(ahora >= VenceEn)──▶ Expirada    (calculada)
PendienteDePago ──Cancelar(ahora)─────▶ Cancelada   (guardada)
Expirada, Cancelada: finales
```

## ReservadorDeAsientos (domain service, nuevo, Domain/Reservas)

`Reservar(Funcion funcion, IReadOnlyList<Reserva> reservasVigentesDeLaFuncion, string clienteId,
IReadOnlyList<Asiento> pedidos, DateTimeOffset ahora, ReglasReserva reglas) → ResultadoReserva`

`funcion` llega con su `Sala` cargada. Evalúa en este orden y devuelve el **primer** error:

| # | Regla | Resultado si falla |
|---|---|---|
| 1 | `!funcion.YaComenzo(ahora)` | `FuncionNoDisponible` |
| 2 | Todo pedido pertenece a la disposición (`Sala.Contiene(asiento)`) | `AsientosInexistentes(lista)` |
| 3 | Ningún pedido está en `Asientos` de una reserva vigente | `AsientosNoDisponibles(lista)` |
| 4 | Asientos vigentes del Cliente en la función + pedidos ≤ máximo | `LimiteSuperado(asientosRestantes)` |
| 5 | — | `Ok(nuevaReserva)` con `VenceEn` calculado |

Las listas de error van ordenadas por fila y número y sin repetidos. Las reglas de forma (vacío, más
de 10, repetidos) las valida el handler antes de entrar al dominio, por eso no figuran acá. La propia
`Reserva` también las vuelve a garantizar.

`ResultadoReserva` es un record con `Tipo` (enum) y los datos de cada caso, igual que
`ObtenerMapaAsientosResultado`.

## Asiento (Value Object, Domain/Funciones, sin cambios)

Se reutiliza tal cual de la feature 003 (`Fila` `A`–`Z`, `Numero` ≥ 1, igualdad por valor).

## Sala (Domain/Funciones, se modifica)

Se agrega `bool Contiene(Asiento asiento)`: `true` si existe una fila con `Letra == asiento.Fila` y
`asiento.Numero <= CantidadAsientos`. Lo usa la regla 2 del reservador.

## Funcion (Domain/Funciones, sin cambios)

Se reutiliza `YaComenzo(ahora)` y `FechaHoraInicio` para calcular `VenceEn`.

## Eventos de outbox (nuevos, Domain/Reservas)

| Evento | Cuándo | Datos |
|---|---|---|
| `ReservaCreada` | Al crear una reserva | `ReservaId`, `FuncionId`, `Asientos`, `VenceEn`, `OcurridoEn` |
| `ReservaCancelada` | Al cancelar | `ReservaId`, `FuncionId`, `AsientosLiberados`, `OcurridoEn` |

Son `record` del dominio y no llevan `ClienteId`, porque no se van a difundir a otros clientes. El
handler los entrega a `IOutbox` dentro de la misma transacción (Principio VI, Decisión 5).

## Puertos

| Puerto | Capa | Métodos |
|---|---|---|
| `IReservaRepository` | Domain/Reservas | `ListarVigentesDeFuncion(funcionId, ahora)`, `ObtenerPorId(id)`, `Agregar(reserva)` |
| `IUnidadDeTrabajo` | Application/Compartido/Ports | `EjecutarEnTransaccion(Func<CancellationToken, Task<T>>)` (abre la transacción, confirma si la función termina sin excepción, guarda cambios y hace rollback si falla) |
| `IFuncionRepository` | Domain/Funciones (se modifica) | + `ObtenerConSalaBloqueando(funcionId)`: toma `SELECT ... FOR UPDATE` sobre la fila de la función y la devuelve con su Sala y Sucursal. Requiere una transacción activa. |
| `IOutbox` | Application/Compartido/Ports | `Registrar(object evento)`: agrega el mensaje al contexto actual, que se guarda en la transacción en curso. |
| `IConsultaReservas` | Application/Reservas/Ports | `ListarDeCliente(clienteId)`, `ObtenerDeCliente(reservaId, clienteId)` → `ReservaLecturaDto` con datos guardados y contexto de la función. |
| `IOcupacionAsientos` | Application/Funciones/Ports (sin cambios) | Nueva implementación: `ReservasRetienenAsientos`. |

## Casos de uso (Application/Reservas)

### `ReservarAsientosCommand(FuncionId, ClienteId, IReadOnlyList<AsientoPedidoDto> Asientos)`

1. Valida la forma: 1 a `MaximoAsientosPorCliente` elementos, fila de una letra `A`–`Z`, número ≥ 1,
   sin repetidos. Si falla → `PedidoInvalido(errores)`.
2. En `IUnidadDeTrabajo.EjecutarEnTransaccion`:
   1. `IFuncionRepository.ObtenerConSalaBloqueando(funcionId)`; si es `null` → `FuncionNoEncontrada`.
   2. `IPeliculaRepository.ObtenerPorId`; si no existe o está inactiva → `FuncionNoEncontrada`.
   3. `IReservaRepository.ListarVigentesDeFuncion(funcionId, ahora)`.
   4. `ReservadorDeAsientos.Reservar(...)`. Si hay error, se devuelve sin escribir nada.
   5. Si es `Ok`: `IReservaRepository.Agregar` + `IOutbox.Registrar(new ReservaCreada(...))`.
3. Devuelve `ReservarAsientosResultado` con `ReservaDto` o el error.

### `CancelarReservaCommand(ReservaId, ClienteId)`

En transacción: `IReservaRepository.ObtenerPorId`. Si no existe o `ClienteId` no coincide →
`ReservaNoEncontrada`. Si existe, `reserva.Cancelar(ahora)`: si devuelve `NoCancelable(estado)` →
`NoCancelable`; si no, `IOutbox.Registrar(new ReservaCancelada(...))`. No hace falta bloquear la
función: cancelar solo libera asientos y no puede crear conflictos. La fila de la reserva la protege
la transacción.

### `ListarMisReservasQuery(ClienteId)` y `ObtenerMiReservaQuery(ReservaId, ClienteId)`

Usan `IConsultaReservas` y `IReloj`. Aplican `Reserva.EstadoEn(guardado, venceEn, ahora)` y mapean a
`ReservaDto`. Ordena de la más reciente a la más antigua (`CreadaEn` desc). `ObtenerMiReserva` de otro
Cliente → `ReservaNoEncontrada` (FR-017).

### `ReservaDto`

`Id`, `Estado` (efectivo), `CreadaEn`, `VenceEn`, `CanceladaEn?`, `Asientos` (`Fila`, `Numero`),
`Funcion` (`FuncionId`, `HoraInicio`, `PeliculaId`, `PeliculaTitulo`, `SucursalId`, `SucursalNombre`,
`SalaId`, `SalaNombre`). Al crear se arma con los datos que el handler ya cargó; no hace falta otra
consulta.

## Relaciones

```text
Funcion 1 ──── * Reserva * ──── 1 Cliente (solo el sub del token; sin tabla)
Reserva 1 ──── 1..10 Asiento (owned)
Funcion * ──── 1 Sala ──── * FilaSala   (feature 003: Sala.Contiene valida el Asiento)
```

## Esquema: migración `AddReservas`

```text
Reservas
  Id            uuid          PK
  FuncionId     uuid          NOT NULL  FK → Funciones(Id) ON DELETE RESTRICT
  ClienteId     varchar(255)  NOT NULL
  Estado        varchar(20)   NOT NULL  ('PendienteDePago' | 'Cancelada')
  CreadaEn      timestamptz   NOT NULL
  VenceEn       timestamptz   NOT NULL
  CanceladaEn   timestamptz   NULL
  IX_Reservas_FuncionId_Estado_VenceEn   (FuncionId, Estado, VenceEn)
  IX_Reservas_ClienteId_CreadaEn         (ClienteId, CreadaEn)

ReservaAsientos
  ReservaId     uuid          FK → Reservas(Id) ON DELETE CASCADE
  Fila          character(1)  NOT NULL
  Numero        integer       NOT NULL
  PK (ReservaId, Fila, Numero)

OutboxMensajes
  Id            uuid          PK
  Tipo          varchar(100)  NOT NULL   (ej. 'ReservaCreada')
  Payload       jsonb         NOT NULL
  OcurridoEn    timestamptz   NOT NULL
  ProcesadoEn   timestamptz   NULL
  IX_OutboxMensajes_ProcesadoEn_OcurridoEn  (ProcesadoEn, OcurridoEn)
```

Las fechas se guardan en UTC, igual que `Funciones.FechaHoraInicio`. La migración no siembra datos.
