# Caso de Uso: Gestionar reserva

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 a RN-08 implementadas rigurosamente para asegurar la consistencia del ciclo de vida de las reservas y la sincronización con el Panel Operativo en Vivo; cada caso de éxito y de excepción cuenta con tests unitarios e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-02 |
| **Nombre** | Gestionar reserva |
| **Actor Principal** | Gerente (usuario autenticado con rol Gerente) |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → controlar el flujo operativo del restaurante en tiempo real, admitir/rechazar solicitudes y rotar mesas eficientemente; Clientes → recibir confirmación certera sobre el estado de su reserva; Personal de Salón / Mozos → conocer de inmediato las mesas ocupadas, libres o en curso |
| **Disparador (Trigger)** | El gerente accede al Panel Operativo en Vivo, selecciona una reserva y ejecuta una acción de cambio de estado (Confirmar, Rechazar, Iniciar, Finalizar, Cancelar) |
| **Prioridad / Frecuencia** | Alta; alta frecuencia operativa durante los turnos de servicio |
| **Reglas de negocio relacionadas** | RN-01 (autorización exclusiva de rol Gerente); RN-02 (transiciones desde estado Pendiente); RN-03 (transiciones desde estado Confirmada); RN-04 (transiciones desde estado En curso); RN-05 (inmutabilidad de estados terminales); RN-06 (liberación automática de mesa asignada); RN-07 (sincronización y visibilidad inmediata en Panel en Vivo); RN-08 (invariante de transición del ciclo de vida) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente gestionar el ciclo de vida de una reserva registrada desde el Panel Operativo en Vivo, ejecutando transiciones controladas de estado (Confirmar, Rechazar, Iniciar, Finalizar o Cancelar), sincronizando en tiempo real la disponibilidad de mesas del restaurante.

### 2. PRECONDICIONES
- El usuario debe encontrarse autenticado con credenciales válidas y rol `Gerente` en su token JWT.
- La reserva referenciada debe existir previamente en la base de datos del sistema.
- La reserva debe poseer un estado vigente que admita transiciones operativas (no encontrarse en estado terminal).
- La Capa de Persistencia debe encontrarse operativa.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Gerente visualiza en el Panel Operativo en Vivo el listado de reservas con sus estados actuales y selecciona una acción permitida sobre una reserva específica.
2. El cliente frontend envía una petición al endpoint `PATCH /api/reservas/{id}/estado` conteniendo un JSON con el campo `nuevoEstado` (ej: `"Confirmada"`, `"EnCurso"`, `"Finalizada"`, `"Rechazada"`, `"Cancelada"`).
3. La **Capa de Presentación** (`ReservasController.CambiarEstadoReserva`) valida que el identificador `{id}` de la ruta sea un formato GUID válido y que el cuerpo JSON contenga un valor de estado contemplado en la enumeración `EstadoReserva`.
4. La **Capa de Negocio** (`ReservaService.CambiarEstadoReservaAsync`) verifica:
   - Que el usuario posea permisos suficientes conforme a la regla **RN-01**.
   - Que la entidad `Reserva` exista en la base de datos.
   - Que la transición solicitada cumpla estrictamente con las reglas de flujo de estados (**RN-02**, **RN-03**, **RN-04**, **RN-05**, **RN-08**).
5. Si el nuevo estado es `Finalizada`, `Rechazada` o `Cancelada`, la **Capa de Negocio** instruye la liberación de la mesa asociada (**RN-06**), actualizando el estado de la mesa a `Libre`.
6. La **Capa de Persistencia** guarda de forma atómica los cambios en las tablas `Reservas` y `Mesas`.
7. El Sistema emite la notificación de evento en tiempo real para refrescar el Panel Operativo en Vivo (**RN-07**).
8. El Sistema responde con código **HTTP 200 OK** retornando el DTO actualizado de la reserva.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. Formato de identificador o JSON inválido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el parámetro `{id}` no es un GUID parseable o el cuerpo JSON tiene sintaxis dañada.
  2. La Capa de Presentación rechaza la solicitud a nivel de enlace de modelos (model binding).
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Estado solicitado inválido o no reconocido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el valor enviado en `nuevoEstado` no pertenece a la enumeración válida de estados.
  2. La Capa de Presentación o el validador del DTO rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "El estado proporcionado no es válido". Fin del caso de uso.

* **3a. Reserva inexistente en base de datos (HTTP 404 Not Found):**
  1. Si en el Paso 4 el ID no coincide con ningún registro en la tabla `Reservas`.
  2. La Capa de Negocio no encuentra la entidad y lanza `ReservaNotFoundException`.
  3. El Sistema devuelve un código **404 Not Found** con el mensaje: "No se encontró la reserva indicada". Fin del caso de uso.

