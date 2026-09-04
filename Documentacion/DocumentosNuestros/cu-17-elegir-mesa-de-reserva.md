# Caso de Uso: Elegir mesa de reserva

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (la mesa debe tener capacidad suficiente para la cantidad de personas de la reserva); RN-02 (la mesa debe encontrarse libre en la franja horaria solicitada sin solapamientos); RN-03 (solo el usuario con rol Gerente puede asignar o reasignar mesas) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-17 |
| **Nombre** | Elegir mesa de reserva |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → seleccionar o reasignar manualmente la mesa óptima para una reserva; Clientes → contar con una ubicación adecuada para su grupo de comensales. |
| **Disparador (Trigger)** | El gerente selecciona la opción "Asignar / Cambiar Mesa" sobre una reserva en el panel operativo. |
| **Prioridad / Frecuencia** | Alta; alta frecuencia durante la organización de turnos |
| **Reglas de negocio relacionadas** | RN-01 (la mesa debe tener capacidad suficiente para la cantidad de personas de la reserva); RN-02 (la mesa debe encontrarse libre en la franja horaria solicitada sin solapamientos); RN-03 (solo el usuario con rol Gerente puede asignar o reasignar mesas) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente asignar o cambiar manualmente la mesa asignada a una reserva, validando que la nueva mesa disponga de capacidad suficiente y esté libre en ese turno.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".
- La reserva debe existir y no encontrarse finalizada ni cancelada.
- La mesa seleccionada debe existir en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/reservas/{id}/asignar-mesa` con un JSON conteniendo `mesaId` (`ReservaAsignarMesaDTO`).
2. La **Capa de Presentación** (`ReservasController.AsignarMesa`) valida la autorización del rol `Gerente` (**RN-03**) y que `mesaId` sea un número entero válido mayor a cero.
3. La **Capa de Negocio** (`ReservaService.AsignarMesaAsync`) comprueba la existencia de la reserva y de la mesa seleccionada.
4. La Capa de Negocio verifica que la capacidad máxima de la mesa sea mayor o igual a la cantidad de personas de la reserva (**RN-01**) y que la mesa no posea otra reserva activa en ese horario (**RN-02**).
5. La **Capa de Persistencia** actualiza la clave foránea `MesaId` en la entidad `Reserva` y guarda los cambios en la base de datos.
6. El Sistema devuelve un código **200 OK** con los datos de la reserva y la nueva mesa asignada.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. ID de mesa inválido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON no contiene `mesaId` o es menor o igual a cero.
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "El ID de mesa es obligatorio y debe ser mayor a cero". Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el usuario no cuenta con rol `Gerente` (**RN-03**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Reserva o mesa no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 3 no se encuentra la reserva o la mesa especificada.
  2. La Capa de Negocio lanza una `EntidadNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found** detallando la entidad inexistente. Fin del caso de uso.

* **4a. Capacidad de mesa insuficiente (HTTP 409 Conflict):**
  1. Si en el Paso 4 la capacidad de la mesa es menor a la cantidad de comensales de la reserva (**RN-01**).
  2. La Capa de Negocio lanza una `CapacidadInsuficienteException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "La mesa seleccionada no cuenta con capacidad suficiente para la reserva". Fin del caso de uso.

* **4b. Mesa con solapamiento horario (HTTP 409 Conflict):**
  1. Si en el Paso 4 la mesa ya se encuentra reservada u ocupada por otra reserva en la misma fecha y franja horaria (**RN-02**).
  2. La Capa de Negocio lanza una `MesaSolapadaException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "La mesa seleccionada ya se encuentra comprometida en ese horario". Fin del caso de uso.

* **5a. Fallo técnico en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 se produce un fallo al actualizar la base de datos.
  2. El Sistema registra la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El gerente puede seleccionar la mesa desde un desplegable en el formulario de la reserva o haciendo clic sobre la mesa en el mapa visual interactivo.
2. En ambos casos se invoca `PATCH /api/reservas/{id}/asignar-mesa`.

### 6. POSTCONDICIONES
- La reserva queda vinculada a la nueva mesa en la base de datos.
- El mapa operativo y las consultas de turnos reflejan la nueva asignación.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Asignación exitosa de mesa a la reserva. |
| `400` | Bad Request | ID de mesa ausente o con formato inválido. |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente (RN-03). |
| `404` | Not Found | Reserva o mesa no encontrada. |
| `409` | Conflict | Capacidad insuficiente (RN-01) o solapamiento de horarios (RN-02). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de ID positivo en `ReservaAsignarMesaDTO` y rol Gerente en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (capacidad suficiente de mesa), RN-02 (disponibilidad horaria sin solapamientos) y RN-03 (rol Gerente) en `ReservaService`.

### Matriz de trazabilidad CU-17 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `AsignarMesaAsync_WithValidTableAndCapacity_AssignsSuccessfully` | `AsignarMesa_Returns200OK` |
| 2a. ID mesa inválido | `400 Bad Request` | `— (detectado por validación de DTO)` | `AsignarMesa_WithInvalidTableId_Returns400BadRequest` |
| 3a. Mesa no encontrada | `404 Not Found` | `AsignarMesaAsync_WhenTableNotFound_ThrowsEntidadNoEncontradaException` | `AsignarMesa_NonExistingTable_Returns404NotFound` |
| 4a. Capacidad insuficiente | `409 Conflict` | `AsignarMesaAsync_WhenCapacityInsufficient_ThrowsCapacidadInsuficienteException` | `AsignarMesa_WhenInsufficientCapacity_Returns409Conflict` |
| 4b. Solapamiento de horario | `409 Conflict` | `AsignarMesaAsync_WhenTableOverlaps_ThrowsMesaSolapadaException` | `AsignarMesa_WhenOverlappingSchedule_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
