# Auditoría End-to-End: Bugs, Seguridad, Validaciones y Code Smells

> **Proyecto:** Sistema de Reserva de Restaurantes (Grupo N° 1)  
> **Fecha:** Noviembre 2026  
> **Estado:** Pendiente de corrección paso a paso  
> **Regla de Ejecución:** No se ha modificado ningún archivo preexistente de la solución. Cada ítem está inventariado para su corrección unitaria según indicación.

---

## Resumen Ejecutivo de Hallazgos

| Categoría | Crítica | Alta | Media | Baja | Total |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **1. Seguridad y Vulnerabilidades** | 2 | 3 | 2 | 0 | **7** |
| **2. Bugs Lógicos y de Runtime** | 2 | 5 | 4 | 2 | **13** |
| **3. Validaciones Faltantes / Defectuosas** | 0 | 4 | 3 | 0 | **7** |
| **4. Code Smells y Deuda Técnica** | 0 | 1 | 6 | 5 | **12** |
| **Total de Hallazgos** | **4** | **13** | **15** | **7** | **39** |

---

## 1. Bugs de Seguridad y Vulnerabilidades

### [SEC-01] IDOR / Broken Object-Level Authorization (BOLA) en Cancelación de Reserva
- **Severidad:** Crítica
- **Archivos:** 
  - `src/BusinessLogic/Reservas/Implementations/ReservaService.cs` (Líneas 137–150)
  - `src/Api/Controllers/ReservasController.cs` (Líneas 67–80)
- **Descripción:** El endpoint `PATCH /api/v1/Reservas/{id}/cancelar` extrae el `clienteId` del token JWT y lo pasa a `_reservaService.CancelarReservaClienteAsync(id, clienteId, dto)`. Sin embargo, dentro del servicio **nunca se valida si `reserva.ClienteId == clienteId`**.
- **Impacto:** Cualquier cliente autenticado puede cancelar las reservas de cualquier otro cliente simplemente enviando el GUID de la reserva.
- **Solución Recomendada:** Verificar `if (reserva.ClienteId != clienteId)` y lanzar una excepción de acceso denegado (`AccesoDenegadoException`) que resulte en HTTP 403 Forbidden (cumpliendo con la RN-01 del caso de uso CU-07).

---

### [SEC-02] Comparación y Aceptación de Contraseñas en Texto Plano (Timing Attack & Insecure Auth)
- **Severidad:** Crítica
- **Archivo:** `src/BusinessLogic/Auth/Implementations/AuthService.cs` (Líneas 71–79)
- **Descripción:** En `VerifyPassword`:
  ```csharp
  if (storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$"))
  {
      return BCrypt.Net.BCrypt.Verify(password, storedHash);
  }
  return password == storedHash; // <-- Fallback inseguro
  ```
- **Impacto:** Permite autenticar usuarios cuyas contraseñas se almacenen sin hash en la base de datos. Además, la comparación `password == storedHash` es vulnerable a ataques de canal lateral por temporización (timing attacks).
- **Solución Recomendada:** Eliminar el fallback a texto plano. Requerir siempre hashes BCrypt válidos y rechazar cualquier credencial que no esté hasheada adecuadamente.

---

### [SEC-03] Clave Secreta JWT Expuesta en Código y `appsettings.json`
- **Severidad:** Alta
- **Archivos:**
  - `src/Api/Program.cs` (Línea 33)
  - `src/BusinessLogic/Auth/Implementations/JwtTokenGenerator.cs` (Línea 22)
  - `src/Api/appsettings.json` (Línea 13)
- **Descripción:** Se define `"SuperSecretKey_GrupoN1_ReservaRestaurantes_2026!"` como fallback en código C# y en el archivo de configuración versionado.
- **Impacto:** Cualquier atacante con acceso al repositorio o al ejecutable puede firmar sus propios tokens JWT con cualquier identidad y con el rol `Admin`, obteniendo control total del sistema.
- **Solución Recomendada:** Exigir que la clave provenga obligatoriamente de variables de entorno seguras (`Environment.GetEnvironmentVariable`) o Azure KeyVault/User Secrets, lanzando una excepción al arranque si no está configurada o si es menor a 256 bits, y quitarla de `appsettings.json`.

---

### [SEC-04] Credenciales Administrativas Semilla Hardcodeadas en Código
- **Severidad:** Alta
- **Archivo:** `src/DataAccess/Data/DbInitializer.cs` (Líneas 172 y 184)
- **Descripción:** En la inicialización de la base de datos se generan usuarios fijos:
  - Gerente: `gerente@restaurant.com` / `Gerente123!`
  - Admin: `admin@restaurant.com` / `Admin123!`
