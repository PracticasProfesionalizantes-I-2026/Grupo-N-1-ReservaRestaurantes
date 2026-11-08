# Issues Pendientes — Grupo N°1 ReservaRestaurantes

> Backlog de auditoría listo para copiar y pegar en GitHub Issues.
> Cada ítem indica **archivo/línea o módulo** y la **acción correctiva**.
> Convención: `- [ ]` = pendiente; tachar o marcar al resolver.

---

### 🔴 RIESGO ALTO (Crítico para la ejecución y completitud core)

- [ ] **La solución no compila (NU1605).**
  **Archivo:** `src/BusinessLogic/BusinessLogic.csproj:16` (fija `Microsoft.Extensions.Configuration.Abstractions 10.0.13`) vs. `src/DataAccess/DataAccess.csproj` (`Microsoft.EntityFrameworkCore.Sqlite 10.0.*` resuelve `10.0.14`).
  **Acción:** alinear todas las versiones de paquetes Microsoft a `10.0.14` (o fijar `10.0.14` explícito en `BusinessLogic`), ejecutar `dotnet restore` y verificar `dotnet build`.

- [ ] **La migración `InitialCreate` no refleja el modelo actual (drift de esquema).**
  **Archivo:** `src/Migrations/Migrations/20260910190502_InitialCreate.cs` (faltan `PasswordHash`, `Rol`, `Activo` en `Clientes`).
  **Acción:** crear una migración nueva (`dotnet ef migrations add SyncModel`) o regenerar `InitialCreate`; alinear `RestaurantDbContextModelSnapshot`. Verificar que `dotnet ef database update` produzca el mismo esquema que `EnsureCreated`.

- [ ] **`DbInitializer` ignora las migraciones.**
  **Archivo:** `src/DataAccess/Data/DbInitializer.cs:13` (`EnsureCreatedAsync`).
  **Acción:** reemplazar por `context.Database.MigrateAsync()` y mantener el seed idempotente. `EnsureCreated` y migraciones no deben convivir.

- [ ] **IDOR: un cliente puede cancelar reservas ajenas.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs:137-172` (`CancelarReservaClienteAsync` recibe `clienteId` pero nunca compara con `reserva.ClienteId`); `src/Api/Controllers/ReservasController.cs:61-80`.
  **Acción:** validar `reserva.ClienteId == clienteId` y lanzar `AccesoDenegadoException` (o `NotFoundException` para no filtrar existencia) cuando no coincida. Agregar test de intento cruzado.

- [ ] **IDOR: cualquier usuario autenticado puede ver cualquier reserva.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs:97-104` (`GetByIdAsync`) y `src/Api/Controllers/ReservasController.cs:48-56`.
  **Acción:** si el rol es `Cliente`, verificar propiedad de la reserva; permitir acceso amplio solo a `Gerente`/`Admin`. Agregar `[Authorize(Roles=...)]` o *resource-based authorization*.

- [ ] **`ClientesController` está completamente anónimo y expone PII.**
  **Archivo:** `src/Api/Controllers/ClientesController.cs:8-44` (sin `[Authorize]`; `GET /api/v1/Clientes/{id}` devuelve email/teléfono).
  **Acción:** proteger con `[Authorize(Roles="Admin,Gerente")]` o permitir solo el propio perfil; evaluar si el alta de cliente debe seguir siendo anónima y, si sí, separarla en un endpoint de registro con rate limiting.

- [ ] **RF-07 no implementado: no se puede modificar una reserva.**
  **Archivo:** `src/Shared/DTOs/Reservas/ReservaUpdateDTO.cs` (sin uso), `ReservaService` (sin método), `ReservasController` (sin endpoint).
  **Acción:** implementar `PATCH/PUT /api/v1/reservas/{id}` con validación de propiedad, disponibilidad, solapamiento y estado; documentarlo como CU nuevo o dentro de CU-06.

