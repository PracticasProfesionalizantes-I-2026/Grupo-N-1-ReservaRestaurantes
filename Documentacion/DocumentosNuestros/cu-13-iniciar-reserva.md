# Caso de Uso: Iniciar reserva

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo podrán iniciarse reservas en estado Confirmada); RN-02 (solo el usuario con rol Gerente podrá iniciar la reserva); RN-03 (al iniciar la reserva, la mesa asignada cambia su estado a Ocupada y la reserva a En curso) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-13 |
| **Nombre** | Iniciar reserva |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente / Recepción → marcar la presencia física de los comensales en el salón; Mozos → recibir la mesa como ocupada para iniciar el servicio; Cocina → coordinar comandas. |
| **Disparador (Trigger)** | El cliente se presenta en el restaurante y el gerente selecciona la acción "Iniciar" en el panel de reservas. |
| **Prioridad / Frecuencia** | Alta; alta frecuencia durante el servicio |
| **Reglas de negocio relacionadas** | RN-01 (solo podrán iniciarse reservas en estado Confirmada); RN-02 (solo el usuario con rol Gerente podrá iniciar la reserva); RN-03 (al iniciar la reserva, la mesa asignada cambia su estado a Ocupada y la reserva a En curso) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente registrar la llegada efectiva de los clientes al restaurante, cambiando el estado de la reserva a En curso y la mesa asignada a Ocupada.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".
- La reserva debe encontrarse previamente en estado `Confirmada`.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/reservas/{id}/iniciar` con su token JWT.
2. La **Capa de Presentación** (`ReservasController.IniciarReserva`) valida la autorización del rol `Gerente` (`[Authorize(Roles = "Gerente")]`) (**RN-02**).
3. La **Capa de Negocio** (`ReservaService.IniciarReservaAsync`) verifica la existencia de la reserva y constata que su estado actual sea `Confirmada` (**RN-01**).
4. La Capa de Negocio actualiza el estado de la reserva a `En curso`, registra la hora real de ingreso y cambia el estado de la mesa asignada a `Ocupada` (**RN-03**).
5. La **Capa de Persistencia** persiste los cambios de forma transaccional en las tablas `Reservas` y `Mesas`.
6. El Sistema devuelve un código **200 OK** con los datos de la reserva y la mesa actualizada.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token no cuenta con el rol `Gerente` (**RN-02**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Reserva no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID no corresponde a ninguna reserva registrada.
  2. La Capa de Negocio lanza una `ReservaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Reserva en estado inválido para iniciar (HTTP 409 Conflict):**
  1. Si en el Paso 3 la reserva no se encuentra en estado `Confirmada` (por ejemplo, está en `Pendiente`, `Cancelada` o ya `En curso`) (**RN-01**).
  2. La Capa de Negocio frena la ejecución y lanza una `TransicionEstadoInvalidaException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "Solo es posible iniciar reservas que se encuentren Confirmadas". Fin del caso de uso.

* **5a. Fallo técnico en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre un error en la base de datos durante la transacción.
  2. El Sistema registra la excepción técnica y revierte los cambios.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El gerente puede iniciar la reserva desde la tablet de recepción o desde la terminal de salón.
2. En ambos casos se invoca `PATCH /api/reservas/{id}/iniciar`.

### 6. POSTCONDICIONES
- La reserva pasa a estado `En curso` con la hora de inicio registrada.
- La mesa asociada pasa a estado `Ocupada` en el mapa del salón.
- Los mozos visualizan la mesa como ocupada para la toma de pedidos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Inicio exitoso de la reserva y mesa marcada como Ocupada. |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente (RN-02). |
| `404` | Not Found | Reserva no encontrada. |
| `409` | Conflict | La reserva no se encuentra en estado Confirmada (RN-01). |
| `500` | Internal Server Error | Error técnico transaccional en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de token y rol Gerente en la Capa de Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (estado actual Confirmada), RN-02 (autorización Gerente) y RN-03 (actualización sincronizada de Reserva a En curso y Mesa a Ocupada) en `ReservaService`.

### Matriz de trazabilidad CU-13 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `IniciarReservaAsync_WhenConfirmed_TransitionsToEnCursoAndTableOccupied` | `IniciarReserva_Returns200OK` |
| 2a. Sin rol Gerente | `403 Forbidden` | `— (middleware de autorización)` | `IniciarReserva_AsCliente_Returns403Forbidden` |
| 3a. Reserva inexistente | `404 Not Found` | `IniciarReservaAsync_WhenNotFound_ThrowsReservaNoEncontradaException` | `IniciarReserva_NonExistingId_Returns404NotFound` |
| 3b. Estado inválido | `409 Conflict` | `IniciarReservaAsync_WhenNotConfirmed_ThrowsTransicionEstadoInvalidaException` | `IniciarReserva_WhenPendiente_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