- **Impacto:** Cuentas con privilegios máximos desplegadas con credenciales públicas y predecibles en todos los entornos.
- **Solución Recomendada:** Cargar las contraseñas iniciales desde configuración/secretos de entorno o forzar el cambio de contraseña en el primer inicio de sesión.

---

### [SEC-05] Endpoint de Clientes sin Autorización (Exposición de Datos Personales)
- **Severidad:** Alta
- **Archivo:** `src/Api/Controllers/ClientesController.cs` (Líneas 36–44)
- **Descripción:** El endpoint `GET /api/v1/Clientes/{id}` carece del atributo `[Authorize]`.
- **Impacto:** Cualquier usuario anónimo en internet puede obtener nombre, apellido, email y teléfono de cualquier cliente registrado mediante una simple petición HTTP GET.
- **Solución Recomendada:** Agregar `[Authorize]` y restringir la consulta a usuarios con rol `Admin`, `Gerente` o al propio cliente dueño del perfil.

---

### [SEC-06] Inconsistencias de Autorización RBAC en Mapa y Estados de Mesas (Bloqueo a Admin)
- **Severidad:** Media
- **Archivo:** `src/Api/Controllers/MesasController.cs` (Líneas 77, 99)
- **Descripción:** Endpoints `PATCH /api/mesas/{id}/estado` y `GET /api/mesas/filtrar-mapa-de-mesas` solo tienen `[Authorize(Roles = "Gerente")]`. Según la especificación CU-10 (RN-01), tanto `Gerente` como `Admin` deben tener acceso.
- **Impacto:** El Administrador del restaurante recibe un HTTP 403 Forbidden al intentar auditar o visualizar el mapa de mesas o sus estados.
- **Solución Recomendada:** Configurar `[Authorize(Roles = "Gerente,Admin")]` en dichos endpoints.

---

### [SEC-07] Falta de Revocación de Tokens JWT al Cerrar Sesión o Cambiar Rol (RN-03 de CU-19)
- **Severidad:** Media
- **Archivos:**
  - `src/BusinessLogic/Auth/Implementations/AuthService.cs` (Líneas 57–62)
  - `src/BusinessLogic/Reservas/Implementations/UsuarioService.cs` (Líneas 60–62)
- **Descripción:** `LogoutAsync` retorna `Task.CompletedTask` vacío. Al cambiar o degradar el rol de un usuario (ej. de Admin a Cliente), los tokens JWT ya emitidos continúan siendo válidos hasta su vencimiento (60 minutos).
- **Impacto:** Un usuario despedido o degradado de rol puede seguir realizando operaciones de Admin o Gerente con el token anterior.
- **Solución Recomendada:** Implementar un mecanismo de lista negra en memoria/caché (ej. IMemoryCache / Redis) o un `SecurityStamp` en la entidad `Cliente` verificado mediante middleware para invalidar tokens previos.

---

## 2. Bugs Lógicos, de Concurrencia y Runtime

### [BUG-01] Desfase Crítico de Zona Horaria (UTC vs Local) en `ReservaNoShowWorker`
- **Severidad:** Crítica
- **Archivos:**
  - `src/BusinessLogic/Workers/ReservaNoShowWorker.cs` (Líneas 35–41)
  - `src/BusinessLogic/Reservas/Implementations/ReservaService.cs` (Líneas 106–116)
- **Descripción:** El worker calcula `limiteTolerancia = DateTime.UtcNow.AddMinutes(-15)` y busca reservas donde `r.FechaHora <= limiteTolerancia`. Pero `FechaHora` se valida y almacena según la franja local del restaurante (ej. Argentina UTC-3: almuerzo 12:00–15:30, cena 20:00–23:30).
- **Impacto:** Si una reserva es a las 20:00 hora local (que son las 23:00 UTC), el worker comparará `20:00 <= 22:45 UTC` y marcará la reserva como `NoShow` y liberará la mesa **3 horas antes de que el cliente deba asistir**.
- **Solución Recomendada:** Estandarizar la persistencia a UTC explícito o aplicar la conversión de zona horaria del restaurante (`TimeZoneInfo.ConvertTime`) antes de evaluar la tolerancia.

---

