# Caso de Uso: Gestionar reserva

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo rol Gerente); RN-02 (transiciones válidas de estado en ciclo de vida de la reserva); RN-03 (liberación de mesa al finalizar) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-02 |
| **Nombre** | Gestionar reserva |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → monitorear y gestionar integralmente las reservas del turno operativo; Mozos → recibir actualizaciones sobre asignación de mesas; Clientes → conocer el avance de su reserva. |
| **Disparador (Trigger)** | El gerente accede a la sección "Panel Operativo en Vivo". |
| **Prioridad / Frecuencia** | Alta; alta frecuencia durante el servicio |
| **Reglas de negocio relacionadas** | RN-01 (solo rol Gerente); RN-02 (transiciones válidas de estado en ciclo de vida de la reserva); RN-03 (liberación de mesa al finalizar) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente visualizar y administrar las reservas del restaurante desde el Panel Operativo en Vivo, evaluando su estado actual y ejecutando las acciones de transición permitidas (confirmar, iniciar, finalizar, cancelar o rechazar).

### 2. PRECONDICIONES
- El sistema debe encontrarse operativo con la base de datos accesible.
- El actor debe poseer un token JWT válido con rol "Gerente".
- Existe al menos una reserva registrada en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `GET /api/gerente/reservas` para consultar el tablero de control de reservas operativas.
2. La **Capa de Presentación** (`GerenteReservasController.GetReservasOperativas`) valida los claims de identidad y rol del usuario (`[Authorize(Roles = "Gerente")]`).
3. La **Capa de Negocio** (`GerenteReservaService.GetTableroOperativoAsync`) consulta las reservas activas y determina las acciones válidas permitidas según el estado actual de cada una (**RN-02**).
4. La **Capa de Persistencia** recupera los registros de la base de datos incluyendo información de mesas y clientes vinculados.
5. El Sistema devuelve un código **200 OK** con la lista de reservas y sus respectivas acciones operativas disponibles.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el token JWT presentado no incluye el rol `Gerente` (**RN-01**).
  2. La Capa de Presentación (Middleware de Autorización) rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. No se registran reservas para la jornada (HTTP 200 OK con colección vacía):**
  1. Si en el Paso 3 no existen reservas cargadas en el sistema para la fecha consultada.
  2. La Capa de Negocio procesa la consulta y genera una colección vacía.
  3. El Sistema devuelve un código **200 OK** con una lista vacía y mensaje descriptivo. Fin del caso de uso.

* **4a. Fallo técnico en la base de datos (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 se produce una interrupción en la conexión con la base de datos.
  2. El Sistema captura la excepción técnica y la registra en el log del servidor.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El gerente puede acceder desde la estación de trabajo de recepción o desde un dispositivo móvil/tablet del salón.
2. Es posible parametrizar la consulta mediante query strings para filtrar por estado (`GET /api/gerente/reservas?estado=Confirmada`).

### 6. POSTCONDICIONES
- El gerente visualiza el estado completo de las reservas del salón.
- No se producen alteraciones de datos en la base de datos en esta operación de consulta y coordinación.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Recuperación exitosa de las reservas del tablero operativo. |
| `401` | Unauthorized | Token JWT no provisto o expirado. |
| `403` | Forbidden | El usuario no cuenta con rol Gerente (RN-01). |
| `500` | Internal Server Error | Error técnico no controlado al acceder a los datos. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de token de autenticación y atributos de rol en la Capa de Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (autorización por rol Gerente) y cálculo de transiciones válidas según máquina de estados de reservas (**RN-02**) en `GerenteReservaService`.

### Matriz de trazabilidad CU-02 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetTableroOperativoAsync_ReturnsActiveReservasWithAvailableActions` | `GetReservasOperativas_AsGerente_Returns200OK` |
| 2a. Sin rol Gerente | `403 Forbidden` | `— (pipeline de autorización ASP.NET Core)` | `GetReservasOperativas_AsCliente_Returns403Forbidden` |
| 3a. Sin reservas registradas | `200 OK` | `GetTableroOperativoAsync_WhenNoReservas_ReturnsEmptyList` | `GetReservasOperativas_WhenEmpty_Returns200WithEmptyArray` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
