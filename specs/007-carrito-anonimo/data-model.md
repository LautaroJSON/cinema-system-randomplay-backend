# Data Model: Carrito Anónimo con Retención Corta

## Agregado `Carrito` (`Domain/Carritos`)

Reemplaza a `Reserva` (features 004 y 006).

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | Lo genera el dominio al crear. No es secreto. |
| `FuncionId` | `Guid` | Obligatorio. Fijo: un carrito pertenece a una sola función. |
| `Asientos` | `IReadOnlyList<Asiento>` | De 1 a `ReglasCarrito.MaximoAsientosPorCarrito`, sin repetidos, ordenados por fila y número. |
| `Estado` (guardado) | `EstadoCarrito` | `Activo` o `Abandonado`. `Expirado` no se guarda: se deriva. |
| `CreadoEn` | `DateTimeOffset` | Momento del primer asiento elegido. |
| `VenceEn` | `DateTimeOffset` | `min(CreadoEn + MinutosRetencion, inicio de la función)`. **Fijo**: ningún reemplazo lo cambia (FR-005, FR-006). |
| `AbandonadoEn` | `DateTimeOffset?` | Solo si `Estado = Abandonado`. |
| `EmailEntrega` | `EmailComprador?` | Opcional, normalizado. No es credencial (FR-017, FR-018). |
| `PrecioUnitario` | `Dinero` | El de la función al crear el carrito. |
| `Total` | `Dinero` (calculado) | `PrecioUnitario × Asientos.Count`. No se persiste. |

### Estado efectivo

```text
EstadoEn(ahora) =
    Abandonado  si Estado guardado = Abandonado
    Expirado    si VenceEn <= ahora
    Activo      en otro caso
RetieneAsientosEn(ahora) = EstadoEn(ahora) == Activo
```

### Transiciones (FR-016)

```text
Activo ──(VenceEn <= ahora, derivado)──► Expirado    (final)
Activo ──Abandonar(ahora)──────────────► Abandonado  (final)
```

### Operaciones

| Operación | Precondición | Efecto | Resultado si falla |
|---|---|---|---|
| `Crear(...)` (internal, desde `AsignadorDeAsientos`) | validaciones de la tabla de abajo | carrito `Activo` con su vencimiento fijo | — |
| `ReemplazarAsientos(nuevos, ahora)` (internal) | `EstadoEn(ahora) == Activo`; 1..máximo; sin repetidos | quita los que ya no están, agrega los nuevos, no toca los comunes ni `VenceEn` | `DomainException` (el servicio valida antes) |
| `Abandonar(ahora)` | `EstadoEn(ahora) == Activo` | `Estado = Abandonado`, `AbandonadoEn = ahora` | `ResultadoAbandono.NoActivo(estadoActual)` |
| `IndicarEmailEntrega(email, ahora)` | `EstadoEn(ahora) == Activo` | `EmailEntrega = email` | `ResultadoEmailEntrega.NoActivo(estadoActual)` |

## Value Objects y reglas

- **`Asiento`** (existente): fila `A`–`Z` y número ≥ 1.
- **`Dinero`** (existente).
- **`EmailComprador`** (existente, de la 006): recorta espacios, pasa a minúsculas, valida formato y largo
  (≤ 254). Ahora solo se usa para el email de entrega.
- **`ReglasCarrito`** (reemplaza a `ReglasReserva`): `MinutosRetencion` (5), `MaximoAsientosPorCarrito`
  (10) y `PorcentajeMaximoRetenidoPorFuncion` (50, entre 1 y 100). Todos mayores que cero; se valida al
  construir. `TopeRetenidoPara(totalAsientosSala) = max(1, floor(total × porcentaje / 100))`.

## Domain service `AsignadorDeAsientos`

Reemplaza a `ReservadorDeAsientos`. Quien lo invoca debe haber tomado el bloqueo de la función.

```text
Crear(funcion, carritosActivosDeLaFuncion, pedidos, ahora, reglas)
Reemplazar(funcion, carrito, carritosActivosDeLaFuncion, pedidos, ahora, reglas)
```

Orden de validación (el primero que falla decide):

1. `Reemplazar`: el carrito no está activo → `CarritoNoActivo(estadoActual)`.
2. La función ya comenzó → `FuncionNoDisponible`.
3. Asientos fuera de la sala → `AsientosInexistentes(lista)`.
4. Asientos retenidos por **otro** carrito activo → `AsientosNoDisponibles(lista)`. Los del propio carrito
   cuentan como disponibles (FR-010).
5. `retenidos por otros carritos + pedidos.Count > TopeRetenidoPara(sala)` → `FuncionSinCupo`. No informa
   cantidades (FR-023).
6. Ok: `Crear` devuelve el carrito nuevo; `Reemplazar` aplica `ReemplazarAsientos` y devuelve la diferencia
   (liberados y retenidos).

Las validaciones de forma (vacío, más del máximo, repetidos) son de `Application` y responden
`PedidoInvalido`, como en la 004.

## Eventos (outbox, misma transacción)

| Evento | Campos |
|---|---|
| `CarritoCreado` | `CarritoId`, `FuncionId`, `Asientos`, `VenceEn`, `OcurridoEn`, `PrecioUnitario`, `Total` |
| `SeleccionReemplazada` | `CarritoId`, `FuncionId`, `AsientosLiberados`, `AsientosRetenidos`, `VenceEn`, `OcurridoEn` |
| `CarritoAbandonado` | `CarritoId`, `FuncionId`, `AsientosLiberados`, `OcurridoEn` |

Ningún evento lleva el email de entrega ni datos del origen.

## Persistencia (PostgreSQL)

### `Carritos`

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `uuid` PK | `ValueGeneratedNever` |
| `FuncionId` | `uuid` FK → `Funciones` | `ON DELETE RESTRICT` |
| `Estado` | `varchar(20)` | `Activo` / `Abandonado` |
| `CreadoEn`, `VenceEn` | `timestamptz` | UTC |
| `AbandonadoEn` | `timestamptz` null | |
| `EmailEntrega` | `varchar(254)` null | normalizado |
| `PrecioUnitario_Monto` | `numeric(12,2)` | |
| `PrecioUnitario_Moneda` | `varchar(3)` | |

Índice `IX_Carritos_FuncionId_Estado_VenceEn` (ocupación de la función y carritos activos).

### `CarritoAsientos`

PK `(CarritoId, Fila, Numero)`, `Fila character(1)`, `Numero integer`, FK `CarritoId` con cascada.

### Se eliminan

`Reservas`, `ReservaAsientos` y `VerificacionesEmail`, más los mensajes del outbox `ReservaCreada` y
`ReservaCancelada`. La migración tiene guarda de descarte explícito (research.md, Decisión 9).

## Token de acceso (no se persiste)

JWT HS256 del esquema `Invitado`: `sub` = `carrito` = id del carrito, `tipo = "portador-carrito"`, `jti`
aleatorio, `exp` = emisión + `Invitados:TokenMinutosVigencia` (60). Ver research.md, Decisión 1.

## Lo que se elimina de la 006

`VerificacionEmail`, `EstadoVerificacion`, `ResultadoVerificacion`, `ReglasVerificacion`,
`IVerificacionEmailRepository`, `ICodigosDeVerificacion`, `IEmisorDeCredencial` (lo reemplaza
`IEmisorDeTokenDeCarrito`), `CodigosDeVerificacionHmac`, `EmisorDeCredencialJwt` (lo reemplaza
`EmisorDeTokenDeCarritoJwt`), los handlers y el controller de verificación, el tope por comprador y el límite
de pedidos por email.
