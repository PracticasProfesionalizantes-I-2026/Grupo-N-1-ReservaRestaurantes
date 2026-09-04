# Caso de Uso: Filtrar mapa de mesas

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo usuarios con rol Gerente o Admin podrán utilizar este filtro); RN-02 (los criterios de estado ingresados deben corresponder a estados válidos: Libre, Ocupada, Reservada, Fuera de servicio) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-10 |
| **Nombre** | Filtrar mapa de mesas |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → localizar rápidamente mesas libres, ocupadas o por capacidad en el salón; Mozos → ubicar mesas específicas asignadas. |
| **Disparador (Trigger)** | El gerente aplica criterios de filtro (estado, capacidad mínima o sector) en el mapa de mesas. |
| **Prioridad / Frecuencia** | Alta; alta frecuencia operativa |
| **Reglas de negocio relacionadas** | RN-01 (solo usuarios con rol Gerente o Admin podrán utilizar este filtro); RN-02 (los criterios de estado ingresados deben corresponder a estados válidos: Libre, Ocupada, Reservada, Fuera de servicio) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente filtrar y visualizar las mesas del salón según su estado operativo y capacidad física para agilizar la asignación en tiempo real.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con las mesas configuradas en la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `GET /api/mesas?estado={estado}&capacidadMin={cap}` con su token JWT.
2. La **Capa de Presentación** (`MesasController.GetMesasFiltradas`) valida que el usuario posea rol `Gerente` (`[Authorize(Roles = "Gerente,Admin")]`) (**RN-01**) y que el estado consultado pertenezca al conjunto de estados válidos (**RN-02**).
3. La **Capa de Negocio** (`MesaService.GetMesasFiltradasAsync`) construye la consulta aplicando los filtros solicitados sobre la tabla de mesas.
4. La **Capa de Persistencia** ejecuta la consulta en la base de datos recuperando las entidades que coinciden con los criterios.
5. El Sistema devuelve un código **200 OK** con la lista de mesas filtradas y su estado actual.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Criterio de estado inválido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el valor enviado en `estado` no corresponde a un estado permitido (**RN-02**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "Estado de mesa no reconocido". Fin del caso de uso.

* **2b. Usuario sin privilegios de rol (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token no contiene el rol `Gerente` o `Admin` (**RN-01**).
  2. La Capa de Presentación deniega el acceso.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. No hay mesas que cumplan los criterios (HTTP 200 OK con lista vacía):**
  1. Si en el Paso 3 ninguna mesa coincide con los filtros aplicados.
  2. La Capa de Negocio retorna una colección vacía.
  3. El Sistema devuelve un código **200 OK** con un arreglo vacío `[]`. Fin del caso de uso.

* **4a. Error técnico en la base de datos (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 se produce un error en la conexión de base de datos.
  2. El Sistema registra el fallo técnico.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El filtro puede aplicarse visualmente en la representación gráfica del mapa del restaurante o en el listado tabular de mesas.
2. En ambos casos se consumen los mismos parámetros en `GET /api/mesas`.

### 6. POSTCONDICIONES
- El gerente visualiza el subconjunto de mesas que cumplen los criterios solicitados.
- No se modifican los registros de mesas en la base de datos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Consulta de mesas filtradas ejecutada con éxito. |
| `400` | Bad Request | Valor de filtro de estado inválido (RN-02). |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente o Admin (RN-01). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de autorización por rol y validación de tipos de datos en query parameters.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Gerente/Admin), RN-02 (estados de mesa válidos) y armado de filtros en `MesaService`.

### Matriz de trazabilidad CU-10 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetMesasFiltradasAsync_WithValidFilters_ReturnsFilteredMesas` | `GetMesas_WithValidFilters_Returns200OK` |
| 2a. Estado inválido | `400 Bad Request` | `— (capturado en validación de enum query parameter)` | `GetMesas_WithInvalidState_Returns400BadRequest` |
| 2b. Sin rol Gerente | `403 Forbidden` | `— (middleware de autorización ASP.NET Core)` | `GetMesas_AsCliente_Returns403Forbidden` |
| 3a. Sin coincidencias | `200 OK` | `GetMesasFiltradasAsync_WhenNoMatch_ReturnsEmptyList` | `GetMesas_WhenNoMatch_Returns200WithEmptyArray` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