- [ ] **RF-10 no implementado: no existe cambio manual/genérico de estado.**
  **Archivo:** `src/Shared/DTOs/Reservas/ReservaCambiarEstadoDTO.cs` (sin uso).
  **Acción:** implementar endpoint de cambio de estado con validación de transiciones permitidas (máquina de estados) y autorización por rol; o eliminar el DTO y documentar que los estados solo cambian por acciones específicas.

- [ ] **RF-13 / CU-18: la lista de espera no promueve ni reasigna reservas.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs:181-192` (solo lista `Pendiente`); `src/Api/Controllers/ReservasController.cs:208-218` (solo `GET`).
  **Acción:** implementar `ListaEsperaService` + `PATCH .../promover` (o endpoint equivalente) que asigne mesa liberada a la reserva en espera y notifique; cubrir con tests.

- [ ] **CU-04: no existe el endpoint de registro documentado.**
  **Archivo:** `src/Api/Controllers/AuthController.cs` (solo `login`/`logout`); no existen `UsuarioRegisterDTO`, `AuthService.RegisterAsync`.
  **Acción:** implementar `POST /api/v1/auth/register` con validación, hash BCrypt y rol `Cliente`; o corregir los CU para documentar el alta vía `ClientesController` y unificar el contrato.

- [ ] **CU-09: no existe `contexto-inicio`.**
  **Archivo:** no hay `UsuarioController` ni `GetContextoInicioAsync`.
  **Acción:** implementar `GET /api/v1/usuario/contexto-inicio` (datos del usuario + resumen del día) o eliminar el CU y su RF asociado.

- [ ] **Condición de carrera al reservar (RNF-06).**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs:49-91` (check-then-act sin transacción); `RestaurantDbContext` sin índice único `(MesaId, FechaHora)`.
  **Acción:** envolver la operación en una transacción con nivel de aislamiento adecuado, agregar índice único parcial o bloqueo optimista (`RowVersion`), y capturar `DbUpdateException` como `ConflictException`. Agregar test de concurrencia.

- [ ] **Secreto JWT hardcodeado y expiración excesiva.**
  **Archivo:** `src/Api/Program.cs:33`, `src/BusinessLogic/Auth/Implementations/JwtTokenGenerator.cs:22`, `src/Api/appsettings.json:13`.
  **Acción:** eliminar el fallback literal; fallar al iniciar si `Jwt:SecretKey` no está configurado; usar User Secrets/variables de entorno; reducir `ExpirationMinutes` a un valor razonable (p. ej. 60) y agregar refresh token.

- [ ] **No hay revocación de tokens al cambiar rol o eliminar usuario.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/UsuarioService.cs` (cambio de rol/baja sin invalidar JWT); `AuthController.Logout` (`src/Api/Controllers/AuthController.cs:40-48`) no revoca nada.
  **Acción:** implementar lista de revocación/`SecurityStamp`/refresh tokens con rotación; validar el sello en la autenticación; documentar la limitación si se decide no hacerlo.

- [ ] **Fallback de contraseña en texto plano.**
  **Archivo:** `src/BusinessLogic/Auth/Implementations/AuthService.cs:64-79` (`VerifyPassword`).
  **Acción:** aceptar únicamente hashes BCrypt; si el formato no es BCrypt, denegar el login y forzar reseteo. Eliminar la comparación directa.

- [ ] **Los clientes semilla no pueden iniciar sesión.**
  **Archivo:** `src/DataAccess/Data/DbInitializer.cs:22-62` (clientes sin `PasswordHash`).
  **Acción:** asignar `PasswordHash` con BCrypt a todos los usuarios semilla o marcar explícitamente que son solo datos de catálogo; documentar credenciales demo.

- [ ] **`AsignarMesaAsync` confunde `mesaId` con `numero`.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs:335-343` (`GetByNumeroAsync(mesaId)`).
  **Acción:** usar `GetByIdAsync(dto.MesaId)` y validar capacidad/estado/solapamiento; renombrar el DTO a `MesaId` si corresponde y agregar test.

