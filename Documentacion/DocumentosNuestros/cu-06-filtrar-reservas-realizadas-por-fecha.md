# Caso de Uso: Filtrar reservas realizadas por fecha

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (el cliente solo podrá consultar sus propias reservas); RN-02 (la fecha debe poseer un formato válido) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-06 |
| **Nombre** | Filtrar reservas realizadas por fecha |
| **Actor Principal** | Cliente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Cliente → consultar el historial o reservas programadas para un día particular; Sistema → resguardar la privacidad garantizando que solo acceda a reservas propias. |
| **Disparador (Trigger)** | El cliente selecciona una fecha en el selector de filtro de la sección "Mis Reservas". |
| **Prioridad / Frecuencia** | Media; media frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (el cliente solo podrá consultar sus propias reservas); RN-02 (la fecha debe poseer un formato válido) |

---

### 1. BREVE DESCRIPCIÓN
Permite al cliente consultar el listado de reservas que ha realizado en una fecha determinada, visualizando estado, horario, cantidad de comensales y mesa asignada.

### 2. PRECONDICIONES
- El sistema debe encontrarse operativo con la base de datos accesible.
- El cliente debe poseer una sesión activa (Token JWT con rol Cliente).
- Debe existir al menos una reserva registrada a nombre del cliente en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `GET /api/cliente/reservas?fecha={fecha}`.
2. La **Capa de Presentación** (`ClienteReservasController.GetReservasPorFecha`) valida que el query parameter `fecha` tenga formato ISO 8601 válido (`YYYY-MM-DD`) (**RN-02**).
3. La **Capa de Negocio** (`ClienteReservaService.GetReservasPorFechaAsync`) extrae el `ClienteId` desde los claims del token JWT (**RN-01**) y consulta las reservas que coincidan con la fecha provista.
4. La **Capa de Persistencia** ejecuta la consulta filtrada en la tabla `Reservas` (`Where(r => r.ClienteId == clienteId && r.Fecha == fecha)`).
5. El Sistema devuelve un código **200 OK** con la lista de reservas encontradas para la fecha seleccionada.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Fecha con formato inválido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 la fecha enviada no cumple el formato requerido o no representa una fecha válida en el calendario (**RN-02**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "El formato de fecha provisto es inválido. Utilice AAAA-MM-DD". Fin del caso de uso.

* **3a. No existen reservas para la fecha especificada (HTTP 200 OK con lista vacía):**
  1. Si en el Paso 3 la búsqueda no arroja reservas asociadas a ese cliente en esa fecha.
  2. La Capa de Negocio genera una respuesta con lista vacía.
  3. El Sistema devuelve un código **200 OK** con un arreglo vacío `[]`. Fin del caso de uso.

* **4a. Fallo técnico en la consulta de persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 se produce un error en la conexión con la base de datos.
  2. El Sistema captura la excepción técnica no controlada.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El cliente puede consultar la información desde el navegador web o desde su dispositivo móvil.
2. En ambos casos se invoca el mismo endpoint con el token en el header `Authorization`.

### 6. POSTCONDICIONES
- El cliente visualiza el listado de reservas correspondientes a la fecha seleccionada.
- No se producen modificaciones en la base de datos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Consulta exitosa de reservas del cliente para la fecha solicitada. |
| `400` | Bad Request | Parámetro fecha faltante o con formato inválido (RN-02). |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `500` | Internal Server Error | Falla técnica en la capa de persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de sintaxis de fecha ISO 8601 en la Capa de Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (aislamiento estricto de datos por `ClienteId` del JWT) y filtro temporal en `ClienteReservaService`.

### Matriz de trazabilidad CU-06 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetReservasPorFechaAsync_ReturnsClientReservasForGivenDate` | `GetReservasPorFecha_Returns200WithReservasList` |
| 2a. Formato fecha inválido | `400 Bad Request` | `— (capturado en validación de query param)` | `GetReservasPorFecha_WithInvalidDate_Returns400BadRequest` |
| 3a. Sin reservas en esa fecha | `200 OK` | `GetReservasPorFechaAsync_WhenNoReservas_ReturnsEmptyList` | `GetReservasPorFecha_WhenNone_Returns200WithEmptyArray` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
