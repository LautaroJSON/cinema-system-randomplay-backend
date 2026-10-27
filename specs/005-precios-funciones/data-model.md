# Data Model: Precios de las Funciones

Cambios sobre el modelo de las features 002 a 004. Decisiones en [research.md](./research.md).

## Value Object nuevo: `Dinero` (`Domain/Compartido`)

| Campo | Tipo | Reglas |
|---|---|---|
| `Monto` | `decimal` | `>= 0`; como máximo 2 decimales significativos; se normaliza a escala 2. |
| `Moneda` | `Moneda` (enum) | Único valor en esta feature: `ARS`. |

- Inmutable (`sealed record`), igualdad por valor.
- `Dinero.Ars(decimal monto)`: fábrica.
- `Multiplicar(int cantidad)`: `cantidad >= 0`; devuelve un `Dinero` nuevo con `Monto * cantidad` en la
  misma moneda. Es exacto: no redondea.
- Errores: `DomainException` con monto negativo, más de 2 decimales o cantidad negativa.

Ejemplos:

| Entrada | Resultado |
|---|---|
| `Ars(5500)` | `5500.00 ARS` |
| `Ars(7250.5m)` | `7250.50 ARS` |
| `Ars(0)` | `0.00 ARS` (válido para `Dinero`) |
| `Ars(-1)` | `DomainException` |
| `Ars(10.005m)` | `DomainException` |
| `Ars(7250.50m).Multiplicar(4)` | `29002.00 ARS` |

## Enum nuevo: `Moneda` (`Domain/Compartido`)

`ARS`. Se persiste como texto.

## `Funcion` (modificada)

| Campo nuevo | Tipo | Reglas |
|---|---|---|
| `PrecioEntrada` | `Dinero` | Obligatorio; `Monto > 0`; `Moneda == ARS` (FR-001, FR-003). |

- El constructor pasa a ser `Funcion(Guid id, Guid peliculaId, Guid salaId, DateTimeOffset fechaHoraInicio, Dinero precioEntrada)`.
- Sin método para cambiar el precio en esta feature.

## `Reserva` (modificada)

| Campo nuevo | Tipo | Reglas |
|---|---|---|
| `PrecioUnitario` | `Dinero` | Obligatorio. Copia de `Funcion.PrecioEntrada` al crearse. No cambia después (FR-008, FR-009). |
| `Total` | `Dinero` (calculado, no persistido) | `PrecioUnitario.Multiplicar(Asientos.Count)`. |

- `Reserva.Crear(...)` recibe `Dinero precioUnitario` y rechaza `null`.
- `ReservadorDeAsientos.Reservar(...)` no cambia de firma: toma el precio de `funcion.PrecioEntrada`.
- El precio y el total se conservan en todos los estados (`PendienteDePago`, `Expirada`, `Cancelada`).

## Evento `ReservaCreada` (modificado)

Agrega `Dinero PrecioUnitario` y `Dinero Total` al final del record.

## DTOs de Application

| DTO | Cambio |
|---|---|
| `DineroDto(decimal Monto, string Moneda)` | Nuevo, en `Application/Compartido/Dtos`, con `DesdeDominio(Dinero)`. |
| `HorarioFuncionDto` | `+ DineroDto PrecioEntrada` |
| `MapaAsientosDto` | `+ DineroDto PrecioEntrada` |
| `ReservaDto` | `+ DineroDto PrecioUnitario`, `+ DineroDto Total` |
| `ReservaLecturaDto` (puerto `IConsultaReservas`) | `+ Dinero PrecioUnitario` (el total lo calcula `ReservaDto.DesdeLectura` con la cantidad de asientos) |

## Esquema (PostgreSQL)

Migración `AddPrecios`:

| Tabla | Columna | Tipo | Nulabilidad |
|---|---|---|---|
| `Funciones` | `PrecioEntrada_Monto` | `numeric(12,2)` | `NOT NULL` |
| `Funciones` | `PrecioEntrada_Moneda` | `character varying(3)` | `NOT NULL` |
| `Reservas` | `PrecioUnitario_Monto` | `numeric(12,2)` | `NOT NULL` |
| `Reservas` | `PrecioUnitario_Moneda` | `character varying(3)` | `NOT NULL` |

Datos existentes (research.md, Decisión 6):
- Las funciones existentes quedan con `5500.00` / `ARS` (default temporal de la migración, que después
  se quita).
- Las reservas existentes copian el precio actual de su función.

Sin índices nuevos: ninguna consulta filtra ni ordena por precio.