### [BUG-02] Liberación Errónea del Estado Físico de Mesa al Cancelar/Rechazar Reservas Futuras
- **Severidad:** Crítica
- **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs` (Líneas 156, 294, 324)
- **Descripción:** En `CancelarReservaClienteAsync`, `CancelarReservaGerenteAsync` y `RechazarReservaAsync`, al cancelar una reserva se ejecuta incondicionalmente:
  ```csharp
  if (reserva.Mesa != null)
  {
      reserva.Mesa.Estado = MesaEstado.Libre;
      await _mesaRepository.UpdateAsync(reserva.Mesa);
  }
  ```
- **Impacto:** Si un cliente cancela hoy una reserva programada para dentro de una semana, la mesa física pasa a estar `Libre` en el sistema en ese preciso instante, sobreescribiendo el estado si la mesa estaba `Ocupada` por otros comensales en el restaurante.
- **Solución Recomendada:** Solo cambiar el estado físico de la mesa a `Libre` si la reserva cancelada está actualmente `EnCurso` o si su franja horaria coincide con el momento actual.

---

### [BUG-03] Ventana de Solapamiento Omite Reservas Nocturnas que Cruzan la Medianoche
- **Severidad:** Alta
- **Archivo:** `src/DataAccess/Repositories/Implementations/ReservaRepository.cs` (Líneas 70–81, 105–116)
- **Descripción:** `GetOverlappingReservationsByMesaAsync` filtra:
  ```csharp
  var windowStart = start.Date;
  var windowEnd = end.Date.AddDays(1);
  ...
  r.FechaHora >= windowStart && r.FechaHora < windowEnd
  ```
- **Impacto:** Si una reserva inició a las 23:30 de ayer y dura 2 horas (hasta 01:30 de hoy), para una reserva solicitada hoy a las 00:30 `windowStart` será hoy a las 00:00 y la reserva de ayer será descartada de la consulta por fecha de inicio, permitiendo una doble reserva sobre la misma mesa.
- **Solución Recomendada:** Ajustar `windowStart` para incluir el inicio de reservas previas cuya duración cubra el cambio de día (`windowStart = start.Date.AddDays(-1)` o evaluar inicio y fin en la base de datos).

---

### [BUG-04] Reservas en Estado `NoShow` Bloquean Mesas y Clientes Indefinidamente
- **Severidad:** Alta
- **Archivo:** `src/DataAccess/Repositories/Implementations/ReservaRepository.cs` (Líneas 78–79, 113–114)
- **Descripción:** Al consultar reservas solapadas por mesa y por cliente, el repositorio filtra:
  ```csharp
  r.Estado != ReservaEstado.Cancelada && r.Estado != ReservaEstado.Finalizada
  ```
- **Impacto:** No se excluye `ReservaEstado.NoShow`. Una reserva marcada como no presentada sigue considerándose ocupando la mesa y el cliente queda inhabilitado para reservar en esa franja horaria.
- **Solución Recomendada:** Agregar `&& r.Estado != ReservaEstado.NoShow` en ambos métodos.

---

### [BUG-05] Discrepancia de Tipos y Semántica en `AsignarMesaAsync` (`int mesaId` vs `Guid Mesa.Id`)
- **Severidad:** Alta
- **Archivos:**
  - `src/BusinessLogic/Reservas/Implementations/ReservaService.cs` (Líneas 335, 342)
  - `src/Shared/DTOs/Mesas/ReservaAsignarMesaDTO.cs` (Línea 7)
- **Descripción:** El parámetro se llama `mesaId` de tipo `int`, pero la entidad `Mesa` tiene como clave primaria un `Guid Id`. En la línea 342 el servicio busca `_mesaRepository.GetByNumeroAsync(mesaId)`.
- **Impacto:** Ambigüedad grave en la API: el consumidor no sabe si debe enviar el ID del recurso (Guid) o el número de mesa (int). Si el cliente manda el GUID de la mesa, el endpoint falla por deserialización.
- **Solución Recomendada:** Clarificar la propiedad en el DTO como `NumeroMesa` o cambiar el diseño para recibir el `Guid MesaId` estándar.

---

### [BUG-06] Incompatibilidad de Traducción SQL con `.Date` en EF Core SQLite
- **Severidad:** Alta
- **Archivo:** `src/DataAccess/Repositories/Implementations/ReservaRepository.cs` (Línea 183)
- **Descripción:** En `GetReservasOperativasAsync`:
  ```csharp
  .Where(r => r.FechaHora.Date == fechaConsulta)
  ```
- **Impacto:** En SQLite, la función `.Date` dentro del árbol de expresión LINQ no tiene traducción SQL directa en todas las configuraciones de EF Core, provocando advertencias de evaluación del lado del cliente o excepciones en tiempo de ejecución.
- **Solución Recomendada:** Reemplazar por un rango de fechas: `r.FechaHora >= fechaConsulta && r.FechaHora < fechaConsulta.AddDays(1)`.

---

### [BUG-07] Clientes Semilla Imposibilitados de Iniciar Sesión
- **Severidad:** Media
- **Archivo:** `src/DataAccess/Data/DbInitializer.cs` (Líneas 22–60)
- **Descripción:** Los clientes semilla `cliente1`, `cliente2`, `cliente3` y `cliente4` son dados de alta sin asignarles `PasswordHash` (queda `""`).
- **Impacto:** En `AuthService.VerifyPassword`, si `storedHash` está vacío retorna `false`. Ninguno de los 4 clientes de prueba puede loguearse.
- **Solución Recomendada:** Asignar un `BCrypt.Net.BCrypt.HashPassword("Cliente123!")` por defecto a cada uno de ellos.

---

### [BUG-08] Archivo `AccesoDenegadoException.cs` Vacío (0 Bytes)
- **Severidad:** Media
- **Archivo:** `src/Shared/Exceptions/AccesoDenegadoException.cs`
- **Descripción:** El archivo existe pero está completamente en blanco (0 bytes).
- **Impacto:** No se puede instanciar ni utilizar en las capas de negocio; si alguien intenta referenciarla generará error de compilación.
- **Solución Recomendada:** Implementar la clase derivada de `Exception` con los constructores estándar.

---

### [BUG-09] Falta de Mapeo de 403 Forbidden en `GlobalExceptionHandler`
- **Severidad:** Media
- **Archivo:** `src/Api/Middleware\GlobalExceptionHandler.cs` (Líneas 29–59)
- **Descripción:** El switch centralizado no maneja excepciones de acceso denegado ni autorizaciones fallidas de negocio.
- **Impacto:** Cualquier fallo de autorización de negocio termina cayendo en el caso default `_ => 500 Internal Server Error` en vez de un código HTTP 403 Forbidden estructurado con ProblemDetails.
- **Solución Recomendada:** Mapear `AccesoDenegadoException => (StatusCodes.Status403Forbidden, "Acceso Denegado")`.

---

### [BUG-10] `CreatedAtAction` en `MesasController.CrearMesa` Apunta a POST
- **Severidad:** Media
- **Archivo:** `src/Api/Controllers/MesasController.cs` (Línea 33)
- **Descripción:** Retorna `CreatedAtAction(nameof(CrearMesa), new { id = mesaCreada.Id }, mesaCreada);`.
- **Impacto:** El encabezado HTTP `Location` apunta a una ruta POST en lugar de un endpoint GET de consulta individual, violando el estándar REST RFC 7231. Además, `MesasController` ni siquiera tiene un endpoint `GetById`.
- **Solución Recomendada:** Crear el endpoint `GET /api/mesas/{id}` y enlazar `CreatedAtAction(nameof(GetById), ...)` correctamente.

---

### [BUG-11] Uso Inseguro de `EnsureCreatedAsync` que Invalida las Migraciones de EF Core
- **Severidad:** Media
- **Archivos:**
  - `src/DataAccess/Data/DbInitializer.cs` (Línea 13)
  - `src/Api/Program.cs` (Línea 22)
- **Descripción:** `DbInitializer` usa `context.Database.EnsureCreatedAsync()`. Esto no registra la tabla `__EFMigrationsHistory`. Para silenciar el conflicto con la migración existente, en `Program.cs` se ignoró la advertencia `PendingModelChangesWarning`.
- **Impacto:** Cualquier comando futuro `dotnet ef database update` fallará con error de SQLite "table already exists".
- **Solución Recomendada:** Reemplazar `EnsureCreatedAsync()` por `context.Database.MigrateAsync()`.

---

### [BUG-12] Migración `InitialCreate` Incompleta (Faltan Columnas de Seguridad)
- **Severidad:** Media
- **Archivo:** `src/Migrations/Migrations/20260910190502_InitialCreate.cs` (Líneas 14–28)
- **Descripción:** La migración inicial no tiene las columnas `PasswordHash`, `Rol` ni `Activo` para la tabla `Clientes`.
- **Impacto:** Al levantar la solución en un entorno limpio mediante migraciones, la base de datos queda sin esas columnas y el sistema crashea en el primer login o consulta.
- **Solución Recomendada:** Generar una nueva migración `AddAuthFieldsToCliente` que agregue dichas columnas al esquema.

---

### [BUG-13] Versiones Flotantes de NuGet (`10.0.*`) en Proyectos de Librería
- **Severidad:** Baja
- **Archivos:**
  - `src/DataAccess/DataAccess.csproj` (Líneas 9, 13)
  - `src/Migrations/Migrations.csproj` (Líneas 8, 12)
- **Descripción:** Ambos proyectos usan `Version="10.0.*"`.
- **Impacto:** Rompe el build reproducible, obliga a NuGet a intentar contactar api.nuget.org en cada compilación y falla si la máquina compila sin acceso a internet o en entornos cerrados.
- **Solución Recomendada:** Fijar las versiones explícitas a `10.0.12` o `10.0.13` igual que en el resto de los proyectos.

---

## 3. Validaciones Faltantes o Defectuosas

### [VAL-01] `MesaCreateDTO` y `MesaUpdateDTO` sin Atributos de Validación
- **Severidad:** Alta
- **Archivos:** 
  - `src/Shared/DTOs/Mesas/MesaCreateDTO.cs`
  - `src/Shared/DTOs/Mesas/MesaUpdateDTO.cs`
- **Descripción:** No poseen ninguna validación DataAnnotation.
- **Impacto:** Permite crear o modificar mesas con número negativo/cero, y con capacidad 0 o negativa (ej. capacidad = -10 comensales). Además, `MesaCreateDTO` no incluye `Ubicacion`, dejando la columna obligatoria de la BD como string vacío `""`.
- **Solución Recomendada:** Agregar `[Range(1, 1000)]` en `Numero`, `[Range(1, 50)]` en `Capacidad` y agregar la propiedad `Ubicacion` con `[Required]`.

---

### [VAL-02] Fechas en el Pasado Permitidas en `ReservaSolicitudDTO`
- **Severidad:** Alta
- **Archivo:** `src/Shared/DTOs/Reservas/ReservaSolicitudDTO.cs` (Líneas 7–8)
- **Descripción:** Solo valida `[Required]` sobre `FechaHora`.
- **Impacto:** Un usuario puede reservar una mesa con fecha de ayer o del año 2020 sin que el sistema lo rechace en la validación de entrada.
- **Solución Recomendada:** Crear una validación personalizada o validar en `ReservaService` que `dto.FechaHora > DateTime.UtcNow` con un margen mínimo de anticipación.

---

### [VAL-03] Ausencia de `[MaxLength]` en Campos `Observaciones` y `Motivo`
- **Severidad:** Alta
- **Archivos:**
  - `src/Shared/DTOs/Reservas/ReservaSolicitudDTO.cs` (Línea 14)
  - `src/Shared/DTOs/Reservas/ReservaUpdateDTO.cs` (Línea 9)
  - `src/Shared/DTOs/Reservas/ReservaCancelarDTO.cs` (Línea 5)
- **Descripción:** La entidad `Reserva` limita `Observaciones` a 500 caracteres (`HasMaxLength(500)`). Los DTOs no limitan la longitud del texto. Además, al cancelar con motivo se concatena `reserva.Observaciones += " | [Motivo: ...]"`, lo que puede superar fácilmente los 500 caracteres.
- **Impacto:** Si un payload supera los 500 caracteres o la concatenación excede el límite, la inserción en base de datos arroja una excepción no controlada (500).
- **Solución Recomendada:** Agregar `[StringLength(500)]` en los DTOs y recortar/validar la longitud resultante al concatenar el motivo de cancelación.

---

### [VAL-04] Falta de Validación de Rango en DTOs con Enums
- **Severidad:** Media
- **Archivos:**
  - `src/Shared/DTOs/Mesas/MesaCambiarEstadoDTO.cs` (Línea 7)
  - `src/Shared/DTOs/Reservas/ReservaCambiarEstadoDTO.cs` (Línea 7)
- **Descripción:** Propiedades `MesaEstado NuevoEstado` y `ReservaEstado NuevoEstado` sin `[EnumDataType]`.
- **Impacto:** Si se envía `{ "nuevoEstado": 88 }`, el serializador JSON lo asigna sin lanzar error de validación, persistiendo un valor inexistente en base de datos.
- **Solución Recomendada:** Decorar con `[EnumDataType(typeof(MesaEstado))]` o validar explícitamente `Enum.IsDefined(typeof(MesaEstado), dto.NuevoEstado)`.

---

### [VAL-05] Conflicto de Sensibilidad a Mayúsculas en `UsuarioCambiarRolDTO` vs Servicio
- **Severidad:** Media
- **Archivos:**
  - `src/Shared/DTOs/Usuarios/UsuarioCambiarRolDTO.cs` (Línea 8)
  - `src/BusinessLogic/Reservas/Implementations/UsuarioService.cs` (Líneas 11–16)
- **Descripción:** La regex en el DTO es `^(Admin|Gerente|Cliente)$` (case-sensitive). Si se envía `"admin"` o `"gerente"` falla con 400 Bad Request. Sin embargo, en el servicio y en los tests unitarios se utiliza `StringComparer.OrdinalIgnoreCase`.
- **Impacto:** Lo que el servicio y los tests unitarios consideran válido es rechazado en la capa de presentación HTTP.
- **Solución Recomendada:** Usar regex `^(?i)(Admin|Gerente|Cliente)$` o normalizar a TitleCase en el DTO.

---

### [VAL-06] Desactivación de Mesa sin Validar Reservas Activas
- **Severidad:** Media
- **Archivo:** `src/BusinessLogic/Reservas/Implementations/MesaService.cs` (Líneas 62–64)
- **Descripción:** En `ModificarMesaAsync`, se permite actualizar `mesa.Activa = mesaDto.Activa` a `false` sin verificar si la mesa tiene reservas confirmadas o en curso asignadas.
- **Impacto:** Se puede desactivar una mesa que está ocupada por clientes en el salón o comprometida para reservas de hoy.
- **Solución Recomendada:** Impedir la desactivación (`Activa = false`) si `_mesaRepository.HasReservaEnCursoAsync` o existen reservas confirmadas pendientes para la fecha.

---

### [VAL-07] Omisión de Verificación de Mesa al Confirmar Reserva (RN-03 de CU-12)
- **Severidad:** Media
- **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs` (Líneas 194–215)
- **Descripción:** `ConfirmarReservaAsync` solo verifica que el estado sea `Pendiente`. No constata que la mesa asignada siga activa, no haya sido eliminada o no tenga un nuevo solapamiento surgido entre la solicitud y la confirmación.
- **Impacto:** Puede confirmarse una reserva con una mesa que ya no está disponible o que fue reasignada.
- **Solución Recomendada:** Revalidar que `reserva.MesaId != Guid.Empty`, que la mesa esté activa y que no existan reservas solapadas antes de pasar a `Confirmada`.

