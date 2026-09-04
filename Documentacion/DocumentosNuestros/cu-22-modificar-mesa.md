# Caso de Uso: Modificar mesa

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Admin podrá modificar mesas); RN-02 (no se podrán guardar datos inválidos, capacidad debe ser > 0); RN-03 (la modificación de capacidad o número no debe generar inconsistencias con reservas confirmadas activas) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-22 |
| **Nombre** | Modificar mesa |
| **Actor Principal** | Admin |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Administrador → ajustar capacidades físicas o renumerar mesas; Mozos / Gerente → disponer de la información física actualizada. |
| **Disparador (Trigger)** | El administrador selecciona una mesa existente y elige la opción "Modificar mesa". |
| **Prioridad / Frecuencia** | Media; baja frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (solo el Admin podrá modificar mesas); RN-02 (no se podrán guardar datos inválidos, capacidad debe ser > 0); RN-03 (la modificación de capacidad o número no debe generar inconsistencias con reservas confirmadas activas) |

---

### 1. BREVE DESCRIPCIÓN
Permite al administrador modificar los datos de una mesa existente (número, capacidad máxima o sector), validando la integridad con las reservas programadas.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Admin".
- La mesa a modificar debe existir previamente en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PUT /api/mesas/{id}` con un JSON conteniendo `numero`, `capacidad` y `sector` (`MesaUpdateDTO`).
2. La **Capa de Presentación** (`MesasController.ModificarMesa`) valida el rol `Admin` (**RN-01**) y que los campos requeridos cumplan las reglas de validación de esquema (**RN-02**).
3. La **Capa de Negocio** (`MesaService.ModificarMesaAsync`) verifica la existencia de la mesa, constata que el nuevo número no pertenezca a otra mesa y comprueba que la nueva capacidad no sea inferior a las personas de reservas futuras ya confirmadas para esa mesa (**RN-03**).
4. La Capa de Negocio actualiza los valores de la entidad `Mesa` y delega su guardado.
5. La **Capa de Persistencia** actualiza el registro en la tabla `Mesas`.
6. El Sistema devuelve un código **200 OK** con los datos actualizados de la mesa.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Datos inválidos en el cuerpo de la petición (HTTP 400 Bad Request):**
  1. Si en el Paso 2 la capacidad es menor a 1 o el número de mesa está vacío (**RN-02**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el detalle de los campos incorrectos. Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token no cuenta con rol `Admin` (**RN-01**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Mesa no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID no corresponde a ninguna mesa registrada.
  2. La Capa de Negocio lanza una `MesaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Inconsistencia con reservas futuras confirmadas (HTTP 409 Conflict):**
  1. Si en el Paso 3 se intenta reducir la capacidad a un valor menor a la cantidad de personas de reservas confirmadas activas para esa mesa (**RN-03**).
  2. La Capa de Negocio bloquea la modificación y lanza una `CapacidadIncompatibleReservaException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "No es posible reducir la capacidad de la mesa por debajo de comensales en reservas confirmadas asociadas". Fin del caso de uso.

* **5a. Error en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 se produce un fallo al actualizar en la base de datos.
  2. El Sistema registra la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. La modificación puede efectuarse desde la grilla de administración de mesas.
2. En todos los casos se invoca `PUT /api/mesas/{id}`.

### 6. POSTCONDICIONES
- Los datos de la mesa quedan actualizados de forma persistente en la tabla `Mesas`.
- El nuevo número o capacidad impacta de inmediato en las consultas de disponibilidad y mapa.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Modificación exitosa de la mesa. |
| `400` | Bad Request | Datos faltantes o capacidad menor o igual a cero (RN-02). |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Admin (RN-01). |
| `404` | Not Found | Mesa no encontrada con el ID provisto. |
| `409` | Conflict | Número duplicado o incompatibilidad con reservas confirmadas (RN-03). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Data annotations en `MesaUpdateDTO` y control de rol Admin en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Admin), RN-02 (datos válidos) y RN-03 (integridad con reservas confirmadas existentes) en `MesaService`.

### Matriz de trazabilidad CU-22 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `ModificarMesaAsync_WithValidData_UpdatesMesaSuccessfully` | `ModificarMesa_Returns200OK` |
| 2a. Capacidad inválida | `400 Bad Request` | `— (detectado por DTO)` | `ModificarMesa_WithInvalidCapacity_Returns400BadRequest` |
| 3a. Mesa inexistente | `404 Not Found` | `ModificarMesaAsync_WhenNotFound_ThrowsMesaNoEncontradaException` | `ModificarMesa_NonExistingId_Returns404NotFound` |
| 3b. Inconsistencia con reservas | `409 Conflict` | `ModificarMesaAsync_WhenCapacityBelowConfirmed_ThrowsCapacidadIncompatibleException` | `ModificarMesa_WhenCapacityBelowConfirmed_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
