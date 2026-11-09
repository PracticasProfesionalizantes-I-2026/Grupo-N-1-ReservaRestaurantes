# Caso de Uso: Registrar

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (correo electrónico único en el sistema); RN-02 (datos obligatorios y contraseña segura); RN-03 (asignación inicial del rol Cliente) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-04 |
| **Nombre** | Registrar |
| **Actor Principal** | Usuario |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario nuevo → crear una cuenta para gestionar sus reservas; Restaurante → almacenar datos de contacto validados de los clientes. |
| **Disparador (Trigger)** | El usuario selecciona la opción "Registrarse" en la aplicación. |
| **Prioridad / Frecuencia** | Alta; media frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (correo electrónico único en el sistema); RN-02 (datos obligatorios y contraseña segura); RN-03 (asignación inicial del rol Cliente) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un nuevo usuario crear una cuenta en el sistema proporcionando su nombre, apellido, correo electrónico, teléfono y una contraseña segura, quedando habilitado con el rol Cliente.

### 2. PRECONDICIONES
- El sistema y la base de datos deben encontrarse operativos.
- El usuario no debe contar con una sesión activa en el dispositivo.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/auth/register` con un JSON conteniendo `nombre`, `apellido`, `email`, `telefono` y `password` (`UsuarioRegisterDTO`).
2. La **Capa de Presentación** (`AuthController.Register`) valida que los campos requeridos estén presentes, que el email cumpla con el formato válido y que la contraseña cumpla los requisitos de longitud y complejidad mínima (data annotations `[Required]`, `[EmailAddress]`, `[MinLength(8)]`).
3. La **Capa de Negocio** (`AuthService.RegisterAsync`) normaliza los datos (`Trim()`, `ToLower()` en email) y verifica que el correo electrónico no se encuentre ya registrado en la base de datos (**RN-01**).
4. La Capa de Negocio aplica hashing seguro (BCrypt / PBKDF2) sobre la contraseña (**RN-02**) y asigna por defecto el rol `Cliente` (**RN-03**).
5. La **Capa de Persistencia** inserta la nueva entidad `Usuario` en la tabla `Usuarios` asignándole un identificador único (GUID).
6. El Sistema devuelve un código **201 Created** con el ID del usuario, nombre y correo electrónico registrado.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Campos obligatorios faltantes o formato de email inválido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON recibido no incluye campos obligatorios o el email no respeta el formato estándar.
  2. La Capa de Presentación rechaza la petición por error de validación.
  3. El Sistema devuelve un código **400 Bad Request** detallando los campos observados. Fin del caso de uso.

* **2b. Contraseña no cumple requisitos de seguridad (HTTP 400 Bad Request):**
  1. Si en el Paso 2 la contraseña tiene menos de 8 caracteres o no combina caracteres alfanuméricos (**RN-02**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** indicando: "La contraseña debe contener al menos 8 caracteres y combinar letras y números". Fin del caso de uso.

* **2c. Formato de teléfono inválido o con letras (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el teléfono contiene letras, caracteres especiales no permitidos o no respeta la longitud válida (entre 7 y 25 caracteres).
  2. La Capa de Presentación y de Negocio rechazan la petición por error de validación.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje descriptivo: "El formato del teléfono es inválido. Solo se permiten números y los símbolos '+', '-', '()', y no se permiten letras." Fin del caso de uso.

* **3a. Correo electrónico ya registrado en el sistema (HTTP 409 Conflict):**
  1. Si en el Paso 3 la verificación determina que el correo electrónico ya existe en la base de datos, violando la regla **RN-01**.
  2. La Capa de Negocio frena la ejecución y lanza una `EmailDuplicadoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "Ya existe un usuario registrado con el correo electrónico provisto". Fin del caso de uso.

* **5a. Fallo técnico en la persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre una falla al intentar guardar el registro en la base de datos.
  2. El Sistema registra el error técnico en los logs del servidor.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El registro puede efectuarse desde la interfaz web o mediante clientes de prueba API.
2. En ambos casos se envía el payload al endpoint `POST /api/v1/auth/register` (o `POST /api/auth/register`).
3. De forma homóloga y para compatibilidad con la API de clientes, el alta puede realizarse a través de `POST /api/v1/Clientes` (`ClienteCreateDTO`) o `POST /api/v1/auth/register` (`UsuarioRegisterDTO`), ambos con rate limiting, hash BCrypt y asignación de rol `Cliente`.

### 6. POSTCONDICIONES
- Se crea un nuevo registro persistente en la tabla `Usuarios` con credenciales hasheadas y rol `Cliente`.
- El usuario queda habilitado para iniciar sesión mediante el caso de uso `CU-05`.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Registro exitoso del nuevo usuario en el sistema. |
| `400` | Bad Request | Datos obligatorios faltantes, formato de email inválido o contraseña débil (RN-02). |
| `409` | Conflict | El correo electrónico ya se encuentra registrado (RN-01). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Data annotations `[Required]`, `[EmailAddress]`, `[MinLength(6)]` y `[RegularExpression]` (formato numérico de teléfono sin letras) sobre los DTOs de registro (`ClienteCreateDTO`) en la Capa de Presentación.
- **Verificación (Negocio, → 400/404/409):** Validación de formato telefónico en `ClienteService`, comprobación de unicidad de email (**RN-01**), normalización de campos, hash de contraseña y asignación de rol Cliente (**RN-03**).

### Matriz de trazabilidad CU-04 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración / Presentación |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CreateAsync_ConTelefonoValido_CreaClienteExitosamente` | `ClienteCreateDTO_DataAnnotation_ConTelefonoValido_PasaValidacion` |
| 2a. Formato email inválido | `400 Bad Request` | `— (detectado por validación de DTO)` | `Validator.TryValidateObject` |
| 2b. Contraseña débil | `400 Bad Request` | `— (detectado por validación de DTO)` | `Validator.TryValidateObject` |
| 2c. Teléfono con letras | `400 Bad Request` | `CreateAsync_ConTelefonoConLetras_LanzaValidationException` | `ClienteCreateDTO_DataAnnotation_ConTelefonoConLetras_FallaValidacion` |
| 3a. Email duplicado | `409 Conflict` | `CreateAsync_WhenEmailExists_ThrowsConflictException` | `POST /api/v1/Clientes` con email existente |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