---

## 4. Code Smells, Inconsistencias de Diseño y Deuda Técnica

### [SMELL-01] Respuestas 404 en Consultas Filtradas sin Coincidencias (Violación REST y de CU-10 / CU-11)
- **Severidad:** Alta
- **Archivos:**
  - `src/BusinessLogic/Reservas/Implementations/MesaService.cs` (Líneas 133–136)
  - `src/BusinessLogic/Reservas/Implementations/ReservaService.cs` (Líneas 404–407)
- **Descripción:** `FiltrarMapaMesasAsync` y `FiltrarReservasAsync` lanzan excepciones `SinMesasDisponiblesException` y `SinReservasOperativasException` cuando la consulta no arroja filas, resultando en HTTP 404 Not Found.
- **Impacto:** Viola los principios REST (un filtro que no arroja resultados debe responder `200 OK` con un array vacío `[]`) y contradice directamente los casos de uso documentados (CU-10 Flujo 3a y CU-11 Flujo 3a estipulan explícitamente `200 OK con colección vacía`).
- **Solución Recomendada:** Retornar `Enumerable.Empty<...>()` en lugar de lanzar una excepción cuando no hay resultados de búsqueda.

---

### [SMELL-02] Discrepancia Absoluta en Esquemas de Rutas de la API (Routing Inconsistency)
- **Severidad:** Media
- **Archivos:**
  - `AuthController.cs`: `[Route("api/v1/[controller]")]` (`/api/v1/Auth`)
  - `ClientesController.cs`: `[Route("api/v1/[controller]")]` (`/api/v1/Clientes`)
  - `ReservasController.cs`: `[Route("api/v1/[controller]")]` (`/api/v1/Reservas`)
  - `MesasController.cs`: `[Route("api/mesas")]` (Sin versión, en minúsculas)
  - `AdminUsuariosController.cs`: `[Route("api/admin/usuarios")]` (Sin versión, en minúsculas)