- [ ] **`ModificarMesaAsync` ignora número/ubicación y no valida capacidad (RF-02/RN-03).**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/MesaService.cs:50-70`; `src/Shared/DTOs/Mesas/MesaUpdateDTO.cs`.
  **Acción:** actualizar todos los campos editables, validar unicidad de `Numero` y que la nueva capacidad no rompa reservas futuras; agregar DataAnnotations y tests.

- [ ] **CU-16: rechazar reserva no registra motivo ni usa estado `Rechazada`.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs:305-333`; `src/Api/Controllers/ReservasController.cs:170-181`; `src/Shared/Enums/ReservaEstado.cs` (sin `Rechazada`).
  **Acción:** agregar `Rechazada` al enum (o justificar su ausencia), recibir `motivoRechazo`, persistirlo y devolverlo; actualizar CU y migración si cambia el esquema.

- [ ] **CU-15: cancelación por gerente sin motivo obligatorio.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs:273-303`; `src/Api/Controllers/ReservasController.cs:154-165` (sin `[FromBody]`).
  **Acción:** exigir `motivo` en el cuerpo para cancelaciones de gerente, validarlo y persistirlo; documentar el contrato.

---

### 🟡 RIESGO MEDIO (Mantenibilidad y Flujos Secundarios)

- [ ] **Filtros vacíos devuelven 404 en lugar de 200 con lista vacía.**
  **Archivo:** `MesaService.FiltrarMapaMesasAsync` (`src/BusinessLogic/Reservas/Implementations/MesaService.cs:125-136`), `ReservaService.FiltrarReservas` (`.../ReservaService.cs:396-410`), `ReservaService.GetListaEsperaAsync` (`.../ReservaService.cs:181-192`).
  **Acción:** devolver `200 OK` con colección vacía; reservar `404` para recursos individuales inexistentes.

- [ ] **Login de cuenta inactiva devuelve 401 en lugar de 403.**
  **Archivo:** `src/BusinessLogic/Auth/Implementations/AuthService.cs:30-34`.
  **Acción:** usar `CuentaInactivaException` mapeada a `403 Forbidden` y actualizar la tabla de códigos del CU-05.

- [ ] **Logout es un no-op.**
  **Archivo:** `src/Api/Controllers/AuthController.cs:40-48`; `AuthService.LogoutAsync` (`.../AuthService.cs:57-62`).
  **Acción:** implementar revocación real (refresh token/denylist) o documentar explícitamente que el logout es del lado del cliente y eliminar el endpoint si no aporta.

- [ ] **Eliminar mesa bloquea por reservas históricas (contradice CU-23).**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/MesaService.cs:72-93`.
  **Acción:** bloquear solo reservas en estados activos/futuros; permitir baja lógica (`Activa = false`) cuando solo haya historial, o exigir confirmación explícita.

