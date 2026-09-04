# Caso de Uso: Rechazar reserva

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Gerente podrá rechazar reservas); RN-02 (solo podrán rechazarse reservas en estado Pendiente o Solicitada); RN-03 (debe informarse el motivo del rechazo al cliente) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-16 |
| **Nombre** | Rechazar reserva |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → desestimar solicitudes de reserva no viables (por sobrecapacidad, evento privado o mantenimiento); Cliente → recibir notificación fundada del rechazo de su solicitud. |
| **Disparador (Trigger)** | El gerente selecciona la acción "Rechazar" sobre una reserva pendiente en el panel operativo. |
| **Prioridad / Frecuencia** | Alta; media frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (solo el Gerente podrá rechazar reservas); RN-02 (solo podrán rechazarse reservas en estado Pendiente o Solicitada); RN-03 (debe informarse el motivo del rechazo al cliente) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente rechazar una solicitud de reserva pendiente especificando el motivo, impidiendo su confirmación e informando la resolución.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".
- La reserva debe existir y encontrarse en estado `Pendiente` (o `Solicitada`).

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/reservas/{id}/rechazar` con un JSON conteniendo `motivoRechazo` (`ReservaRechazarDTO`).
2. La **Capa de Presentación** (`ReservasController.RechazarReserva`) valida la autorización del rol `Gerente` (**RN-01**) y que el campo `motivoRechazo` esté presente y no vacío.
3. La **Capa de Negocio** (`ReservaService.RechazarReservaAsync`) verifica que la reserva exista y que su estado actual sea `Pendiente` (**RN-02**).
4. La Capa de Negocio actualiza el estado de la reserva a `Rechazada`, guarda el motivo (**RN-03**) y libera cualquier bloqueo provisorio de mesa.
5. La **Capa de Persistencia** guarda los cambios en la base de datos.
6. El Sistema devuelve un código **200 OK** con los datos de la reserva rechazada y el motivo asociado.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Motivo de rechazo faltante o vacío (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON no incluye `motivoRechazo` o se envía una cadena vacía.
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "Debe especificar un motivo de rechazo". Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el usuario no posee rol `Gerente` (**RN-01**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Reserva no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID no existe en la base de datos.
  2. La Capa de Negocio lanza una `ReservaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Reserva en estado no rechazable (HTTP 409 Conflict):**
  1. Si en el Paso 3 la reserva ya ha sido confirmada, iniciada o cancelada (**RN-02**).
  2. La Capa de Negocio lanza una `TransicionEstadoInvalidaException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "Solo pueden rechazarse reservas en estado Pendiente". Fin del caso de uso.

* **5a. Fallo técnico en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre una falla de base de datos.
  2. El Sistema registra la excepción no controlada.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El rechazo puede realizarse desde la vista detallada de la reserva o desde la lista de pendientes.
2. En ambos casos se invoca `PATCH /api/reservas/{id}/rechazar`.

### 6. POSTCONDICIONES
- La reserva queda con estado `Rechazada` de forma persistente en la base de datos.
- El cliente puede visualizar el motivo del rechazo en su panel.
- No queda ningún recurso ni mesa retenida por dicha reserva.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Rechazo de reserva procesado exitosamente. |
| `400` | Bad Request | Motivo de rechazo faltante o inválido (RN-03). |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente (RN-01). |
| `404` | Not Found | Reserva no encontrada. |
| `409` | Conflict | Reserva en estado no rechazable (RN-02). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Campo `motivoRechazo` obligatorio en `ReservaRechazarDTO` y verificación de rol Gerente en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Gerente), RN-02 (estado actual Pendiente) y RN-03 (persistencia del motivo de rechazo) en `ReservaService`.

### Matriz de trazabilidad CU-16 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `RechazarReservaAsync_WhenPendingWithReason_RejectsSuccessfully` | `RechazarReserva_Returns200OK` |
| 2a. Sin motivo | `400 Bad Request` | `— (validado por DTO DataAnnotations)` | `RechazarReserva_WithoutMotivo_Returns400BadRequest` |
| 2b. Sin rol Gerente | `403 Forbidden` | `— (middleware de autorización)` | `RechazarReserva_AsCliente_Returns403Forbidden` |
| 3a. Reserva inexistente | `404 Not Found` | `RechazarReservaAsync_WhenNotFound_ThrowsReservaNoEncontradaException` | `RechazarReserva_NonExistingId_Returns404NotFound` |
| 3b. Estado no rechazable | `409 Conflict` | `RechazarReservaAsync_WhenConfirmada_ThrowsTransicionEstadoInvalidaException` | `RechazarReserva_WhenConfirmada_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
