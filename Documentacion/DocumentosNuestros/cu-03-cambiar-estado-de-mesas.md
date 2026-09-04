# Caso de Uso: Cambiar estado de mesas

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (rol exclusivo), RN-02 (protección ante reservas activas), RN-03 (estados válidos) y RN-04 (control de concurrencia y actualización en vivo) implementadas para salvaguardar la integridad física y operativa del salón (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-03 |
| **Nombre** | Cambiar estado de mesas |
| **Actor Principal** | Gerente (usuario autenticado con rol Gerente) |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → mantener el mapa del salón actualizado de forma manual ante la dinámica imprevista del salón; Personal de Salón / Mozos → ubicar comensales con base en información visual fidedigna de mesas libres u ocupadas; Clientes en espera → habilitación inmediata de mesas para ser convocados |
| **Disparador (Trigger)** | El gerente hace clic o selecciona una mesa en el plano interactivo del salón dentro del Panel en Vivo y opta por cambiar su estado |
| **Prioridad / Frecuencia** | Alta; alta frecuencia durante los turnos de apertura del restaurante |
| **Reglas de negocio relacionadas** | RN-01 (autorización exclusiva para rol Gerente); RN-02 (invariante de protección: prohibido liberar mesas con reserva activa o comensales presentes); RN-03 (restricción a estados operativos permitidos: Libre, Reservada, Ocupada); RN-04 (control de concurrencia optimista y sincronización en tiempo real) |

---

### 1. BREVE DESCRIPCIÓN
Permite al Gerente modificar de forma manual y en tiempo real el estado operativo de una mesa (Libre, Reservada, Ocupada) desde el plano interactivo del salón en el Panel en Vivo, garantizando que no se liberen mesas con reservas en curso y previniendo conflictos concurrentes.

### 2. PRECONDICIONES
- El usuario debe haber iniciado sesión y su token JWT debe certificar el rol `Gerente`.
- La mesa a modificar debe existir previamente en la base de datos y pertenecer al salón configurado.
- El sistema y el servicio de señalización en tiempo real (ej. WebSockets / SignalR) deben estar activos.
- La Capa de Persistencia debe encontrarse operativa.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Gerente ingresa al Panel en Vivo y visualiza el mapa del salón con el estado operativo actual de cada mesa.
2. El Gerente hace clic sobre una mesa y selecciona uno de los estados permitidos (`"Libre"`, `"Reservada"` u `"Ocupada"`).
3. El frontend envía una petición al endpoint `PATCH /api/mesas/{id}/estado` con un JSON que contiene `nuevoEstado` y el identificador de concurrencia `version` (rowversion/token de concurrencia).
4. La **Capa de Presentación** (`MesasController.CambiarEstadoMesa`) valida que el identificador `{id}` de la ruta sea un GUID válido y que el valor del nuevo estado corresponda a un elemento de la enumeración `EstadoMesa` (**RN-03**).
5. La **Capa de Negocio** (`MesaService.CambiarEstadoMesaAsync`) verifica:
   - Que el actor cuente con permisos de Gerente (**RN-01**).
   - Que la entidad `Mesa` exista en la Capa de Persistencia.
   - Si el estado solicitado es `Libre`, verifica que no existan reservas activas en estado `En curso` ni clientes asignados a dicha mesa (**RN-02**).
   - Que la versión de la entidad coincida con la enviada, validando que no haya sido modificada en paralelo (**RN-04**).
6. La **Capa de Persistencia** actualiza el campo `Estado` de la entidad en la tabla `Mesas` e incrementa el token de concurrencia.
7. El Sistema emite un evento en tiempo real mediante el hub de comunicación hacia todos los clientes suscritos al Panel en Vivo (**RN-04**).
8. El Sistema responde con código **HTTP 200 OK** conteniendo el DTO de la mesa actualizada (`id`, `numero`, `capacidad`, `estado`, `version`).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. Identificador de mesa inválido o JSON malformado (HTTP 400 Bad Request):**
  1. Si en el Paso 3 el `{id}` no cumple formato GUID o el cuerpo del JSON presenta inconsistencias sintácticas.
  2. La Capa de Presentación (model binding) descarta la solicitud.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Estado de mesa desconocido o fuera de rango (HTTP 400 Bad Request):**
  1. Si en el Paso 3 el valor provisto para `nuevoEstado` no es uno de los permitidos (`Libre`, `Reservada`, `Ocupada`), violando la regla **RN-03**.
  2. La Capa de Presentación rechaza la petición por error de validación de esquema.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "El estado de mesa especificado no es válido". Fin del caso de uso.

* **3a. Mesa inexistente en el sistema (HTTP 404 Not Found):**
  1. Si en el Paso 5 el identificador de mesa no existe en los registros de la base de datos.
  2. La Capa de Negocio no encuentra la entidad y lanza `MesaNotFoundException`.
  3. El Sistema devuelve un código **404 Not Found** con el mensaje: "La mesa solicitada no existe". Fin del caso de uso.

* **3b. Usuario no autorizado para alterar estados de mesa (HTTP 403 Forbidden):**
  1. Si en el Paso 5 el usuario carece del rol `Gerente` exigido por la regla **RN-01**.
  2. El middleware de autorización o la Capa de Negocio bloquea la operación.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3c. Intento de liberar mesa con reserva activa o comensales asignados (HTTP 409 Conflict):**
  1. Si en el Paso 5 el Gerente intenta marcar una mesa como `Libre`, pero existe una reserva activa asociada en estado `En curso`, violando la regla **RN-02**.
  2. La Capa de Negocio detecta la inconsistencia de ocupación y lanza `MesaConReservaActivaException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "No se puede marcar la mesa como Libre porque posee una reserva o servicio activo". Fin del caso de uso.

* **3d. Conflicto de concurrencia / Mesa modificada concurrentemente (HTTP 409 Conflict):**
  1. Si en el Paso 5 otro usuario o proceso modificó el estado de la mesa antes de completarse la petición (discrepancia en el token de concurrencia), conforme a **RN-04**.
  2. La Capa de Negocio o el contexto de persistencia detecta una colisión de concurrencia (`DbUpdateConcurrencyException`).
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "El estado de la mesa fue modificado recientemente por otro usuario. Se ha refrescado la vista". Fin del caso de uso.

* **6a. Error no controlado durante la persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 6 sobreviene una falla técnica o corte de conectividad con el motor de base de datos.
  2. El Sistema interrumpe la transacción y captura el fallo en el middleware global.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El Gerente puede modificar el estado seleccionando la mesa desde el plano gráfico interactivo en dos dimensiones (2D) o desde una vista de tabla resumen de mesas del Panel en Vivo.
2. Ambos canales consumen el mismo endpoint `PATCH /api/mesas/{id}/estado` y producen idéntico efecto.

### 6. POSTCONDICIONES
- El nuevo estado de la mesa queda guardado de manera persistente en la tabla `Mesas`.
- El mapa interactivo del Panel en Vivo se actualiza visualmente en tiempo real para todos los puestos conectados (**RN-04**).
- La disponibilidad global del salón para asignación de nuevos comensales se recalcula automáticamente.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Confirmación de modificación exitosa del estado de la mesa. |
| `400` | Bad Request | Formato de GUID erróneo, cuerpo JSON inválido o estado no reconocido (RN-03). |
| `403` | Forbidden | El usuario no posee el rol `Gerente` requerido para modificar mesas manualmente (RN-01). |
| `404` | Not Found | La mesa especificada no existe en la base de datos. |
| `409` | Conflict | Intento de liberar una mesa con reserva en curso (RN-02) o colisión de concurrencia optimista (RN-04). |
| `500` | Internal Server Error | Falla no controlada a nivel de persistencia o infraestructura. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Verifica el formato sintáctico del identificador GUID y valida que el estado provisto coincida con las constantes declaradas en `EstadoMesa` (**RN-03**).
- **Verificación (Negocio, → 403/404/409):** Confirma la identidad y perfil del actor (**RN-01**), comprueba la existencia de la entidad en la base de datos (→ 404), evalúa que no existan reservas en curso que impidan la liberación (**RN-02**), controla los tokens de versión para prevenir sobreescrituras concurrentes (**RN-04**) y orquesta la emisión del evento en tiempo real.

### Matriz de trazabilidad CU-03 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `CambiarEstadoMesaAsync_WithValidData_UpdatesStatusAndEmitsEvent` | `CambiarEstadoMesa_WithValidState_Returns200Ok` |
| 1a. GUID malformado | `400 Bad Request` | — *(validación de ruta en controller)* | `CambiarEstadoMesa_WithInvalidGuid_Returns400BadRequest` |
| 2a. Estado no permitido | `400 Bad Request` | — *(deserialización / validación DTO)* | `CambiarEstadoMesa_WithInvalidStateEnum_Returns400BadRequest` |
| 3a. Mesa inexistente | `404 Not Found` | `CambiarEstadoMesaAsync_WhenMesaNotFound_ThrowsMesaNotFoundException` | `CambiarEstadoMesa_WhenNonExistent_Returns404NotFound` |
| 3b. Rol no autorizado | `403 Forbidden` | `CambiarEstadoMesaAsync_WithoutGerenteRole_ThrowsUnauthorizedAccessException` | `CambiarEstadoMesa_WithUnauthorizedRole_Returns403Forbidden` |
| 3c. Liberar mesa con reserva activa | `409 Conflict` | `CambiarEstadoMesaAsync_WhenHasActiveBooking_ThrowsMesaConReservaActivaException` | `CambiarEstadoMesa_WhenTableHasActiveBooking_Returns409Conflict` |
| 3d. Conflicto concurrencia | `409 Conflict` | `CambiarEstadoMesaAsync_WhenConcurrencyConflict_ThrowsDbUpdateConcurrencyException` | `CambiarEstadoMesa_WhenConcurrencyConflict_Returns409Conflict` |

> Regla de oro: la lógica de invariantes de mesa (como la no liberación con clientes activos) debe contar con tests unitarios independientes con mocks de repositorio, y tests de integración con cliente HTTP para corroborar los códigos de estado.
