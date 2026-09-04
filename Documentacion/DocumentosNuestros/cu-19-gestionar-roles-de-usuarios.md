# Caso de Uso: Gestionar roles de usuarios

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Admin podrá gestionar roles de usuarios); RN-02 (solo podrán asignarse roles existentes y habilitados en el sistema); RN-03 (los permisos y tokens del usuario deberán actualizarse acorde al nuevo rol) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-19 |
| **Nombre** | Gestionar roles de usuarios |
| **Actor Principal** | Admin |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Administrador → gobernar la seguridad y asignar roles (Cliente, Gerente, Admin) al personal; Personal del restaurante → contar con los permisos operativos acordes a su función. |
| **Disparador (Trigger)** | El administrador selecciona la opción "Gestionar roles de usuarios" en el módulo de administración. |
| **Prioridad / Frecuencia** | Media; baja a media frecuencia (configuración de personal) |
| **Reglas de negocio relacionadas** | RN-01 (solo el Admin podrá gestionar roles de usuarios); RN-02 (solo podrán asignarse roles existentes y habilitados en el sistema); RN-03 (los permisos y tokens del usuario deberán actualizarse acorde al nuevo rol) |

---

### 1. BREVE DESCRIPCIÓN
Permite al administrador consultar el listado de usuarios del sistema y modificar el rol asignado a una cuenta específica (ej. promover a Gerente o Administrador).

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Admin".
- Deben existir usuarios registrados en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PUT /api/admin/usuarios/{id}/rol` con un JSON conteniendo `nuevoRol` (`UsuarioCambiarRolDTO`).
2. La **Capa de Presentación** (`AdminUsuariosController.CambiarRol`) valida la autorización del rol `Admin` (`[Authorize(Roles = "Admin")]`) (**RN-01**) y que el campo `nuevoRol` no esté vacío.
3. La **Capa de Negocio** (`UsuarioService.CambiarRolUsuarioAsync`) verifica la existencia del usuario, valida que el rol solicitado pertenezca al catálogo oficial de roles válidos (`Cliente`, `Gerente`, `Admin`) (**RN-02**) y comprueba que no se despoje al último administrador del sistema.
4. La Capa de Negocio actualiza el rol en la entidad `Usuario` y revoca los tokens activos del usuario para forzar la reemisión de credenciales con los nuevos claims (**RN-03**).
5. La **Capa de Persistencia** actualiza el registro en la tabla `Usuarios`.
6. El Sistema devuelve un código **200 OK** con los datos actualizados del usuario y su nuevo rol.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Rol no permitido o inexistente (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el valor enviado en `nuevoRol` no existe o no se encuentra habilitado en el catálogo de seguridad (**RN-02**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "El rol especificado no es válido en el sistema". Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token no cuenta con rol `Admin` (**RN-01**).
  2. La Capa de Presentación deniega el acceso.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Usuario no encontrado (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID del usuario no existe en la base de datos.
  2. La Capa de Negocio lanza una `UsuarioNoEncontradoException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Intento de remover el único Administrador activo (HTTP 409 Conflict):**
  1. Si en el Paso 3 el cambio de rol dejaría al sistema sin ningún usuario con rol `Admin` activo.
  2. La Capa de Negocio frena la operación por salvaguarda de gobernanza.
  3. El Sistema devuelve un código **409 Conflict** indicando: "No se puede quitar el rol de Administrador al único administrador activo del sistema". Fin del caso de uso.

* **5a. Error en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre una falla al persistir el cambio en la base de datos.
  2. El Sistema registra la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El administrador puede cambiar el rol desde la grilla de administración de cuentas de usuario.
2. Se envía la petición PUT a `/api/admin/usuarios/{id}/rol`.

### 6. POSTCONDICIONES
- El usuario queda asociado a su nuevo rol en la base de datos.
- Los tokens previos son invalidados forzando una nueva autenticación con los claims actualizados.
- Queda registrada la acción en el registro de auditoría administrativa.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Actualización exitosa del rol del usuario. |
| `400` | Bad Request | Rol especificado no válido o no habilitado (RN-02). |
| `401` | Unauthorized | Token JWT ausente o expirado. |
| `403` | Forbidden | Usuario sin rol Admin (RN-01). |
| `404` | Not Found | Usuario no encontrado con el ID provisto. |
| `409` | Conflict | Violación de regla de gobernanza (único administrador activo). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de que `nuevoRol` no sea nulo y verificación de rol Admin en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Admin), RN-02 (roles habilitados) y RN-03 (revocación de sesiones y actualización de permisos) en `UsuarioService`.

### Matriz de trazabilidad CU-19 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `CambiarRolUsuarioAsync_WithValidRole_UpdatesRoleAndRevokesTokens` | `CambiarRolUsuario_Returns200OK` |
| 2a. Rol inexistente | `400 Bad Request` | `— (validado por catálogo de roles en DTO)` | `CambiarRolUsuario_WithInvalidRole_Returns400BadRequest` |
| 2b. Sin rol Admin | `403 Forbidden` | `— (middleware de autorización)` | `CambiarRolUsuario_AsGerente_Returns403Forbidden` |
| 3a. Usuario inexistente | `404 Not Found` | `CambiarRolUsuarioAsync_WhenNotFound_ThrowsUsuarioNoEncontradoException` | `CambiarRolUsuario_NonExistingId_Returns404NotFound` |
| 3b. Remover único Admin | `409 Conflict` | `CambiarRolUsuarioAsync_WhenLastAdmin_ThrowsOperacionInvalidaException` | `CambiarRolUsuario_WhenLastAdmin_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