* **3b. Usuario no autorizado para gestionar reservas (HTTP 403 Forbidden):**
  1. Si en el Paso 4 el token provisto no posee el rol `Gerente` requerido por la regla **RN-01**.
  2. La Capa de Negocio o el filtro de autorización de ASP.NET Core rechaza la ejecución.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3c. Transición de estado no permitida según ciclo de vida (HTTP 409 Conflict):**
  1. Si en el Paso 4 se solicita una transición no contemplada por las reglas de negocio (ej: pasar de `Pendiente` directamente a `En curso`, o de `Pendiente` a `Finalizada`), violando **RN-02**, **RN-03**, **RN-04** o **RN-08**.
  2. La Capa de Negocio detecta la inconsistencia y lanza la excepción `TransicionEstadoInvalidaException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "Transición de estado no permitida para la reserva en su estado actual". Fin del caso de uso.

* **3d. Intento de modificación sobre estado terminal (HTTP 409 Conflict):**
  1. Si en el Paso 4 la reserva se encuentra en estado `Finalizada`, `Cancelada` o `Rechazada`, violando la regla de inmutabilidad **RN-05**.
  2. La Capa de Negocio detecta que la reserva ya alcanzó su estado definitivo y lanza `ReservaEnEstadoTerminalException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "No se pueden realizar modificaciones sobre una reserva finalizada o cancelada". Fin del caso de uso.

* **6a. Error interno al persistir el cambio de estado (HTTP 500 Internal Server Error):**
  1. Si en el Paso 6 ocurre un error de concurrencia en la base de datos o fallo de infraestructura.
  2. El Sistema cancela la transacción y registra el fallo técnico.
  3. El Sistema devuelve un código **500 Internal Server Error** y la reserva conserva su estado previo. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El Gerente puede gatillar la actualización desde la vista de lista del Panel en Vivo, desde el menú contextual del mapa interactivo de mesas, o mediante peticiones directas de API.
2. Todas las variaciones invocan la misma operación de negocio y devuelven código **200 OK** con la entidad actualizada.

### 6. POSTCONDICIONES
- El estado de la reserva queda actualizado y persistido en la base de datos.
- La disponibilidad de la mesa asignada se sincroniza según el nuevo estado: si la reserva pasa a `Finalizada`, `Rechazada` o `Cancelada`, la mesa pasa a estado `Libre` (**RN-06**).
- El Panel Operativo en Vivo actualiza en tiempo real la información visual tanto de la reserva como del mapa del salón (**RN-07**).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Actualización exitosa del estado de la reserva y persistencia en base de datos. |
| `400` | Bad Request | Formato de ID inválido, JSON sintácticamente erróneo o nombre de estado no reconocido. |
| `403` | Forbidden | El usuario autenticado carece del rol `Gerente` exigido por RN-01. |
| `404` | Not Found | El identificador de reserva no corresponde a ningún registro persistido. |
| `409` | Conflict | Transición de estado contraria al ciclo de vida permitido (RN-02, RN-03, RN-04, RN-08) o modificación sobre estados finales (RN-05). |
| `500` | Internal Server Error | Falla no controlada de infraestructura o base de datos durante la transacción. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprueba que el parámetro `{id}` sea un GUID bien formado y que el campo `nuevoEstado` coincida sintácticamente con un valor representable del enum de dominio.
- **Verificación (Negocio, → 403/404/409):** Garantiza la autorización por rol (**RN-01**), comprueba la existencia de la entidad en persistencia (→ 404), evalúa las invariantes de la máquina de estados (**RN-02, RN-03, RN-04, RN-05, RN-08**) y coordina los efectos colaterales sobre la mesa vinculada (**RN-06**) y eventos en tiempo real (**RN-07**).

### Matriz de trazabilidad CU-02 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal (Confirmar) | `200 OK` | `CambiarEstadoReservaAsync_FromPendienteToConfirmada_UpdatesStatus` | `CambiarEstadoReserva_Confirmar_Returns200Ok` |
| Flujo principal (Iniciar) | `200 OK` | `CambiarEstadoReservaAsync_FromConfirmadaToEnCurso_UpdatesStatus` | `CambiarEstadoReserva_Iniciar_Returns200Ok` |
| Flujo principal (Finalizar) | `200 OK` | `CambiarEstadoReservaAsync_FromEnCursoToFinalizada_FreesTableAndUpdatesStatus` | `CambiarEstadoReserva_Finalizar_FreesTableAndReturns200Ok` |
| 1a. Formato de ID inválido | `400 Bad Request` | — *(manejado por validación de ruta en ASP.NET Core)* | `CambiarEstadoReserva_WithInvalidGuid_Returns400BadRequest` |
| 2a. Estado no válido | `400 Bad Request` | — *(validación de serialización / enum)* | `CambiarEstadoReserva_WithUnrecognizedStatus_Returns400BadRequest` |
| 3a. Reserva inexistente | `404 Not Found` | `CambiarEstadoReservaAsync_WhenNotFound_ThrowsReservaNotFoundException` | `CambiarEstadoReserva_WhenNonExistent_Returns404NotFound` |
| 3b. Rol no autorizado | `403 Forbidden` | `CambiarEstadoReservaAsync_WithoutGerenteRole_ThrowsUnauthorizedAccessException` | `CambiarEstadoReserva_WithClientRole_Returns403Forbidden` |
| 3c. Transición inválida | `409 Conflict` | `CambiarEstadoReservaAsync_WhenInvalidTransition_ThrowsTransicionEstadoInvalidaException` | `CambiarEstadoReserva_WhenIllegalTransition_Returns409Conflict` |
| 3d. Modificación estado final | `409 Conflict` | `CambiarEstadoReservaAsync_WhenAlreadyFinalized_ThrowsReservaEnEstadoTerminalException` | `CambiarEstadoReserva_WhenFinalized_Returns409Conflict` |

> Regla de oro: cada flujo alternativo y transición de estado cuenta con tests unitarios específicos en la capa de servicios y tests de integración en el endpoint HTTP.