- **Impacto:** Arquitectura de URLs rota y confusa para cualquier cliente frontend o consumidor de la API.
- **Solución Recomendada:** Unificar todas las rutas bajo el estándar `api/v1/{recurso}` en minúsculas o kebab-case (ej. `api/v1/mesas`, `api/v1/admin/usuarios`).

---

### [SMELL-03] Estructura de Carpetas vs Namespaces Desalineados
- **Severidad:** Media
- **Archivos:**
  - Archivo en `src/BusinessLogic/Reservas/Interfaces/IMesaService.cs` con namespace `BusinessLogic.Mesas.Interfaces`.
  - Archivo en `src/BusinessLogic/Reservas/Implementations/MesaService.cs` con namespace `BusinessLogic.Mesas.Implementations`.
  - Archivo en `src/BusinessLogic/Reservas/Interfaces/IUsuarioService.cs` con namespace `BusinessLogic.Usuarios.Interfaces`.
  - Archivo en `src/BusinessLogic/Reservas/Implementations/UsuarioService.cs` con namespace `BusinessLogic.Usuarios.Implementations`.
  - Archivo en `src/Shared/DTOs/Mesas/ReservaAsignarMesaDTO.cs` con namespace `Shared.DTOs.Reservas`.
- **Impacto:** Mala organización del código, dificulta la navegación e incumple las convenciones estándar de .NET.
- **Solución Recomendada:** Mover las interfaces e implementaciones a sus carpetas correspondientes (`BusinessLogic/Mesas` y `BusinessLogic/Usuarios`).

