# Caso de Uso: Iniciar sesión

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo usuarios registrados y activos); RN-02 (coincidencia de credenciales email y contraseña hash); RN-03 (generación de token JWT con claims del rol asignado) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-05 |
| **Nombre** | Iniciar sesión |
| **Actor Principal** | Usuario |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario (Cliente, Gerente, Admin) → acceder a sus funciones y datos protegidos; Sistema → validar la identidad y emitir credenciales de sesión seguras. |
| **Disparador (Trigger)** | El usuario ingresa sus credenciales en la pantalla de acceso y presiona "Iniciar Sesión". |
| **Prioridad / Frecuencia** | Alta; muy alta frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (solo usuarios registrados y activos); RN-02 (coincidencia de credenciales email y contraseña hash); RN-03 (generación de token JWT con claims del rol asignado) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un usuario autenticarse en el sistema mediante su correo electrónico y contraseña, obteniendo un token JWT con sus claims y permisos para operar.

### 2. PRECONDICIONES
- El sistema y la base de datos se encuentran operativos.
- El usuario debe poseer una cuenta previamente registrada en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `POST /api/auth/login` con un JSON conteniendo `email` y `password` (`LoginRequestDTO`).
2. La **Capa de Presentación** (`AuthController.Login`) valida que los campos no vengan vacíos ni con formatos inválidos.
3. La **Capa de Negocio** (`AuthService.LoginAsync`) busca al usuario por email (**RN-01**) y verifica la contraseña utilizando el comparador criptográfico de hash (**RN-02**).
4. La Capa de Negocio genera un Token JWT firmado con la clave del servidor, incorporando claims de `UserId`, `Email` y `Role` (**RN-03**).
5. El Sistema devuelve un código **200 OK** con el token JWT, tiempo de expiración y perfil básico del usuario autenticado.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Campos vacíos o incompletos (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON no contiene email o contraseña.
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje de campo obligatorio. Fin del caso de uso.

* **3a. Credenciales inválidas o usuario no encontrado (HTTP 401 Unauthorized):**
  1. Si en el Paso 3 el correo no existe o la contraseña no coincide con el hash almacenado (**RN-02**).
  2. La Capa de Negocio rechaza la autenticación protegiendo la información de existencia de cuenta.
  3. El Sistema devuelve un código **401 Unauthorized** con el mensaje: "Credenciales inválidas". Fin del caso de uso.

* **3b. Cuenta inactiva o deshabilitada (HTTP 403 Forbidden):**
  1. Si en el Paso 3 el usuario existe y sus credenciales son correctas, pero su cuenta tiene estado `Inactivo` (**RN-01**).
  2. La Capa de Negocio frena el acceso y lanza una `CuentaInactivaException`.
  3. El Sistema devuelve un código **403 Forbidden** indicando: "La cuenta de usuario se encuentra deshabilitada". Fin del caso de uso.

* **4a. Fallo técnico en la emisión del token (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 se produce un error en la firma criptográfica del JWT.
  2. El Sistema registra el incidente en el log de auditoría técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. Inicio de sesión desde la aplicación web o mediante clientes de prueba API.
2. Ambas envían el payload al endpoint `POST /api/auth/login`.

### 6. POSTCONDICIONES
- El usuario recibe un Token JWT válido para autenticar subsiguientes peticiones HTTP en el header `Authorization: Bearer <token>`.
- Se actualiza el campo `UltimoAcceso` del usuario en la base de datos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Autenticación exitosa y emisión del Token JWT. |
| `400` | Bad Request | Petición con campos obligatorios vacíos o mal formados. |
| `401` | Unauthorized | Credenciales incorrectas (usuario no encontrado o contraseña inválida). |
| `403` | Forbidden | Cuenta inactiva o deshabilitada (RN-01). |
| `500` | Internal Server Error | Error técnico no controlado durante el procesamiento. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de que `email` y `password` no sean nulos ni vacíos (`[Required]`, `[EmailAddress]` en `LoginRequestDTO`).
- **Verificación (Negocio, → 400/404/409):** Verificación de coincidencia criptográfica de hash de contraseña (**RN-02**), verificación de estado activo (**RN-01**) y construcción de claims en `AuthService`.

### Matriz de trazabilidad CU-05 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `LoginAsync_WithValidCredentials_ReturnsTokenAndUserClaims` | `Login_WithCorrectCredentials_Returns200AndJwtToken` |
| 2a. Campos vacíos | `400 Bad Request` | `— (detectado en model binding)` | `Login_WithEmptyFields_Returns400BadRequest` |
| 3a. Credenciales incorrectas | `401 Unauthorized` | `LoginAsync_WithWrongPassword_ThrowsCredencialesInvalidasException` | `Login_WithWrongCredentials_Returns401Unauthorized` |
| 3b. Cuenta inactiva | `403 Forbidden` | `LoginAsync_WhenUserInactive_ThrowsCuentaInactivaException` | `Login_WhenUserIsInactive_Returns403Forbidden` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
