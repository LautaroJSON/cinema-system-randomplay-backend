# Data Model: Comprador Invitado con Email Verificado

Cambios sobre el modelo de la feature 004. Las entidades de catálogo, sucursales, salas y funciones no
cambian.

## Value Object: `EmailComprador` (`Domain/Compradores`)

Identidad del comprador invitado. Inmutable, comparable por valor.

| Regla | Detalle |
|---|---|
| Normalización | Se recortan espacios al inicio y al final y se pasa a minúsculas (cultura invariante). Sin tratamiento de alias (`+tag`, puntos de Gmail). |
| Validación | No vacío; 254 caracteres como máximo; una sola `@`; parte local y dominio no vacíos; el dominio contiene un punto y no empieza ni termina con punto; sin espacios internos. |
| Construcción | `EmailComprador.TryCrear(string?, out EmailComprador)` para entrada externa; `Crear` lanza `DomainException` si es inválido. |
| Igualdad | Por el valor normalizado. |

## Agregado: `VerificacionEmail` (`Domain/Verificaciones`)

Un intento de probar que alguien controla un email.

| Campo | Tipo | Regla |
|---|---|---|
| `Id` | `Guid` | Generado al crear. |
| `Email` | `EmailComprador` | Normalizado. |
| `CodigoHash` | `string` | HMAC-SHA256 del par email + código, en Base64. El código nunca se guarda. |
| `CreadaEn` | `DateTimeOffset` | Del `IReloj`. |
| `VenceEn` | `DateTimeOffset` | `CreadaEn` + vigencia del código (por defecto 10 min). |
| `IntentosFallidos` | `int` | Empieza en 0. |
| `Estado` | `EstadoVerificacion` | Guardado: `Vigente`, `Usada`, `Invalidada`. |

**Estado efectivo** (`EstadoEn(ahora)`): si el guardado es `Vigente` y `VenceEn <= ahora`, es `Vencida`;
en cualquier otro caso es el guardado.

```
              Verificar(correcto)
  Vigente ────────────────────────► Usada
     │ │
     │ └─ Verificar(incorrecto, intentos > máximo) ──► Invalidada
     └──── Invalidar() (se pidió un código nuevo) ───► Invalidada
  (Vigente con VenceEn <= ahora se ve como Vencida)
```

**Operaciones** (devuelven resultados explícitos, no excepciones):

| Operación | Resultado |
|---|---|
| `Crear(email, codigoHash, ahora, vigencia)` | Una `VerificacionEmail` `Vigente`. Lanza `DomainException` con argumentos inválidos. |
| `Invalidar()` | Pasa `Vigente` a `Invalidada`. No hace nada en otros estados. |
| `Verificar(hashIngresado, ahora, maximoIntentos)` | `Correcto` (pasa a `Usada`); `Vencido`; `YaUsado`; `Invalidado`; `Incorrecto` (suma un intento y, si supera el máximo, pasa a `Invalidada`). |

**Invariantes**: un código se usa una sola vez (FR-004); superar el máximo de intentos invalida el código
(FR-005); un email tiene a lo sumo una verificación `Vigente` (índice único parcial, ver Esquema).

## Agregado: `Reserva` (cambia el dueño)

| Antes (004) | Ahora |
|---|---|
| `ClienteId: string` (claim `sub` del proveedor) | `Comprador: EmailComprador` |

Todo lo demás (estados, vencimiento, asientos, precio, `Cancelar`) no cambia. `ReservadorDeAsientos` cuenta
el tope por `Comprador` entre las reservas vigentes de la función.

**Eventos de dominio**: `ReservaCreada` reemplaza el campo del cliente por `Comprador` (email normalizado).
`ReservaCancelada` no lleva identidad y no cambia.

## Resultado de la verificación (`Application`)

| Caso de uso | Resultados |
|---|---|
| `SolicitarVerificacionEmail` | `Aceptada`, `EmailInvalido`, `LimiteExcedido` |
| `ConfirmarVerificacionEmail` | `Ok(credencial, venceEn)`, `EmailInvalido`, `CodigoInvalido`, `CodigoVencido`, `CodigoInvalidado` |

`CodigoInvalido` cubre código incorrecto, ya usado, o sin verificación vigente: las tres respuestas son
indistinguibles para no dar pistas.

## Esquema (migraciones `AddCompradorEmailEnReservas` y `AddVerificacionesEmail`)

### Tabla nueva `VerificacionesEmail`

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `uuid` | PK. |
| `Email` | `varchar(254)` | Normalizado. |
| `CodigoHash` | `varchar(64)` | Base64 de 32 bytes. |
| `CreadaEn`, `VenceEn` | `timestamptz` | UTC. |
| `IntentosFallidos` | `integer` | Por defecto 0. |
| `Estado` | `varchar(20)` | `Vigente`, `Usada` o `Invalidada`. |

Índices:
- `UX_VerificacionesEmail_Email_Vigente`: único sobre `Email` con filtro `"Estado" = 'Vigente'`.
- `IX_VerificacionesEmail_Email_CreadaEn`: para contar las solicitudes de la ventana.

### Tabla `Reservas`

- `ClienteId` (`varchar(255)`) pasa a `CompradorEmail` (`varchar(254)`), con el índice
  `(CompradorEmail, CreadaEn)` renombrado.
- Antes del cambio, se **descartan** las filas de `ReservaAsientos` y `Reservas` y los `OutboxMensajes` de
  tipo `ReservaCreada` y `ReservaCancelada` (FR-021), protegido por la guarda descrita en
  [research.md](./research.md), Decisión 10.

## Configuración (`Invitados`)

| Clave | Por defecto | Uso |
|---|---|---|
| `Invitados:ClaveFirma` | (obligatoria, ≥ 32 bytes) | Firma de la credencial y del hash del código. |
| `Invitados:CodigoMinutosVigencia` | 10 | FR-001. |
| `Invitados:MaximoIntentosFallidos` | 5 | FR-005. |
| `Invitados:CredencialMinutosVigencia` | 60 | FR-008. |
| `Invitados:SolicitudesPorEmail` | 5 | FR-006. |
| `Invitados:SolicitudesPorOrigen` | 20 | FR-006. |
| `Invitados:VentanaSolicitudesMinutos` | 60 | Ventana de ambos límites. |

`Reservas:MaximoAsientosPorCliente` se renombra a `Reservas:MaximoAsientosPorComprador` (mismo valor por
defecto, 10).
