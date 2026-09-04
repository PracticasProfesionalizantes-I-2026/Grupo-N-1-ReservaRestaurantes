# Caso de Uso: Eliminar usuarios

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Admin podrá eliminar usuarios); RN-02 (la eliminación debe requerir confirmación explícita); RN-03 (el sistema debe preservar la integridad referencial aplicando baja lógica si el usuario tiene reservas asociadas) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-20 |
| **Nombre** | Eliminar usuarios |
| **Actor Principal** | Admin |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Administrador → depurar cuentas obsoletas o bajas de personal; Sistema → salvaguardar la integridad histórica de reservas y auditoría. |
| **Disparador (Trigger)** | El administrador selecciona la opción "Eliminar usuario" y confirma la acción en el modal de confirmación. |
| **Prioridad / Frecuencia** | Media; baja frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (solo el Admin podrá eliminar usuarios); RN-02 (la eliminación debe requerir confirmación explícita); RN-03 (el sistema debe preservar la integridad referencial aplicando baja lógica si el usuario tiene reservas asociadas) |

---

### 1. BREVE DESCRIPCIÓN
Permite al administrador eliminar o deshabilitar lógicamente una cuenta de usuario del sistema, preservando el histórico de reservas vinculadas para cumplir la integridad referencial.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Admin".
- El usuario a eliminar debe existir en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `DELETE /api/admin/usuarios/{id}` con confirmación en header o cuerpo (`{ "confirmado": true }`).
2. La **Capa de Presentación** (`AdminUsuariosController.EliminarUsuario`) valida el rol `Admin` (**RN-01**) y comprueba el flag de confirmación (**RN-02**).
3. La **Capa de Negocio** (`UsuarioService.EliminarUsuarioAsync`) verifica la existencia del usuario, constata que no se trate de la cuenta propia del administrador en sesión y evalúa si tiene registros de reservas asociadas.
4. Si el usuario posee reservas o historial, la Capa de Negocio aplica una **baja lógica** (`IsDeleted = true`, `Activo = false`) preservando los datos históricos (**RN-03**); si no posee relaciones, procede a su remoción física.
5. La **Capa de Persistencia** actualiza o remueve el registro en la tabla `Usuarios`.
6. El Sistema devuelve un código **200 OK** confirmando la baja del usuario.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Falta de confirmación explícita (HTTP 400 Bad Request):**
  1. Si en el Paso 2 no se incluye la confirmación explícita requerida (**RN-02**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** indicando: "Se requiere confirmación explícita para eliminar un usuario". Fin del caso de uso.

* **2b. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token no cuenta con rol `Admin` (**RN-01**).
  2. La Capa de Presentación deniega el acceso.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Usuario no encontrado (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID no existe en la base de datos.
  2. La Capa de Negocio lanza una `UsuarioNoEncontradoException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Intento de auto-eliminación de la cuenta en sesión (HTTP 409 Conflict):**
  1. Si en el Paso 3 el administrador intenta eliminar su propia cuenta activa.
  2. La Capa de Negocio bloquea la operación para prevenir pérdida de sesión administrativa.
  3. El Sistema devuelve un código **409 Conflict** indicando: "No es posible eliminar la propia cuenta de administrador en sesión". Fin del caso de uso.

* **5a. Fallo técnico en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre una falla de base de datos.
  2. El Sistema registra la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. La eliminación puede realizarse desde la tabla de usuarios con diálogo modal de confirmación.
2. En todos los casos se invoca `DELETE /api/admin/usuarios/{id}`.

### 6. POSTCONDICIONES
- La cuenta queda eliminada o marcada con baja lógica en la base de datos.
- El usuario no puede volver a iniciar sesión.
- Se conserva la integridad histórica de reservas asociadas (**RN-03**).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Baja de usuario ejecutada correctamente. |
| `400` | Bad Request | Falta de confirmación explícita (RN-02) o ID no numérico. |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Admin (RN-01). |
| `404` | Not Found | Usuario no encontrado. |
| `409` | Conflict | Intento de auto-eliminación de la cuenta en sesión. |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de flag de confirmación y autorización de rol Admin en Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Admin), RN-02 (confirmación requerida) y RN-03 (integridad referencial y baja lógica si posee reservas) en `UsuarioService`.

### Matriz de trazabilidad CU-20 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `EliminarUsuarioAsync_WithHistory_PerformsSoftDelete` | `EliminarUsuario_Returns200OK` |
| 2a. Sin confirmación | `400 Bad Request` | `— (validado en controlador/DTO)` | `EliminarUsuario_WithoutConfirm_Returns400BadRequest` |
| 2b. Sin rol Admin | `403 Forbidden` | `— (middleware de autorización)` | `EliminarUsuario_AsGerente_Returns403Forbidden` |
| 3a. Usuario inexistente | `404 Not Found` | `EliminarUsuarioAsync_WhenNotFound_ThrowsUsuarioNoEncontradoException` | `EliminarUsuario_NonExistingId_Returns404NotFound` |
| 3b. Auto-eliminación | `409 Conflict` | `EliminarUsuarioAsync_SelfDelete_ThrowsOperacionInvalidaException` | `EliminarUsuario_SelfDelete_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
