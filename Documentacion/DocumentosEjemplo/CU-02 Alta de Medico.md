# Caso de Uso: Alta de Médico

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-04 (matrícula única) y RN-05 (normalización de espacios y límites
> de longitud) **implementadas** en el código; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-02 |
| **Nombre** | Alta de Médico |
| **Actor Principal** | Administrador del Sistema (o usuario autorizado con rol administrativo) |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Administración → registrar al profesional y tener su matrícula verificada; Médico → quedar habilitado para recibir turnos; Pacientes → poder elegirlo en la reserva de turnos |
| **Disparador (Trigger)** | La administración recibe la solicitud de alta de un nuevo profesional con sus datos (nombre, especialidad y matrícula) |
| **Prioridad / Frecuencia** | Alta; baja frecuencia (altas ocasionales de personal) |
| **Reglas de negocio relacionadas** | RN-04 (matrícula única por médico); RN-05 (normalización de campos) |

---

### 1. BREVE DESCRIPCIÓN
Permite registrar a un nuevo médico en el sistema con sus datos de identificación
profesional (nombre, especialidad y matrícula) para que quede disponible en la agenda de
turnos.

### 2. PRECONDICIONES
1. El sistema debe estar en funcionamiento y con la Capa de Persistencia accesible
   (base de datos inicializada por `DbInitializer`).
2. El actor debe poseer un estado de autenticación activo (Token JWT válido) con
   permisos de escritura sobre el recurso Médicos.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201)
1. El Actor envía una petición al endpoint `POST /api/medicos` con un JSON que contiene
   los datos del médico (`nombre`, `especialidad`, `matricula`).
2. La **Capa de Presentación** (`MedicosController.CreateMedico`) valida que el JSON sea
   estructuralmente correcto y que los campos requeridos estén presentes y no vacíos
   (data annotations `[Required]` sobre `MedicoCreateDTO`).
3. La **Capa de Negocio** (`MedicoService.CreateMedicoAsync`) construye la entidad
   `Medico` y delega su persistencia.
4. La **Capa de Persistencia** genera un nuevo `Id` (GUID) y guarda el registro en la
   tabla `Medicos`.
