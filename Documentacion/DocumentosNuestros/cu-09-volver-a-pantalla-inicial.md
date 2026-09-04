# Caso de Uso: Volver a pantalla inicial

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (la pantalla inicial y datos cargados deben corresponder a los permisos y funciones habilitadas del rol activo) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-09 |
| **Nombre** | Volver a pantalla inicial |
| **Actor Principal** | Usuario |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario (Cliente, Gerente, Admin) → regresar al punto de partida de navegación correspondiente a sus permisos; Sistema → suministrar el resumen operativo y contextual correcto. |
| **Disparador (Trigger)** | El usuario selecciona el logotipo del restaurante o la opción de menú "Inicio / Panel Principal". |
| **Prioridad / Frecuencia** | Media; alta frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (la pantalla inicial y datos cargados deben corresponder a los permisos y funciones habilitadas del rol activo) |

---

### 1. BREVE DESCRIPCIÓN
Permite al usuario retornar a la pantalla principal del sistema, obteniendo los datos de contexto y accesos rápidos adaptados a su rol (Cliente, Gerente o Administrador).

### 2. PRECONDICIONES
- El sistema debe encontrarse operativo.
- El usuario debe contar con una sesión activa con un rol asignado.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `GET /api/usuario/contexto-inicio` con su token de autenticación.
2. La **Capa de Presentación** (`UsuarioController.GetContextoInicio`) valida que el usuario esté autenticado (`[Authorize]`).
3. La **Capa de Negocio** (`UsuarioService.GetContextoInicioAsync`) lee el rol del usuario desde los claims del token (**RN-01**) y recopila la información de resumen correspondiente (Cliente → próximas reservas; Gerente → métricas del salón del día; Admin → resumen de usuarios y configuración).
4. La **Capa de Persistencia** consulta los datos resumidos según el perfil requerido.
5. El Sistema devuelve un código **200 OK** con los datos de navegación y resumen contextual.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Sesión expirada o no autenticada (HTTP 401 Unauthorized):**
  1. Si en el Paso 2 el token JWT está ausente o vencido.
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **401 Unauthorized** redirigiendo al login. Fin del caso de uso.

* **3a. Rol no reconocido (HTTP 403 Forbidden):**
  1. Si en el Paso 3 los claims del usuario presentan un rol no configurado en el sistema.
  2. La Capa de Negocio deniega la generación de contexto.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **4a. Fallo técnico en la consulta de contexto (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 se produce un error en el acceso a la base de datos.
  2. El Sistema captura la excepción técnica.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El usuario puede accionar el retorno haciendo clic en el logo institucional o seleccionando 'Dashboard' en la barra de navegación.
2. En ambos casos se invoca el endpoint de contexto inicial.

### 6. POSTCONDICIONES
- El usuario visualiza la pantalla principal adaptada a sus privilegios y necesidades operativas.
- No se altera el estado de la base de datos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Entrega exitosa de datos de la pantalla inicial acorde al rol. |
| `401` | Unauthorized | Token JWT ausente o vencido. |
| `403` | Forbidden | Rol no habilitado para acceder al contexto solicitado (RN-01). |
| `500` | Internal Server Error | Error técnico no controlado en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de token JWT en cabecera HTTP.
- **Verificación (Negocio, → 400/404/409):** RN-01 (resolución de vista y datos según rol en `UsuarioService`).

### Matriz de trazabilidad CU-09 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetContextoInicioAsync_ReturnsSummaryAccordingToRole` | `GetContextoInicio_Returns200WithContextData` |
| 2a. Sin token | `401 Unauthorized` | `— (manejado por autenticación ASP.NET Core)` | `GetContextoInicio_WithoutToken_Returns401Unauthorized` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
