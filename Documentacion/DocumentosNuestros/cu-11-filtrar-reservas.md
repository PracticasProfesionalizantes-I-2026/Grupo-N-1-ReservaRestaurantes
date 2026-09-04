# Caso de Uso: Filtrar reservas

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Gerente podrá utilizar este filtro operativo integral); RN-02 (los criterios ingresados deben ser válidos y las fechas en formato correcto) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-11 |
| **Nombre** | Filtrar reservas |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → organizar y consultar reservas del día o turnos futuros por estado o cliente; Recepción → verificar rápidamente las reservas programadas. |
| **Disparador (Trigger)** | El gerente introduce criterios de búsqueda (fecha, estado, cliente) en el listado general de reservas. |
| **Prioridad / Frecuencia** | Alta; alta frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (solo el Gerente podrá utilizar este filtro operativo integral); RN-02 (los criterios ingresados deben ser válidos y las fechas en formato correcto) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente consultar y filtrar todas las reservas del sistema combinando criterios de fecha, estado operativo (Pendiente, Confirmada, En curso, Finalizada, Cancelada) y nombre del cliente.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `GET /api/reservas?fecha={f}&estado={e}&cliente={c}` con su token JWT.
2. La **Capa de Presentación** (`ReservasController.GetReservasFiltradas`) valida que el usuario posea rol `Gerente` (**RN-01**) y que los parámetros de formato de fecha y estado sean válidos (**RN-02**).
3. La **Capa de Negocio** (`ReservaService.GetReservasFiltradasAsync`) construye la consulta dinámica combinando los filtros provistos.
4. La **Capa de Persistencia** ejecuta la consulta en la tabla `Reservas` incluyendo los datos de la mesa y el cliente.
5. El Sistema devuelve un código **200 OK** con la lista de reservas que coinciden con los criterios de búsqueda.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Criterio de búsqueda inválido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el formato de fecha no es válido o el estado provisto no existe (**RN-02**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** indicando el parámetro inválido. Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el usuario no cuenta con rol `Gerente` (**RN-01**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. No se encuentran reservas (HTTP 200 OK con colección vacía):**
  1. Si en el Paso 3 no existen registros que cumplan con la combinación de filtros solicitada.
  2. La Capa de Negocio retorna una lista vacía.
  3. El Sistema devuelve un código **200 OK** con un arreglo vacío `[]`. Fin del caso de uso.

* **4a. Error en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 se produce un error en la base de datos.
  2. El Sistema registra la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El gerente puede filtrar aplicando uno, varios o todos los parámetros simultáneamente.
2. Los resultados soportan paginación mediante query params (`page`, `pageSize`).

### 6. POSTCONDICIONES
- El gerente visualiza el listado de reservas que satisfacen los criterios aplicados.
- No se altera el estado de la base de datos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Recuperación exitosa del listado filtrado de reservas. |
| `400` | Bad Request | Criterios de filtro con formato inválido (RN-02). |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente (RN-01). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de rol Gerente y formato de parámetros de consulta en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Gerente), RN-02 (validación de criterios y consulta dinámica) en `ReservaService`.

### Matriz de trazabilidad CU-11 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetReservasFiltradasAsync_WithFilters_ReturnsMatchingReservas` | `GetReservasFiltradas_Returns200OK` |
| 2a. Filtro inválido | `400 Bad Request` | `— (validado en binding de query params)` | `GetReservasFiltradas_WithBadParams_Returns400BadRequest` |
| 2b. Sin rol Gerente | `403 Forbidden` | `— (middleware de autorización)` | `GetReservasFiltradas_AsCliente_Returns403Forbidden` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
