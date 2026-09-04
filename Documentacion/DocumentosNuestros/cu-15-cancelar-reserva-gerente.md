# Caso de Uso: Cancelar reserva (Gerente)

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Gerente podrá cancelar reservas desde el panel de gestión); RN-02 (debe indicarse el motivo de cancelación: No-show, cancelación telefónica, etc.); RN-03 (la mesa asignada queda inmediatamente liberada) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-15 |
| **Nombre** | Cancelar reserva (Gerente) |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → anular reservas por incomparecencia (no-show) o solicitud telefónica; Clientes en espera → recibir reasignación de mesa liberada; Restaurante → minimizar mesas desaprovechadas. |
| **Disparador (Trigger)** | El gerente selecciona "Cancelar reserva" en el panel administrativo ante ausencia del cliente o aviso imprevisto. |
| **Prioridad / Frecuencia** | Alta; media a alta frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (solo el Gerente podrá cancelar reservas desde el panel de gestión); RN-02 (debe indicarse el motivo de cancelación: No-show, cancelación telefónica, etc.); RN-03 (la mesa asignada queda inmediatamente liberada) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente cancelar una reserva activa o confirmada registrando el motivo correspondiente (por ejemplo, No-show o solicitud directa), liberando la mesa asignada de forma inmediata.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".
- La reserva debe existir y no encontrarse en estado `Finalizada`.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/gerente/reservas/{id}/cancelar` con un JSON conteniendo `motivo` (`GerenteCancelarReservaDTO`).
2. La **Capa de Presentación** (`GerenteReservasController.CancelarReserva`) valida la autorización del rol `Gerente` (**RN-01**) y que el campo `motivo` no se encuentre vacío (data annotation `[Required]`).
3. La **Capa de Negocio** (`GerenteReservaService.CancelarReservaAsync`) verifica la existencia de la reserva, comprueba que no esté `Finalizada` y cambia su estado a `Cancelada`, guardando el motivo de cancelación (**RN-02**).
4. La Capa de Negocio libera la mesa asignada cambiándola a `Libre` (**RN-03**) y evalúa la lista de espera para posible reasignación automática o sugerida.
5. La **Capa de Persistencia** actualiza los cambios en la base de datos.
6. El Sistema devuelve un código **200 OK** confirmando la cancelación de la reserva y la liberación de la mesa.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Motivo de cancelación ausente (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON no contiene el campo `motivo` o este contiene solo espacios en blanco.
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** indicando: "El motivo de cancelación es obligatorio". Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el usuario no cuenta con rol `Gerente` (**RN-01**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Reserva no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID provisto no existe.
  2. La Capa de Negocio lanza una `ReservaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Reserva ya finalizada (HTTP 409 Conflict):**
  1. Si en el Paso 3 la reserva ya posee estado `Finalizada`.
  2. La Capa de Negocio lanza una `TransicionEstadoInvalidaException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "No se puede cancelar una reserva que ya ha sido finalizada". Fin del caso de uso.

* **5a. Fallo técnico en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre una falla de base de datos.
  2. El Sistema registra la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El gerente puede seleccionar motivos tipificados (ej. 'No-show / Cliente ausente', 'Cancelación telefónica', 'Problema operativo') o ingresar texto personalizado.
2. En todos los casos se invoca `PATCH /api/gerente/reservas/{id}/cancelar`.

### 6. POSTCONDICIONES
- La reserva queda registrada con estado `Cancelada` junto con el motivo y usuario responsable.
- La mesa queda disponible (`Libre`) para el salón.
- Si hay clientes en lista de espera compatibles, el sistema habilita su asignación.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Cancelación exitosa efectuada por el gerente. |
| `400` | Bad Request | Motivo de cancelación ausente o en blanco (RN-02). |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente (RN-01). |
| `404` | Not Found | Reserva no encontrada. |
| `409` | Conflict | La reserva ya se encuentra finalizada. |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Campo `motivo` requerido en `GerenteCancelarReservaDTO` y verificación de rol Gerente en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Gerente), RN-02 (registro de motivo y estado de reserva) y RN-03 (liberación inmediata de mesa) en `GerenteReservaService`.

### Matriz de trazabilidad CU-15 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `CancelarReservaAsync_WithValidReason_CancelsAndFreesTable` | `CancelarReservaGerente_Returns200OK` |
| 2a. Motivo faltante | `400 Bad Request` | `— (detectado por DataAnnotations en DTO)` | `CancelarReservaGerente_WithoutMotivo_Returns400BadRequest` |
| 2b. Sin rol Gerente | `403 Forbidden` | `— (middleware de autorización)` | `CancelarReservaGerente_AsCliente_Returns403Forbidden` |
| 3a. Reserva inexistente | `404 Not Found` | `CancelarReservaAsync_WhenNotFound_ThrowsReservaNoEncontradaException` | `CancelarReservaGerente_NonExistingId_Returns404NotFound` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
