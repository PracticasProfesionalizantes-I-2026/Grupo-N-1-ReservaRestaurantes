# Caso de Uso: Cambiar estado de mesas

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo rol Gerente); RN-02 (estados permitidos: Libre, Ocupada, Reservada, Fuera de servicio); RN-03 (prohibición de marcar Libre si existe reserva en curso) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-03 |
| **Nombre** | Cambiar estado de mesas |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → actualizar en tiempo real la disponibilidad física de las mesas; Mozos → conocer la disponibilidad inmediata para sentar clientes sin reserva; Clientes → recibir una mesa habilitada. |
| **Disparador (Trigger)** | El gerente selecciona una mesa en el mapa o listado operativo y elige la acción "Cambiar Estado". |
| **Prioridad / Frecuencia** | Alta; alta frecuencia durante el turno |
| **Reglas de negocio relacionadas** | RN-01 (solo rol Gerente); RN-02 (estados permitidos: Libre, Ocupada, Reservada, Fuera de servicio); RN-03 (prohibición de marcar Libre si existe reserva en curso) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente modificar manualmente el estado operativo de una mesa (Libre, Ocupada, Reservada, Fuera de servicio) para reflejar contingencias del salón o liberar/bloquear mesas manualmente.

### 2. PRECONDICIONES
- El sistema debe encontrarse operativo y con la base de datos accesible.
- El actor debe estar autenticado con rol "Gerente".
- La mesa a modificar debe existir previamente en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/mesas/{id}/estado` con un JSON conteniendo `nuevoEstado` (`MesaCambiarEstadoDTO`).
2. La **Capa de Presentación** (`MesasController.CambiarEstado`) valida que el ID de mesa sea un número entero positivo y que el estado enviado corresponda a un valor válido del enum (**RN-02**).
3. La **Capa de Negocio** (`MesaService.CambiarEstadoMesaAsync`) verifica la existencia de la mesa, la autorización del rol Gerente (**RN-01**) y valida que la mesa no posea una reserva en curso si se intenta pasar a `Libre` sin haber finalizado dicha reserva (**RN-03**).
4. La **Capa de Persistencia** actualiza el campo `Estado` en la entidad `Mesa` y guarda los cambios en la base de datos.
5. El Sistema devuelve un código **200 OK** con los datos actualizados de la mesa (ID, número, capacidad y nuevo estado).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Estado enviado no válido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el valor de `nuevoEstado` no corresponde a ninguno de los estados reconocidos por el sistema (**RN-02**).
  2. La Capa de Presentación rechaza la petición por error de enlace de modelo.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "Estado de mesa no válido". Fin del caso de uso.

* **3a. Mesa inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID de la mesa enviado no existe en la base de datos.
  2. La Capa de Negocio lanza una `MesaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found** con el mensaje: "La mesa especificada no existe". Fin del caso de uso.

* **3b. Intento de liberar mesa con reserva activa en curso (HTTP 409 Conflict):**
  1. Si en el Paso 3 el gerente intenta cambiar el estado a `Libre` pero la mesa tiene una reserva asociada en estado `En curso` (**RN-03**).
  2. La Capa de Negocio interrumpe la operación y lanza una `MesaConReservaActivaException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "No se puede marcar la mesa como Libre mientras tenga una reserva en curso activa". Fin del caso de uso.

* **4a. Fallo técnico en la persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 se produce un error técnico durante la persistencia en la base de datos.
  2. El Sistema captura la excepción no controlada.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El gerente puede cambiar el estado desde la vista de lista de mesas o interactuando directamente sobre el plano visual del salón.
2. Ambas acciones envían la misma petición a `PATCH /api/mesas/{id}/estado`.

### 6. POSTCONDICIONES
- El estado de la mesa queda actualizado de forma persistente en la tabla `Mesas`.
- El nuevo estado se refleja de inmediato en los paneles de disponibilidad y en el mapa del salón.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Actualización exitosa del estado operativo de la mesa. |
| `400` | Bad Request | Valor de estado no válido (RN-02) o ID no numérico. |
| `401` | Unauthorized | Falta de autenticación o token inválido. |
| `403` | Forbidden | Usuario sin privilegios de rol Gerente (RN-01). |
| `404` | Not Found | La mesa con el ID especificado no existe. |
| `409` | Conflict | Inconsistencia de negocio: mesa con reserva activa en curso (RN-03). |
| `500` | Internal Server Error | Error técnico en la capa de persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de ID positivo y estado válido en `MesaCambiarEstadoDTO`.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Gerente), verificación de existencia de la mesa y validación de regla RN-03 (integridad entre mesas y reservas activas en `MesaService`).

### Matriz de trazabilidad CU-03 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `CambiarEstadoMesaAsync_WithValidState_UpdatesMesaSuccessfully` | `CambiarEstadoMesa_Returns200OK` |
| 2a. Estado no válido | `400 Bad Request` | `— (capturado por validación de enum en DTO)` | `CambiarEstadoMesa_WithInvalidState_Returns400BadRequest` |
| 3a. Mesa inexistente | `404 Not Found` | `CambiarEstadoMesaAsync_WhenMesaNotFound_ThrowsMesaNoEncontradaException` | `CambiarEstadoMesa_NonExistingId_Returns404NotFound` |
| 3b. Reserva en curso activa | `409 Conflict` | `CambiarEstadoMesaAsync_WhenReservaEnCurso_ThrowsMesaConReservaActivaException` | `CambiarEstadoMesa_WhenReservaEnCurso_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
