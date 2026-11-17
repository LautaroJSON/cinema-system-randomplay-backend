# Data Model: Pago del Carrito con Mercado Pago

Cambios sobre el modelo de la feature 007. Decisiones en [research.md](./research.md).

## Carrito (agregado, cambia)

### Campos nuevos

| Campo | Tipo | Nulo | Descripción |
|---|---|---|---|
| `PreferenciaPagoId` | string(100) | sí | Id de la preferencia en Mercado Pago. Se fija al pasar a `Pagando`. |
| `UrlPago` | string(500) | sí | URL de la página de pago. Se fija al pasar a `Pagando`. |
| `PagoIniciadoEn` | timestamptz | sí | Momento en que pasó a `Pagando`. |
| `PagoId` | string(50) | sí | Id del pago que lo pagó. Se fija al pasar a `Pagado`. |
| `PagoAprobadoEn` | timestamptz | sí | Hora de aprobación informada por Mercado Pago. |
| `PagadoEn` | timestamptz | sí | Momento en que el sistema lo marcó `Pagado`. |
| `EntregaEnviadaEn` | timestamptz | sí | Momento en que el servicio de email aceptó el email de entrega. |
| `Entradas` | colección de `Entrada` | — | Vacía salvo en `Pagado`. |

`EstadoCarrito`: `Activo`, `Pagando`, `Pagado`, `Abandonado`, `Expirado`. En la base se guardan `Activo`, `Pagando`,
`Pagado` y `Abandonado`; `Expirado` se calcula.

### Estado efectivo

```text
EstadoEn(ahora):
  Abandonado | Pagado            → el guardado
  Activo | Pagando, VenceEn<=ahora → Expirado
  en otro caso                   → el guardado

RetieneAsientosEn(ahora) = Pagado
                         || ((Activo || Pagando) && VenceEn > ahora)
```

### Transiciones

```text
Activo  ──IniciarPago──▶ Pagando ──MarcarPagado──▶ Pagado      (final)
  │                         │
  │ Abandonar               └─(VenceEn pasa)──▶ Expirado (calculado)
  ▼                                               │
Abandonado (final)                                └─MarcarPagado, solo si AprobadoEn <= VenceEn
                                                    y los asientos siguen libres ──▶ Pagado
Activo ─(VenceEn pasa)──▶ Expirado (calculado, final)
```

### Métodos nuevos o que cambian

- `VencimientoAlIniciarPago(inicioFuncion)` → `min(VenceEn + 1 min, inicioFuncion)`. Puro; lo usa el caso de uso para
  la preferencia antes de la transacción.
- `IniciarPago(preferenciaId, urlPago, asientosEsperados, ahora, inicioFuncion)` → `ResultadoInicioPago`:
  - `YaEnPago(urlPago)` si el estado guardado es `Pagando` (no cambia nada, FR-005);
  - `NoActivo(estado)` si el estado efectivo no es `Activo`;
  - `SinEmailEntrega` si `EmailEntrega` es nulo (FR-002);
  - `SeleccionModificada` si `Asientos` difiere de `asientosEsperados`;
  - `Iniciado`: `Estado = Pagando`, `VenceEn = VencimientoAlIniciarPago(inicio)`, guarda preferencia, URL y momento.
- `MarcarPagado(pagoId, aprobadoEn, ahora, datosDeFuncion, generadorDeCodigos)`: exige estado guardado `Pagando` (lo
  garantiza `ConfirmadorDePagos`). `Estado = Pagado`, guarda pago y momentos, y crea una `Entrada` por asiento.
- `ReemplazarAsientos`, `Abandonar`, `IndicarEmailEntrega`: con estado efectivo `Pagando` devuelven `EnPago` (FR-009).
- `MarcarEntregaEnviada(ahora)`: idempotente.

### Invariantes

- `VenceEn` cambia a lo sumo una vez después de la creación: al pasar a `Pagando`, y nunca más allá del inicio de la
  función (SC-006).
- `Pagado` implica `PagoId`, `PagoAprobadoEn`, `PagadoEn` no nulos y `Entradas.Count == Asientos.Count`.
- `Pagando` o `Pagado` implica `EmailEntrega`, `PreferenciaPagoId` y `UrlPago` no nulos.

## Entrada (entidad del agregado Carrito, nueva)

