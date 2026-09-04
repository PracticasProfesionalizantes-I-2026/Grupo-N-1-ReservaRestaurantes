# Caso de Uso: Crear mesa

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Admin podrá crear mesas); RN-02 (los datos obligatorios número y capacidad > 0 deben ser válidos); RN-03 (cada mesa debe poseer un número o identificador único en el establecimiento) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-21 |
| **Nombre** | Crear mesa |
| **Actor Principal** | Admin |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Administrador → registrar la distribución y capacidad de mobiliario del restaurante; Gerente y Mozos → disponer de mesas para asignación en el salón. |
| **Disparador (Trigger)** | El administrador selecciona la opción "Crear mesa" en la configuración de mesas. |
| **Prioridad / Frecuencia** | Media; baja frecuencia (altas ocasionales o rediseño del salón) |
| **Reglas de negocio relacionadas** | RN-01 (solo el Admin podrá crear mesas); RN-02 (los datos obligatorios número y capacidad > 0 deben ser válidos); RN-03 (cada mesa debe poseer un número o identificador único en el establecimiento) |

---

### 1. BREVE DESCRIPCIÓN
Permite al administrador dar de alta una nueva mesa en el plano del restaurante indicando su número identificador, capacidad máxima de comensales y sector (interior, terraza, etc.).

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Admin".

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/mesas` con un JSON conteniendo `numero`, `capacidad` y `sector` (`MesaCreateDTO`).
2. La **Capa de Presentación** (`MesasController.CrearMesa`) valida que el usuario posea rol `Admin` (**RN-01**) y que los campos requeridos estén presentes y sean válidos (`[Required]`, `[Range(1, 30)]` sobre capacidad) (**RN-02**).
3. La **Capa de Negocio** (`MesaService.CrearMesaAsync`) normaliza los datos y verifica que el número de mesa no se encuentre ya en uso (**RN-03**).
4. La Capa de Negocio inicializa el estado de la nueva entidad `Mesa` como `Libre` y delega su persistencia.
5. La **Capa de Persistencia** guarda la entidad `Mesa` en la base de datos generando su identificador único.
6. El Sistema devuelve un código **201 Created** con los datos de la mesa creada (ID, número, capacidad, sector y estado).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Datos obligatorios faltantes o capacidad inválida (HTTP 400 Bad Request):**
  1. Si en el Paso 2 falta el número de mesa o la capacidad es menor o igual a cero (**RN-02**).
  2. La Capa de Presentación rechaza la petición por error de validación.
  3. El Sistema devuelve un código **400 Bad Request** detallando el error. Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token no cuenta con rol `Admin` (**RN-01**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Número de mesa duplicado (HTTP 409 Conflict):**
  1. Si en el Paso 3 ya existe una mesa registrada con el mismo número o identificador (**RN-03**).
  2. La Capa de Negocio lanza una `NumeroMesaDuplicadoException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "Ya existe una mesa con el número provisto". Fin del caso de uso.

* **5a. Error en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre una falla al insertar el registro en la base de datos.
  2. El Sistema registra la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El administrador puede crear mesas desde el formulario de configuración o mediante un editor de layout visual.
2. En ambos casos se invoca `POST /api/mesas`.

### 6. POSTCONDICIONES
- Se registra una nueva entidad `Mesa` con estado `Libre` en la tabla `Mesas`.
- La mesa queda disponible para reservas y asignaciones en el mapa operativo.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Creación exitosa de la nueva mesa. |
| `400` | Bad Request | Datos faltantes o capacidad menor o igual a cero (RN-02). |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Admin (RN-01). |
| `409` | Conflict | Número de mesa ya existente en el restaurante (RN-03). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Data annotations `[Required]`, `[Range(1, 30)]` sobre `MesaCreateDTO` y verificación de rol Admin en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Admin), RN-02 (datos obligatorios y capacidad positiva) y RN-03 (unicidad de número de mesa con `ExistsByNumeroAsync`) en `MesaService`.

### Matriz de trazabilidad CU-21 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CrearMesaAsync_WithValidData_CreatesMesaWithLibreState` | `CrearMesa_Returns201Created` |
| 2a. Capacidad inválida | `400 Bad Request` | `— (detectado por validación de DTO)` | `CrearMesa_WithZeroCapacity_Returns400BadRequest` |
| 2b. Sin rol Admin | `403 Forbidden` | `— (middleware de autorización)` | `CrearMesa_AsGerente_Returns403Forbidden` |
| 3a. Número duplicado | `409 Conflict` | `CrearMesaAsync_WhenNumeroExists_ThrowsNumeroMesaDuplicadoException` | `CrearMesa_DuplicateNumero_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
