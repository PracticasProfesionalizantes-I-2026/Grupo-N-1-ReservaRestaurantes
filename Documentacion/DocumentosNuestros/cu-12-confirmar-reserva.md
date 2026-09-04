# Caso de Uso: Confirmar reserva

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Gerente podrá confirmar reservas); RN-02 (solo podrán confirmarse reservas en estado Pendiente); RN-03 (debe poseer una mesa asignada y con capacidad suficiente sin solapamientos) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-12 |
| **Nombre** | Confirmar reserva |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → ratificar la reserva de un cliente asignando formalmente el recurso; Cliente → recibir la confirmación de su mesa para el horario solicitado. |
| **Disparador (Trigger)** | El gerente selecciona la acción "Confirmar" sobre una reserva pendiente en el panel operativo. |
| **Prioridad / Frecuencia** | Alta; alta frecuencia durante la jornada |
| **Reglas de negocio relacionadas** | RN-01 (solo el Gerente podrá confirmar reservas); RN-02 (solo podrán confirmarse reservas en estado Pendiente); RN-03 (debe poseer una mesa asignada y con capacidad suficiente sin solapamientos) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente confirmar formalmente una solicitud de reserva pendiente, asegurando la asignación de la mesa y notificando el estado confirmado.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".
- La reserva debe existir y encontrarse en estado `Pendiente` (o `Solicitada`).

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/reservas/{id}/confirmar` con su token JWT.
2. La **Capa de Presentación** (`ReservasController.ConfirmarReserva`) valida la autorización del rol `Gerente` (`[Authorize(Roles = "Gerente")]`) (**RN-01**).
3. La **Capa de Negocio** (`ReservaService.ConfirmarReservaAsync`) verifica la existencia de la reserva, comprueba que su estado sea `Pendiente` (**RN-02**) y constata que tenga una mesa asignada válida (**RN-03**).
4. La Capa de Negocio actualiza el estado de la reserva a `Confirmada` y reserva formalmente el recurso mesa para esa franja horaria.
5. La **Capa de Persistencia** guarda los cambios de estado en la base de datos.
6. El Sistema devuelve un código **200 OK** con los datos de la reserva confirmada.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token presentado no cuenta con el rol `Gerente` (**RN-01**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Reserva no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID provisto no coincide con ninguna reserva en el sistema.
  2. La Capa de Negocio lanza una `ReservaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Reserva en estado no apto para confirmación (HTTP 409 Conflict):**
  1. Si en el Paso 3 la reserva no está en estado `Pendiente` (por ejemplo, ya fue `Confirmada`, `Cancelada` o `Rechazada`) (**RN-02**).
  2. La Capa de Negocio lanza una `TransicionEstadoInvalidaException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "Solo pueden confirmarse reservas en estado Pendiente". Fin del caso de uso.

* **3c. Mesa no asignada o con conflicto de horario (HTTP 409 Conflict):**
  1. Si en el Paso 3 la reserva no tiene una mesa asignada o la mesa presenta un solapamiento sobrevenido (**RN-03**).
  2. La Capa de Negocio frena la confirmación y solicita la resolución previa de la mesa.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "La reserva requiere una mesa válida asignada para poder confirmarse". Fin del caso de uso.

* **5a. Fallo técnico en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre un error de base de datos.
  2. El Sistema registra la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El gerente puede confirmar la reserva desde el panel operativo o desde el detalle individual de la reserva.
2. En ambos casos se invoca `PATCH /api/reservas/{id}/confirmar`.

### 6. POSTCONDICIONES
- La reserva pasa a estado `Confirmada` en la base de datos.
- El cliente visualiza su reserva como confirmada.
- El horario de la mesa queda bloqueado en los motores de búsqueda de turnos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Confirmación exitosa de la reserva. |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente (RN-01). |
| `404` | Not Found | Reserva no encontrada. |
| `409` | Conflict | Estado inválido para confirmación (RN-02) o falta de mesa asignada (RN-03). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de rol Gerente y formato de ID de reserva en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Gerente), RN-02 (estado actual Pendiente), RN-03 (disponibilidad y asignación de mesa) en `ReservaService`.

### Matriz de trazabilidad CU-12 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `ConfirmarReservaAsync_WhenPendingAndTableAssigned_ConfirmsSuccessfully` | `ConfirmarReserva_Returns200OK` |
| 2a. Sin rol Gerente | `403 Forbidden` | `— (middleware de autorización)` | `ConfirmarReserva_AsCliente_Returns403Forbidden` |
| 3a. Reserva inexistente | `404 Not Found` | `ConfirmarReservaAsync_WhenNotFound_ThrowsReservaNoEncontradaException` | `ConfirmarReserva_NonExistingId_Returns404NotFound` |
| 3b. Estado inválido | `409 Conflict` | `ConfirmarReservaAsync_WhenAlreadyConfirmed_ThrowsTransicionEstadoInvalidaException` | `ConfirmarReserva_WhenNotPending_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