---

### [SMELL-04] Violación de SRP: Fusión Indiscriminada de `Cliente` y `Usuario`
- **Severidad:** Media
- **Archivo:** `src/DataAccess/Entities/Cliente.cs`
- **Descripción:** La entidad `Cliente` representa a la vez al comensal del restaurante (nombre, apellido, reservas) y a la credencial del sistema administrativo (`Rol`, `PasswordHash`, `Activo`).
- **Impacto:** Los administradores y gerentes deben figurar en la base de datos como "Clientes" para poder acceder al sistema.
- **Solución Recomendada:** Separar el modelo de identidad (`Usuario`) del perfil de comensal (`Cliente`), vinculados por una relación 1 a 0..1.

---

### [SMELL-05] Pérdida de Diccionario de Errores de Validación en `GlobalExceptionHandler`
- **Severidad:** Media
- **Archivo:** `src/Api/Middleware/GlobalExceptionHandler.cs` (Líneas 78–90)
- **Descripción:** `ValidationException` contiene una propiedad `Errors` (`IDictionary<string, string[]>`), pero el middleware solo mapea `Detail = exception.Message`.
- **Impacto:** El consumidor de la API no recibe qué campos específicos fallaron en validaciones de negocio complejas.
- **Solución Recomendada:** En caso de ser `ValidationException`, volcar `Errors` en `problemDetails.Extensions["errors"]`.