Tabla `Entradas`, mapeada como entidad propia con FK a `Carritos` y borrado en cascada (no como `OwnsMany`), para
poder indexar `Fila` y `Numero` como columnas simples.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | uuid | PK. |
| `CarritoId` | uuid | FK a `Carritos`. |
| `Codigo` | char(11) | `XXXXX-XXXXX`, Crockford Base32. **Único**. |
| `FuncionId` | uuid | Copia de la función del carrito. |
| `Fila`, `Numero` | char(1), int | Asiento. |
| `PeliculaTitulo` | string(200) | Copia al pagar. |
| `SucursalNombre` | string(200) | Copia al pagar. |
| `SalaNombre` | string(100) | Copia al pagar. |
| `HoraInicio` | timestamptz | Copia al pagar. |
| `PrecioUnitario_Monto`, `PrecioUnitario_Moneda` | numeric(12,2), string(3) | Precio congelado del carrito. |
| `EmitidaEn` | timestamptz | Momento del pago. |

Índices: único `(Codigo)`; único `(FuncionId, Fila, Numero)` (red de seguridad contra doble venta).

## ConfirmadorDePagos (domain service, nuevo)

`Decidir(carrito, carritosQueRetienenDeLaFuncion, pago, ahora)` → `DecisionDePago`:
`Acreditar` | `Devolver(MotivoDevolucion)`. Orden de reglas en [research.md, Decisión 6](./research.md).

`PagoAprobado` (value object de dominio): `PagoId` (string, no vacío), `Monto` (`Dinero`), `AprobadoEn`.

`MotivoDevolucion`: `PagoTardio`, `CarritoYaPagado`, `AsientosNoDisponibles`, `MontoDistinto`.

## PagoProcesado (nuevo)

Tabla `PagosProcesados`. Garantiza que cada pago tenga efecto una sola vez (FR-017).

| Campo | Tipo | Descripción |
|---|---|---|
| `PagoId` | string(50) | **PK**. Id del pago en Mercado Pago. |
| `CarritoId` | uuid | Carrito referenciado. |
| `Monto`, `Moneda` | numeric(12,2), string(3) | Según Mercado Pago. |
| `AprobadoEn` | timestamptz | Según Mercado Pago. |
| `Resultado` | string(20) | `Acreditado` o `Devuelto`. |
| `ProcesadoEn` | timestamptz | |

Solo se insertan pagos aprobados. Rechazados y pendientes no dejan fila: se vuelven a evaluar si llega otro aviso.

## Devolucion (nuevo)

Tabla `Devoluciones` (FR-020, FR-021).

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | uuid | PK. Es también la clave de idempotencia ante Mercado Pago. |
| `PagoId` | string(50) | **Único**. Un pago se devuelve a lo sumo una vez. |
| `CarritoId` | uuid | |
| `Monto`, `Moneda` | numeric(12,2), string(3) | Monto completo del pago. |
| `Motivo` | string(30) | `MotivoDevolucion`. |
| `Estado` | string(20) | `Pendiente` → `Completada` \| `Fallida`. |
| `SolicitadaEn` | timestamptz | |
| `ResueltaEn` | timestamptz, nulo | |
| `UltimoError` | string(500), nulo | |

## OutboxMensaje (cambia)

Columnas nuevas para el procesador ([research.md, Decisión 10](./research.md)):

| Campo | Tipo | Descripción |
|---|---|---|
| `Intentos` | int, default 0 | |
| `ProximoIntentoEn` | timestamptz, nulo | Lease y espera entre reintentos. |
| `UltimoError` | string(1000), nulo | Mensaje de la excepción, sin datos sensibles. |
| `FallidoEn` | timestamptz, nulo | Reintentos agotados. |

Índice nuevo: `(ProcesadoEn, FallidoEn, ProximoIntentoEn)` para la toma de mensajes.

## Eventos nuevos (Outbox)

| Evento | Payload | Manejador |
|---|---|---|
| `PagoIniciado` | carritoId, funcionId, venceEn, ocurridoEn | ninguno (lo usará SignalR) |
| `CarritoPagado` | carritoId, funcionId, pagoId, asientos, ocurridoEn | `EnviarEmailDeEntrega` |
| `DevolucionSolicitada` | devolucionId, pagoId, carritoId, motivo, ocurridoEn | `EjecutarDevolucion` |

## Migración

Una migración, `AgregarPagoDeCarritos`: columnas nuevas en `Carritos` y `OutboxMensajes`, tablas `Entradas`,
`PagosProcesados` y `Devoluciones`. No descarta datos: los carritos existentes quedan con las columnas nuevas en nulo y
siguen siendo válidos. `Estado` ya es `varchar(20)` y admite los valores nuevos.
