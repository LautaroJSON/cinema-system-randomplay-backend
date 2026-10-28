# Quickstart: Comprador Invitado con Email Verificado

## Prerrequisitos

- .NET 10 SDK instalado.
- Docker corriendo (Postgres local y `Testcontainers.PostgreSql` para los tests de integración).
- Postgres local levantado con `docker compose up -d`, como en las features anteriores.

## Configurar la clave de firma

La credencial se firma con `Invitados:ClaveFirma` (mínimo 32 bytes). En desarrollo se guarda con user-secrets
para no versionarla:

```bash
dotnet user-secrets set "Invitados:ClaveFirma" "una-clave-de-desarrollo-de-al-menos-32-bytes" --project src/CinemaSystemRandomPlay.Api
```

Sin clave, la API no arranca y lo dice en el mensaje de error.

## Aplicar migraciones

Con una base de desarrollo que tenga reservas de la feature 004, la migración `AddCompradorEmailEnReservas` **las
descarta** (FR-021) y por eso exige una decisión explícita:

```bash
CINEMA_PERMITIR_DESCARTE_RESERVAS=true dotnet ef database update --project src/CinemaSystemRandomPlay.Infrastructure --startup-project src/CinemaSystemRandomPlay.Api
```

Sin la variable y con filas en `Reservas`, la migración falla sin tocar nada. En Development, la API aplica
las migraciones al arrancar y fija la variable por sí misma.

## Correr la API

```bash
dotnet run --project src/CinemaSystemRandomPlay.Api --launch-profile http
```

## Escenario 1: obtener una credencial (User Story 1)

Pedir el código:

```bash
curl -i -X POST http://localhost:5165/api/verificaciones-email -H 'Content-Type: application/json' -d '{"email":"Ana@Example.com"}'
```

Esperado: `202 Accepted` sin body. En la consola de la API aparece el mensaje registrado con el destinatario
`ana@example.com` y el código de 6 dígitos.

Confirmar el código (reemplazar `CODIGO`):

```bash
curl -s -X POST http://localhost:5165/api/verificaciones-email/confirmacion -H 'Content-Type: application/json' -d '{"email":"ana@example.com","codigo":"CODIGO"}'
```

Esperado: `200` con `credencial`, `tipo` y `venceEn`. Guardar la credencial como `CRED_ANA`. Repetir el mismo
comando: `400` con `codigo-invalido` (el código es de un solo uso).

## Escenario 2: reservar como invitado (User Story 2)

Obtener una función y sus asientos del día (endpoints públicos), luego reservar:

```bash
curl -i -X POST http://localhost:5165/api/funciones/FUNCION_ID/reservas -H "Authorization: Bearer $CRED_ANA" -H 'Content-Type: application/json' -d '{"asientos":[{"fila":"C","numero":5},{"fila":"C","numero":6}]}'
```

Esperado: `201 Created`, reserva `PendienteDePago`. Sin el header `Authorization`: `401`.

## Escenario 3: ver y cancelar solo lo propio (User Story 3)

Repetir el Escenario 1 con `beto@example.com` y guardar `CRED_BETO`. Después:

```bash
curl -s http://localhost:5165/api/reservas -H "Authorization: Bearer $CRED_BETO"
```

Esperado: `[]`. Con el id de la reserva de Ana y la credencial de Beto, consultar o cancelar da
`404 reserva-no-encontrada`.

## Correr los tests

```bash
dotnet test
```

Cubre: normalización y validación del email; estados e intentos de la verificación; los casos de uso de
pedir y confirmar el código; el tope por email normalizado; el aislamiento entre emails y la validación de
credenciales (sin credencial, vencida, alterada, de otro emisor); la unicidad de una verificación vigente en
Postgres; la migración que descarta las reservas; y el test de concurrencia de reservas, que sigue vigente
con credenciales de invitado.