---

### [SMELL-06] Payload JSON en Método HTTP DELETE
- **Severidad:** Media
- **Archivo:** `src/Api/Controllers/AdminUsuariosController.cs` (Líneas 48, 58)
- **Descripción:** `[HttpDelete("{id:guid}")]` recibe `[FromBody] UsuarioEliminarDTO? dto`.
- **Impacto:** Muchos clientes HTTP, proxies inversos (Cloudflare, Nginx, API Gateways) y navegadores descartan silenciosamente el cuerpo de peticiones DELETE, lo que provocará que la confirmación nunca llegue y falle con 400.
- **Solución Recomendada:** Pasar la confirmación por query parameter (`DELETE /api/v1/admin/usuarios/{id}?confirmado=true`).

---

### [SMELL-07] Múltiples Enumeraciones Innecesarias (`Multiple Enumeration Smell`)
- **Severidad:** Baja
- **Archivos:**
  - `MesaService.cs` (`FiltrarMapaMesasAsync`)
  - `ReservaService.cs` (`GetListaEsperaAsync`, `GetTableroOperativoAsync`, `FiltrarReservasAsync`)
- **Descripción:** Se ejecuta `if (!coleccion.Any())` y luego `return coleccion.Select(...)` sobre un `IEnumerable`.
- **Impacto:** Doble iteración innecesaria en memoria.
- **Solución Recomendada:** Materializar a lista (`.ToList()`) una sola vez o evaluar sobre la lista ya resuelta.

---