- [ ] **Confirmar reserva no revalida mesa ni solapamiento (RN-03 del CU-12).**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/ReservaService.cs:194-215`.
  **Acción:** revalidar disponibilidad al confirmar; si la mesa ya no sirve, lanzar `ConflictException` o `MesaNoCompatibleException` y documentar la regla.

- [ ] **Validación de entrada incompleta en DTOs de mesas.**
  **Archivo:** `src/Shared/DTOs/Mesas/MesaCreateDTO.cs`, `MesaUpdateDTO.cs`, `MesaCambiarEstadoDTO.cs`.
  **Acción:** agregar `[Required]`, `[Range(1,30)]`, `[StringLength]`, `[EnumDataType]`; agregar `Ubicacion` a create/update; validar enum en el servicio.

- [ ] **Enums inválidos aceptados por `MesaCambiarEstadoDTO`.**
  **Archivo:** `src/Api/Controllers/MesasController.cs` (endpoint de estado) + DTO.
  **Acción:** validar `Enum.IsDefined` antes de aplicar y devolver `400` con detalle; agregar test.

- [ ] **Inconsistencia de rutas (`/api/v1` vs `/api`).**
  **Archivo:** `ReservasController.cs:10`, `AuthController.cs:9`, `ClientesController.cs:8` (`api/v1`) vs. `MesasController.cs` (`api/mesas`) y `AdminUsuariosController.cs` (`api/admin/usuarios`).
  **Acción:** unificar prefijo y versionado (`api/v1/...`) o documentar la excepción; actualizar todos los CU.

- [ ] **Los CU referencian clases/servicios/endpoints inexistentes.**
  **Archivo:** `Documentacion/DocumentosNuestros/cu-02`, `cu-04`, `cu-09`, `cu-18` (`GerenteReservaService`, `UsuarioRegisterDTO`, `UsuarioController`, `ListaEsperaService`, `MesaNoCompatibleException`, etc.).
  **Acción:** alinear la documentación con el código real o implementar lo prometido; mantener una única fuente de verdad de contratos.

- [ ] **No hay pruebas de integración, aunque las matrices las declaran.**
  **Archivo:** `tests/Tests/` (solo unitarias: 13+26+11) y matrices de los 23 CU.
  **Acción:** agregar `WebApplicationFactory` + `HttpClient` para flujos críticos (login, solicitar, cancelar, confirmar) o corregir las matrices para reflejar solo pruebas unitarias.

- [ ] **Nombres de tests documentados ≠ tests reales.**
  **Archivo:** matrices de trazabilidad de los CU vs. `tests/Tests/*.cs`.
  **Acción:** regenerar las matrices desde el código o renombrar los tests; automatizar la verificación de trazabilidad.

- [ ] **`ReservaNoShowWorker`: lógica no documentada y posible bug de zona horaria.**
  **Archivo:** `src/BusinessLogic/Workers/ReservaNoShowWorker.cs:35` (`DateTime.UtcNow.AddMinutes(-15)` comparado con fechas locales).
  **Acción:** definir política de zona horaria (UTC en persistencia), documentar el worker en un CU/RF, hacerlo configurable y agregar tests con reloj inyectable.

- [ ] **Estado `EnMantenimiento` no coincide con "Fuera de servicio" del CU-03.**
  **Archivo:** `src/Shared/Enums/MesaEstado.cs:8` vs. `Documentacion/DocumentosNuestros/cu-03`.
  **Acción:** unificar nomenclatura y valores; actualizar CU y UI/contratos.

- [ ] **No existe estado `Rechazada` en `ReservaEstado`.**
  **Archivo:** `src/Shared/Enums/ReservaEstado.cs`.
  **Acción:** decidir si se agrega o si "rechazada" se modela como `Cancelada`; documentar la decisión y actualizar CU-16.

- [ ] **Política de contraseñas débil (6 caracteres) vs. RNF/documentación.**
  **Archivo:** `src/Shared/DTOs/Auth/LoginRequestDTO.cs` (o DTO de registro) y validaciones de `ClienteCreateDTO`.
  **Acción:** aplicar mínimo 8 con complejidad o justificar el mínimo actual; alinear RNF-02.

- [ ] **Sin CORS, rate limiting ni bloqueo por intentos fallidos.**
  **Archivo:** `src/Api/Program.cs`.
  **Acción:** configurar CORS por política, `AddRateLimiter` en login/registro y lockout temporal; documentar en RNF de seguridad.

- [ ] **Sin auditoría de cambios de rol/estado/baja de usuarios.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/UsuarioService.cs`, `AdminUsuariosController`.
  **Acción:** registrar `actor`, `acción`, `entidad`, `antes/después` y timestamp; exponer o persistir según RNF de trazabilidad.

- [ ] **Operaciones que tocan `Reservas` y `Mesas` no son atómicas.**
  **Archivo:** `ReservaService` (confirmar/iniciar/finalizar/cancelar) y `MesaService`.
  **Acción:** introducir `IUnitOfWork`/transacción para que el cambio de estado de mesa y reserva sea atómico.

- [ ] **Falta `Ubicacion` en los DTOs de alta/edición de mesa.**
  **Archivo:** `src/Shared/DTOs/Mesas/MesaCreateDTO.cs`, `MesaUpdateDTO.cs`.
  **Acción:** agregar el campo y persistirlo; actualizar CU-21/22 y tests.

- [ ] **La lista de espera se modela como `Pendiente`, sin semántica propia.**
  **Archivo:** `ReservaService.GetListaEsperaAsync` (`.../ReservaService.cs:181-192`).
  **Acción:** definir estado `EnEspera` o un flag dedicado; separar "pendiente de confirmación" de "sin mesa disponible".

- [ ] **Manejo de errores inconsistente con los nombres de excepción documentados.**
  **Archivo:** `src/Shared/Exceptions/*`, `src/Api/Middleware/GlobalExceptionHandler.cs` vs. CU.
  **Acción:** unificar catálogo de excepciones y mapeos; actualizar tablas de códigos HTTP de los CU.

- [ ] **`ToLower()` sensible a la cultura en emails.**
  **Archivo:** `src/BusinessLogic/Auth/Implementations/AuthService.cs:27`, repositorio de clientes.
  **Acción:** usar `ToLowerInvariant()` y normalizar en un único punto (value object `Email`).

- [ ] **README sin instrucciones de ejecución ni documentación autocontenida.**
  **Archivo:** `README.md`.
  **Acción:** agregar requisitos, `dotnet restore/build/run`, configuración de JWT/DB, credenciales demo y enlaces internos; evitar depender solo de Drive/Google Docs.

- [ ] **Sin paginación en listados (CU-11 y tablero operativo).**
  **Archivo:** `ReservaService.FiltrarReservas`, `MesaService.FiltrarMapaMesasAsync`.
  **Acción:** agregar `page`/`pageSize` y metadatos de paginación; documentar en CU.

---

### 🟢 RIESGO BAJO (Deuda técnica y Mejoras Menores)

- [ ] **Archivo de excepción vacío.**
  **Archivo:** `src/Shared/Exceptions/AccesoDenegadoException.cs`.
  **Acción:** implementar la excepción (hereda de `ForbiddenException`/`Exception`) o eliminarla si no se usa.

- [ ] **Namespace y carpeta no coinciden en servicios.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/MesaService.cs:1` (namespace `BusinessLogic.Mesas`), `UsuarioService.cs` (namespace `BusinessLogic.Usuarios`).
  **Acción:** mover archivos a `Mesas/` y `Usuarios/` o renombrar namespaces; actualizar usings.

- [ ] **DTOs sin uso (código muerto).**
  **Archivo:** `ReservaUpdateDTO`, `ReservaCreateDTO`, `ReservaCambiarEstadoDTO`, `ClienteUpdateDTO`, `UsuarioResponseDTO`.
  **Acción:** implementar los flujos asociados o eliminarlos; evitar contratos fantasma.

- [ ] **Comentario de CU incorrecto en logout.**
  **Archivo:** `src/Api/Controllers/AuthController.cs` (dice CU-07; corresponde a CU-08).
  **Acción:** corregir la referencia.

- [ ] **`[Required]` inefectivo sobre tipos de valor.**
  **Archivo:** `src/Shared/DTOs/Reservas/ReservaSolicitudDTO.cs` (`DateTime`, `int`).
  **Acción:** usar `[Range]`/`[CustomValidation]` o nullable + validación explícita.

- [ ] **Formato/legibilidad mejorable.**
  **Archivo:** `src/BusinessLogic/Reservas/Implementations/MesaService.cs:119-120` (cadena de `Where` mal indentada).
  **Acción:** aplicar `dotnet format` y revisar indentación.

- [ ] **Hueco de numeración en requerimientos (RF-18 inexistente).**
  **Archivo:** `Documentacion/DocumentosNuestros/Documentacion (1).md`.
  **Acción:** renumerar o justificar el salto; verificar que las matrices no referencien RF-18.

- [ ] **`Api.http` contiene el endpoint de ejemplo `weatherforecast`.**
  **Archivo:** `src/Api/Api.http`.
  **Acción:** reemplazar por requests reales del sistema (login, solicitar reserva, etc.).

- [ ] **Artefactos `bin/obj` versionados pese a `.gitignore`.**
  **Archivo:** `src/**/obj/*`, `tests/**/obj/*` (trackeados).
  **Acción:** `git rm -r --cached` de `bin/obj`, confirmar `.gitignore` y agregar regla de CI.

- [ ] **Usings redundantes.**
  **Archivo:** p. ej. `src/Shared/Exceptions/ConflictException.cs` (`using Shared.Exceptions;` dentro del mismo namespace), `JwtTokenGenerator.cs` (`using DataAccess.Entities` si aplica).
  **Acción:** limpiar con analizadores/IDE.

- [ ] **Sin propagación de `CancellationToken`.**
  **Archivo:** controladores, servicios y repositorios.
  **Acción:** agregar `CancellationToken` en firmas async y pasarlo a EF Core.

- [ ] **Ubicación de DTO fuera de convención.**
  **Archivo:** `src/Shared/DTOs/Reservas/ReservaAsignarMesaDTO.cs` (namespace `Reservas`, no `DTOs.Reservas`).
  **Acción:** unificar namespace con la carpeta.

- [ ] **Worker de no-show siempre activo y no configurable.**
  **Archivo:** `src/Api/Program.cs` (registro de `ReservaNoShowWorker`).
  **Acción:** agregar flag de configuración (`Workers:NoShow:Enabled`) y parámetros (tolerancia, intervalo).

- [ ] **`GetReservasOperativasAsync` sin `AsNoTracking`.**
  **Archivo:** `src/DataAccess/Repositories/Implementations/ReservaRepository.cs`.
  **Acción:** usar `AsNoTracking()` en consultas de solo lectura.

- [ ] **Respuestas 200 vs. 204 inconsistentes.**
  **Archivo:** controladores (`MesasController`, `AdminUsuariosController`).
  **Acción:** unificar criterio y reflejarlo en `[ProducesResponseType]` y en los CU.

- [ ] **Falta documentación XML en endpoints públicos.**
  **Archivo:** `src/Api/Controllers/*`.
  **Acción:** agregar `<summary>`/`<response>` para habilitar Swagger descriptivo.

- [ ] **`ClienteService.GetAllAsync` y `ClienteUpdateDTO` sin endpoint asociado.**
  **Archivo:** `src/BusinessLogic/Cliente/Implementations/ClienteService.cs`, `src/Shared/DTOs/Clientes/ClienteUpdateDTO.cs`.
  **Acción:** exponer `GET /clientes` y `PUT /clientes/{id}` con autorización, o eliminar el código no usado.

- [ ] **`SinMesasDisponiblesException` hereda de `NotFoundException` para listados vacíos.**
  **Archivo:** `src/Shared/Exceptions/SinMesasDisponiblesException.cs`.
  **Acción:** si se corrige el 404 de listados, devolver lista vacía y reservar la excepción para el flujo de solicitud sin mesas.

- [ ] **Sin pruebas para `AuthService`, `ClienteService` y el worker.**
  **Archivo:** `tests/Tests/`.
  **Acción:** agregar tests unitarios de login (válido/inactivo/password inválido), alta de cliente y transición a `NoShow`.

- [ ] **Sin pipeline de CI (build + test + format).**
  **Archivo:** raíz del repositorio (`.github/workflows/`).
  **Acción:** agregar workflow que ejecute `dotnet restore`, `dotnet build -warnaserror`, `dotnet test` y `dotnet format --verify-no-changes`.
