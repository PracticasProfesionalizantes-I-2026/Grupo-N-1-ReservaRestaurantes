# Caso de Uso: Cerrar sesión

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (al cerrar sesión deben invalidarse las credenciales o sesión activa en el cliente y revocar refresh tokens en el servidor) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-08 |
| **Nombre** | Cerrar sesión |
| **Actor Principal** | Usuario |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario → proteger la privacidad y seguridad de su cuenta al finalizar su uso en el dispositivo; Sistema → invalidar tokens y registrar la finalización de sesión. |
| **Disparador (Trigger)** | El usuario selecciona la opción "Cerrar sesión" en el menú de usuario. |
| **Prioridad / Frecuencia** | Media; alta frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (al cerrar sesión deben invalidarse las credenciales o sesión activa en el cliente y revocar refresh tokens en el servidor) |

---

### 1. BREVE DESCRIPCIÓN
Permite al usuario finalizar su sesión activa, asegurando la revocación de tokens y el descarte de credenciales en el cliente.

### 2. PRECONDICIONES
- El sistema se encuentra operativo.
- El usuario debe contar con una sesión activa y un token JWT válido.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `POST /api/auth/logout` con el token JWT en el header `Authorization` y el refresh token en el cuerpo (`LogoutRequestDTO`).
2. La **Capa de Presentación** (`AuthController.Logout`) valida la presencia de la cabecera de autenticación.
3. La **Capa de Negocio** (`AuthService.LogoutAsync`) extrae el identificador de sesión/usuario y revoca el refresh token almacenado en la base de datos (**RN-01**).
4. La **Capa de Persistencia** actualiza el estado del token a revocado.
5. El Sistema devuelve un código **200 OK** confirmando el cierre de sesión y ordenando la limpieza de cookies/almacenamiento local en el cliente.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Petición no autenticada o token ausente (HTTP 401 Unauthorized):**
  1. Si en el Paso 2 no se adjunta el token JWT o este ya se encuentra expirado.
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **401 Unauthorized**. Fin del caso de uso.

* **3a. Fallo técnico al revocar credenciales (HTTP 500 Internal Server Error):**
  1. Si en el Paso 3 se produce un error en la base de datos al marcar la revocación del token.
  2. El Sistema captura la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El usuario puede cerrar sesión desde el menú de la aplicación web o móvil.
2. Ambas invocan `POST /api/auth/logout`.

### 6. POSTCONDICIONES
- El refresh token queda revocado en la base de datos (**RN-01**).
- El cliente elimina el token JWT de su almacenamiento local impidiendo futuras peticiones no autenticadas.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Cierre de sesión exitoso y credenciales revocadas. |
| `401` | Unauthorized | Token JWT ausente, inválido o previamente revocado. |
| `500` | Internal Server Error | Error técnico al revocar el token en la base de datos. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación del header Authorization Bearer en la Capa de Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (revocación de sesión activa y persistencia de estado revocado en `AuthService`).

### Matriz de trazabilidad CU-08 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `LogoutAsync_WithValidSession_RevokesRefreshToken` | `Logout_WithValidAuth_Returns200OK` |
| 2a. Sin token | `401 Unauthorized` | `— (rechazado por middleware JWT)` | `Logout_WithoutToken_Returns401Unauthorized` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