### [SMELL-08] Anulación de Índices de Base de Datos por `ToLower()` en LINQ
- **Severidad:** Baja
- **Archivo:** `src/DataAccess/Repositories/Implementations/ClienteRepository.cs` (Líneas 37, 42)
- **Descripción:** `c.Email.ToLower() == email.ToLower()`.
- **Impacto:** Como `email` ya viene normalizado en minúsculas, aplicar `.ToLower()` sobre la columna `c.Email` en la base de datos anula el índice único de SQLite o SQL Server, forzando un table scan completo.
- **Solución Recomendada:** Usar directamente `c.Email == email` (o configurar `EF.Functions.Collate` sin sensibilidad a mayúsculas si fuera necesario).

---

### [SMELL-09] Falta de Índices en Columnas Operativas de `Reservas`
- **Severidad:** Baja
- **Archivo:** `src/DataAccess/Context/RestaurantDbContext.cs` (Líneas 47–68)
- **Descripción:** No hay índices definidos sobre `FechaHora`, `Estado` ni combinaciones compuestas (`MesaId, FechaHora`).
- **Impacto:** A medida que crezca el número de reservas históricas, la verificación de disponibilidad y el tablero operativo se volverán lentos.
- **Solución Recomendada:** Crear índices explícitos en `OnModelCreating` sobre `FechaHora` y `(MesaId, FechaHora)`.

---

### [SMELL-10] Clases DTO Huérfanas (Código Muerto)
- **Severidad:** Baja
- **Archivos:**
  - `src/Shared/DTOs/Clientes/ClienteUpdateDTO.cs` (nunca usado en controladores ni servicios)
  - `src/Shared/DTOs/Reservas/ReservaCreateDTO.cs` (reemplazado por `ReservaSolicitudDTO`)
  - `src/Shared/DTOs/Reservas/ReservaUpdateDTO.cs` *(Resuelto: Implementado en RF-07 / CU-08 para modificación de reservas)*
  - `src/Shared/DTOs/Reservas/ReservaCambiarEstadoDTO.cs` *(Resuelto: Implementado en RF-10 / CU-10 para cambio genérico/manual de estado con máquina de estados)*
- **Impacto:** Confusión al desarrollador y aumento de deuda técnica.
- **Solución Recomendada:** Eliminar los DTOs obsoletos o implementar las funcionalidades faltantes si estaban previstas en el backlog.

---

### [SMELL-11] Omisión de `Ubicacion` en `MesaResponseDTO`
- **Severidad:** Baja
- **Archivo:** `src/Shared/DTOs/Mesas/MesaResponseDTO.cs`
- **Descripción:** La entidad `Mesa` contiene `Ubicacion`, pero `MesaResponseDTO` no incluye esta propiedad.
- **Impacto:** Las pantallas de gestión de mesas no pueden mostrar el sector/ubicación física de la mesa (Terraza, Salón VIP, etc.).
- **Solución Recomendada:** Añadir `public string Ubicacion { get; set; } = string.Empty;` a `MesaResponseDTO`.

---

### [SMELL-12] Respuestas de Error Heterogéneas en `MesasController`
- **Severidad:** Baja
- **Archivo:** `src/Api/Controllers/MesasController.cs` (Líneas 49, 69, 89)
- **Descripción:** En caso de mesa no encontrada, el controlador retorna `NotFound(new { mensaje = "Mesa no encontrada." })` en lugar de apoyarse en excepciones o `ProblemDetails`.
- **Impacto:** El consumidor recibe respuestas en formatos distintos: unas veces RFC 7807 (`ProblemDetails` vía `GlobalExceptionHandler`) y otras veces objetos ad-hoc `{ mensaje = "..." }`.
- **Solución Recomendada:** Lanzar `MesaNoEncontradaException` para que el `GlobalExceptionHandler` unifique la respuesta en formato estándar.

---

## Plan de Acción para Corrección Paso a Paso

Cuando des la orden de comenzar las correcciones, podemos avanzar en el siguiente orden secuencial y seguro:

1. **Fase 1: Correcciones Críticas de Seguridad** (`SEC-01`, `SEC-02`, `SEC-03`, `SEC-04`, `SEC-05`)
2. **Fase 2: Corrección de Bugs Lógicos Nucleares** (`BUG-01`, `BUG-02`, `BUG-03`, `BUG-04`, `BUG-05`, `BUG-06`)
3. **Fase 3: Excepciones, Middleware y Base de Datos** (`BUG-08`, `BUG-09`, `BUG-11`, `BUG-12`, `BUG-13`, `SEC-06`, `SEC-07`)
4. **Fase 4: Validaciones Faltantes en DTOs y Servicios** (`VAL-01` a `VAL-07`)
5. **Fase 5: Normalización REST y Limpieza de Code Smells** (`SMELL-01` a `SMELL-12`)

