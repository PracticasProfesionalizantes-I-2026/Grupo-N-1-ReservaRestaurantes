# Caso de Uso: Cancelar reserva

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (el cliente solo podrá cancelar sus propias reservas); RN-02 (no se puede cancelar una reserva en estado En curso o Finalizada); RN-03 (la mesa asignada debe quedar liberada inmediatamente) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-07 |
| **Nombre** | Cancelar reserva |
| **Actor Principal** | Cliente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Cliente → cancelar un turno al que no asistirá; Restaurante → liberar la mesa con anticipación para otros comensales o lista de espera; Clientes en espera → oportunidad de obtener una mesa. |
| **Disparador (Trigger)** | El cliente selecciona la opción "Cancelar Reserva" en su panel de reservas. |
| **Prioridad / Frecuencia** | Alta; media frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (el cliente solo podrá cancelar sus propias reservas); RN-02 (no se puede cancelar una reserva en estado En curso o Finalizada); RN-03 (la mesa asignada debe quedar liberada inmediatamente) |

---

### 1. BREVE DESCRIPCIÓN
Permite al cliente cancelar una reserva previamente solicitada o confirmada, liberando la mesa asignada y actualizando su estado a Cancelada.

### 2. PRECONDICIONES
- El sistema y la base de datos se encuentran operativos.
- El cliente debe poseer una sesión activa (Token JWT con rol Cliente).
- La reserva debe existir y encontrarse en estado susceptible de cancelación (`Pendiente` o `Confirmada`).

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/reservas/{id}/cancelar` con un JSON opcional indicando el motivo de cancelación (`ReservaCancelarDTO`).
2. La **Capa de Presentación** (`ReservasController.CancelarReservaCliente`) valida que el ID sea un identificador válido.
3. La **Capa de Negocio** (`ReservaService.CancelarReservaClienteAsync`) verifica que la reserva pertenezca al cliente autenticado (**RN-01**) y que su estado actual permita la cancelación (**RN-02**).
4. La Capa de Negocio cambia el estado de la reserva a `Cancelada`, libera la mesa asociada (**RN-03**) y evalúa si hay solicitudes en lista de espera para esa fecha y horario.
5. La **Capa de Persistencia** actualiza los cambios en la base de datos dentro de una transacción.
6. El Sistema devuelve un código **200 OK** confirmando la cancelación exitosa con los datos de la reserva actualizada.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Reserva no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 2 el ID enviado no corresponde a ninguna reserva registrada.
  2. La Capa de Negocio lanza una `ReservaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found** indicando: "La reserva solicitada no existe". Fin del caso de uso.

* **3a. Reserva no pertenece al cliente autenticado (HTTP 403 Forbidden):**
  1. Si en el Paso 3 el `ClienteId` de la reserva no coincide con el identificador del token JWT (**RN-01**).
  2. La Capa de Negocio frena la ejecución por violación de seguridad de acceso a datos.
  3. El Sistema devuelve un código **403 Forbidden** con el mensaje: "No posee permisos para cancelar una reserva ajena". Fin del caso de uso.

* **3b. Estado no cancelable (HTTP 409 Conflict):**
  1. Si en el Paso 3 la reserva ya se encuentra en estado `En curso`, `Finalizada` o `Cancelada` (**RN-02**).
  2. La Capa de Negocio lanza una `TransicionEstadoInvalidaException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "No es posible cancelar una reserva que ya se encuentra en curso o finalizada". Fin del caso de uso.

* **5a. Fallo técnico en la persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 se produce un fallo al actualizar el registro en la base de datos.
  2. El Sistema captura la excepción técnica y revierte la transacción.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El cliente puede cancelar desde la aplicación web o desde un cliente móvil.
2. En ambos casos se invoca el mismo endpoint con el ID de reserva en la ruta.

### 6. POSTCONDICIONES
- El estado de la reserva pasa a `Cancelada` de forma persistente en la base de datos.
- La mesa asignada queda liberada y disponible para nuevas asignaciones.
- La reserva cancelada se refleja en el panel operativo del restaurante.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Cancelación exitosa de la reserva y liberación de la mesa. |
| `400` | Bad Request | ID de reserva inválido o mal formado. |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Intento de cancelar una reserva de otro usuario (RN-01). |
| `404` | Not Found | Reserva no encontrada. |
| `409` | Conflict | La reserva no se encuentra en un estado cancelable (RN-02). |
| `500` | Internal Server Error | Falla técnica en la capa de persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de ID de reserva en la ruta HTTP.
- **Verificación (Negocio, → 400/404/409):** RN-01 (pertenencia de la reserva al usuario del token), RN-02 (estado válido para cancelar) y liberación de mesa (**RN-03**) en `ReservaService`.

### Matriz de trazabilidad CU-07 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `CancelarReservaClienteAsync_WhenOwnerAndPending_CancelsAndFreesMesa` | `CancelarReservaCliente_Returns200OK` |
| 2a. Reserva inexistente | `404 Not Found` | `CancelarReservaClienteAsync_WhenNotFound_ThrowsReservaNoEncontradaException` | `CancelarReservaCliente_NonExisting_Returns404NotFound` |
| 3a. Reserva ajena | `403 Forbidden` | `CancelarReservaClienteAsync_WhenNotOwner_ThrowsAccesoDenegadoException` | `CancelarReservaCliente_OtherUserReserva_Returns403Forbidden` |
| 3b. Estado no cancelable | `409 Conflict` | `CancelarReservaClienteAsync_WhenEnCurso_ThrowsTransicionEstadoInvalidaException` | `CancelarReservaCliente_WhenEnCurso_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
