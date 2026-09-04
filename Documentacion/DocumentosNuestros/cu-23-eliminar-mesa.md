# Caso de Uso: Eliminar mesa

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Admin podrá eliminar mesas); RN-02 (no se podrá eliminar una mesa que tenga una reserva activa o asignación futura que genere inconsistencia); RN-03 (la eliminación debe requerir confirmación explícita) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-23 |
| **Nombre** | Eliminar mesa |
| **Actor Principal** | Admin |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Administrador → retirar mobiliario en desuso o reestructurar el salón; Sistema → salvaguardar que no se cancelen o rompan reservas activas. |
| **Disparador (Trigger)** | El administrador selecciona una mesa y presiona la opción "Eliminar mesa". |
| **Prioridad / Frecuencia** | Media; baja frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (solo el Admin podrá eliminar mesas); RN-02 (no se podrá eliminar una mesa que tenga una reserva activa o asignación futura que genere inconsistencia); RN-03 (la eliminación debe requerir confirmación explícita) |

---

### 1. BREVE DESCRIPCIÓN
Permite al administrador eliminar una mesa del sistema cuando esta no posee reservas activas ni compromisos futuros, requiriendo confirmación previa para garantizar la integridad operativa.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Admin".
- La mesa a eliminar debe existir en el sistema.
- La mesa no debe poseer reservas activas (Confirmadas o En curso) asociadas.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `DELETE /api/mesas/{id}` con confirmación en el cuerpo o cabecera (`{ "confirmado": true }`).
2. La **Capa de Presentación** (`MesasController.EliminarMesa`) valida que el usuario posea rol `Admin` (**RN-01**) y que el flag de confirmación esté presente (**RN-03**).
3. La **Capa de Negocio** (`MesaService.EliminarMesaAsync`) comprueba la existencia de la mesa y verifica que no tenga reservas asociadas en estado `En curso`, `Confirmada` o futuras (**RN-02**).
4. La Capa de Negocio ejecuta la eliminación física si no tiene historial o baja lógica (`IsDeleted = true`) si posee historial histórico pasado.
5. La **Capa de Persistencia** actualiza la base de datos.
6. El Sistema devuelve un código **200 OK** confirmando la eliminación de la mesa.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Falta de confirmación explícita (HTTP 400 Bad Request):**
  1. Si en el Paso 2 no se incluye la confirmación explícita requerida (**RN-03**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "Se requiere confirmación para proceder con la eliminación de la mesa". Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token no cuenta con rol `Admin` (**RN-01**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Mesa no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID no existe en la base de datos.
  2. La Capa de Negocio lanza una `MesaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Mesa con reservas activas o futuras asociadas (HTTP 409 Conflict):**
  1. Si en el Paso 3 la verificación detecta que la mesa tiene reservas en estado `Confirmada`, `En curso` o asignaciones programadas (**RN-02**).
  2. La Capa de Negocio interrumpe la eliminación y lanza una `MesaConReservasActivasException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "No es posible eliminar la mesa porque posee reservas activas o futuras asignadas. Reasigne las reservas antes de eliminar". Fin del caso de uso.

* **5a. Fallo técnico en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre un error en la base de datos.
  2. El Sistema registra la excepción no controlada.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El administrador puede ejecutar la eliminación desde la tabla de mesas o desde el mapa del salón tras confirmar en el modal de alerta.
2. En ambos casos se envía `DELETE /api/mesas/{id}`.

### 6. POSTCONDICIONES
- La mesa queda eliminada o dada de baja del sistema.
- La mesa deja de aparecer en el mapa del salón y en los motores de asignación de reservas.
- No se alteran reservas activas (**RN-02**).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Mesa eliminada exitosamente del sistema. |
| `400` | Bad Request | Falta de confirmación explícita (RN-03) o ID no numérico. |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Admin (RN-01). |
| `404` | Not Found | Mesa no encontrada con el ID provisto. |
| `409` | Conflict | La mesa posee reservas activas o futuras asignadas (RN-02). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de flag de confirmación y autorización de rol Admin en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Admin), RN-02 (ausencia de reservas activas o futuras vinculadas) y RN-03 (confirmación requerida) en `MesaService`.

### Matriz de trazabilidad CU-23 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `EliminarMesaAsync_WithoutActiveReservas_DeletesSuccessfully` | `EliminarMesa_Returns200OK` |
| 2a. Sin confirmación | `400 Bad Request` | `— (validado en controlador)` | `EliminarMesa_WithoutConfirmation_Returns400BadRequest` |
| 2b. Sin rol Admin | `403 Forbidden` | `— (middleware de autorización)` | `EliminarMesa_AsGerente_Returns403Forbidden` |
| 3a. Mesa inexistente | `404 Not Found` | `EliminarMesaAsync_WhenNotFound_ThrowsMesaNoEncontradaException` | `EliminarMesa_NonExistingId_Returns404NotFound` |
| 3b. Con reservas activas | `409 Conflict` | `EliminarMesaAsync_WhenHasActiveReservas_ThrowsMesaConReservasActivasException` | `EliminarMesa_WhenHasActiveReservas_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