5. El Sistema devuelve un código **201 Created** con la información resultante (ID,
   nombre, especialidad y matrícula del médico creado).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. JSON inválido o ilegible (HTTP 400 Bad Request):**
  1. Si en el Paso 1 el cuerpo de la petición no es un JSON válido (sintaxis rota,
     cuerpo vacío o con formato incorrecto).
  2. El Sistema (Capa de Presentación / model binding) rechaza la petición por error de
     esquema.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Dato obligatorio faltante (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON no incluye `nombre`, `especialidad` o `matricula`
     (cualquiera de los tres).
  2. El Sistema (Capa de Presentación) rechaza la petición por error de validación
     (`ModelState.IsValid == false`).
  3. El Sistema devuelve un código **400 Bad Request** detallando el campo faltante con
     su mensaje, ej: `"El nombre es obligatorio."`. Fin del caso de uso.

* **2b. Campo con cadena vacía (HTTP 400 Bad Request):**
  1. Si en el Paso 2 un campo obligatorio llega como cadena de longitud cero (`""`),
     borde que `[Required]` sí detecta (no permite strings vacíos por defecto).
  2. El Sistema (Capa de Presentación) rechaza la petición por validación de esquema.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje del campo
     correspondiente. Fin del caso de uso.

* **2c. Campo con solo espacios en blanco (HTTP 400 Bad Request):**
  1. Si en el Paso 2 un campo llega con únicamente espacios (ej. `"   "`): la regla
     **RN-05** normaliza los valores aplicando `Trim()`; si el valor resultante queda
     vacío, el Sistema rechaza la petición.
  2. El Sistema (Capa de Negocio) lanza una `ValidationException`.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje del campo
     correspondiente. Fin del caso de uso.

* **2d. Longitud excesiva de un campo (HTTP 400 Bad Request):**
  1. Si en el Paso 2 algún campo supera la longitud máxima definida en el DTO
     (`[MaxLength]`: nombre y especialidad 200, matrícula 50).
  2. El Sistema (Capa de Presentación) rechaza la petición por validación de esquema
     (`ModelState.IsValid == false`).
  3. El Sistema devuelve un código **400 Bad Request** indicando el límite excedido.
     Fin del caso de uso.

* **3a. Matrícula duplicada (HTTP 409 Conflict):**
  1. Si en el Paso 3 la verificación determina que ya existe un médico registrado con la
     misma matrícula, violando la regla de negocio **RN-04** (matrícula única).
  2. El Sistema frena la ejecución en la **Capa de Negocio** y lanza la excepción de
     dominio `MatriculaDuplicadaException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "Ya existe un médico
     con la matrícula {matricula}". Fin del caso de uso.

* **4a. Error interno en la persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 la **Capa de Persistencia** no puede guardar el registro (ej. falla
     de conexión, corrupción de la base de datos).
  2. El Sistema interrumpe la operación y registra el error como no controlado.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El actor puede enviar el JSON desde el panel web, desde la colección de Bruno
   (`POST Create Medico.bru`) o desde un cliente HTTP (Postman, Swagger/Scalar).
2. En todas las variantes el esquema del cuerpo y el resultado (`201 Created`) son
   idénticos.

### 6. POSTCONDICIONES
1. Se ha creado un nuevo registro persistente en la tabla `Medicos` con ID único (GUID).
2. El nuevo médico aparece en `GET /api/medicos` y queda **disponible para recibir
   turnos** (visible en la selección de profesionales al reservar un turno).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Confirmación de persistencia exitosa del nuevo recurso Médico. |
| `400` | Bad Request | Fallo en la validación de esquema o sintaxis del JSON recibido (campos faltantes, vacíos, inválidos). |
| `409` | Conflict | Violación de invariantes de negocio (RN-04: matrícula duplicada). |
| `500` | Internal Server Error | Error técnico no controlado durante la persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** campos obligatorios, cadena vacía y límites de
  longitud (`[Required]`/`[MaxLength]` sobre `MedicoCreateDTO` + `ModelState.IsValid` en
  el controller), y formato del JSON (model binding).
- **Verificación (Negocio, → 400/409):** RN-05 normalización `Trim()` y detección de
  campos que quedan vacíos **solo con espacios** (`ValidationException` → 400); RN-04
  matrícula única revisando `ExistsByMatriculaAsync` antes de persistir
  (`MatriculaDuplicadaException` → 409). El negocio funciona como *defensa en profundidad*:
  aunque la Presentación ya rechace los casos 2a/2b, el service los vuelve a verificar.

### Matriz de trazabilidad CU-02 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CreateMedicoAsync_SavesAndReturnsCreatedMedico` / `CreateMedicoAsync_WithValidData_TrimsAndSavesMedico` | `CreateMedico_ReturnsSuccessAndCreatedMedico` |
| 1a. JSON inválido | `400 Bad Request` | — (model binding, no pasa por Negocio) | `CreateMedico_WithInvalidJson_Returns400BadRequest` |
| 2a. Dato obligatorio faltante | `400 Bad Request` | — (se detecta vía `[Required]` en Presentación) | `CreateMedico_WithMissingRequiredField_Returns400BadRequest` |
| 2b. Campo con cadena vacía | `400 Bad Request` | `CreateMedicoAsync_WithEmptyRequiredField_ThrowsValidationException` | *(cubierto por `CreateMedico_WithMissingRequiredField_Returns400BadRequest`)* |
| 2c. Campo con solo espacios | `400 Bad Request` | `CreateMedicoAsync_WithWhitespaceOnlyField_ThrowsValidationException` | `CreateMedico_WithWhitespaceOnlyField_Returns400BadRequest` |
| 2d. Longitud excesiva | `400 Bad Request` | — (validación de esquema DTO) | `CreateMedico_WithOversizedField_Returns400BadRequest` |
| 3a. Matrícula duplicada | `409 Conflict` | `CreateMedicoAsync_WhenMatriculaAlreadyExists_ThrowsMatriculaDuplicadaException` | `CreateMedico_WhenDuplicateMatricula_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación (1a, 2a, 2d) el test aplicable es el de integración
> HTTP, ya que el service no se invoca. Los tests se ejecutan con
> `dotnet test Clinica-San-Salud.slnx`.