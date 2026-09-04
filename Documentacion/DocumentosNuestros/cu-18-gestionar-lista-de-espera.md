# Caso de Uso: Gestionar lista de espera

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo el Gerente podrá gestionar la lista de espera); RN-02 (las solicitudes deberán respetar el orden de llegada FIFO y compatibilidad de personas); RN-03 (una solicitud solo podrá avanzar a reserva si existe una mesa disponible compatible) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-18 |
| **Nombre** | Gestionar lista de espera |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → administrar solicitudes de clientes sin reserva previa o en turnos llenos; Clientes en espera → ser reasignados a mesas que se liberen de forma ordenada. |
| **Disparador (Trigger)** | El gerente accede a la sección "Lista de Espera" del panel operativo. |
| **Prioridad / Frecuencia** | Media; alta frecuencia en días y horarios pico |
| **Reglas de negocio relacionadas** | RN-01 (solo el Gerente podrá gestionar la lista de espera); RN-02 (las solicitudes deberán respetar el orden de llegada FIFO y compatibilidad de personas); RN-03 (una solicitud solo podrá avanzar a reserva si existe una mesa disponible compatible) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente consultar la cola de espera de clientes y promover solicitudes a reservas efectivas ante mesas liberadas o cancelaciones, respetando el orden de llegada y la capacidad.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".
- Existen solicitudes en lista de espera registradas.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `GET /api/lista-espera` para consultar las solicitudes en espera activas.
2. La **Capa de Presentación** (`ListaEsperaController.GetSolicitudes`) valida el rol `Gerente` (**RN-01**).
3. La **Capa de Negocio** (`ListaEsperaService.GetSolicitudesOrdenadasAsync`) recupera las solicitudes activas ordenadas por fecha/hora de registro en estricto orden de llegada (**RN-02**).
4. Ante una mesa disponible, el Gerente envía `PATCH /api/lista-espera/{id}/promover` con el `mesaId` propuesto.
5. La Capa de Negocio verifica que la mesa seleccionada esté efectivamente libre y cuente con capacidad adecuada (**RN-03**), promueve la solicitud creando una entidad `Reserva` en estado `Confirmada` y desactiva la entrada de lista de espera.
6. La **Capa de Persistencia** guarda los cambios de forma transaccional.
7. El Sistema devuelve un código **200 OK** con los datos de la nueva reserva generada desde la lista de espera.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el usuario no posee rol `Gerente` (**RN-01**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Lista de espera vacía (HTTP 200 OK con colección vacía):**
  1. Si en el Paso 3 no existen solicitudes pendientes en la lista de espera.
  2. La Capa de Negocio retorna una lista vacía.
  3. El Sistema devuelve un código **200 OK** con un arreglo vacío `[]`. Fin del caso de uso.

* **4a. Solicitud de lista de espera no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 4 el ID de la solicitud en lista de espera no existe en la base de datos.
  2. La Capa de Negocio lanza una `EntidadNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **5a. Mesa incompatible o no disponible al promover (HTTP 409 Conflict):**
  1. Si en el Paso 5 la mesa indicada para la promoción no está libre o su capacidad es insuficiente para los comensales de la solicitud (**RN-03**).
  2. La Capa de Negocio frena la promoción y lanza una `MesaNoCompatibleException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "La mesa no se encuentra disponible o no cuenta con capacidad compatible". Fin del caso de uso.

* **6a. Error técnico en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 6 ocurre una falla de base de datos durante la transacción.
  2. El Sistema captura la excepción y revierte la operación.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El gerente puede promover una solicitud asignando una mesa manualmente o aceptar la asignación automática recomendada por el sistema.
2. En ambos casos se envía la solicitud al endpoint de promoción.

### 6. POSTCONDICIONES
- La solicitud de lista de espera pasa a estado `Atendida` o `Promovida`.
- Se crea una nueva entidad `Reserva` persistente en estado `Confirmada` con la mesa asignada.
- El cliente es notificado de la asignación de su mesa.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Consulta de lista de espera o promoción exitosa a reserva. |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente (RN-01). |
| `404` | Not Found | Solicitud de lista de espera o mesa no encontrada. |
| `409` | Conflict | La mesa no está disponible o su capacidad es insuficiente (RN-03). |
| `500` | Internal Server Error | Error técnico transaccional en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de token y rol Gerente en Capa de Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (rol Gerente), RN-02 (orden FIFO de solicitudes) y RN-03 (disponibilidad y capacidad de mesa compatible) en `ListaEsperaService`.

### Matriz de trazabilidad CU-18 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `PromoverSolicitudAsync_WithCompatibleTable_CreatesConfirmedReserva` | `PromoverSolicitud_Returns200OK` |
| 2a. Sin rol Gerente | `403 Forbidden` | `— (middleware de autorización)` | `GetSolicitudesEspera_AsCliente_Returns403Forbidden` |
| 4a. Solicitud inexistente | `404 Not Found` | `PromoverSolicitudAsync_WhenNotFound_ThrowsEntidadNoEncontradaException` | `PromoverSolicitud_NonExistingId_Returns404NotFound` |
| 5a. Mesa incompatible | `409 Conflict` | `PromoverSolicitudAsync_WhenIncompatibleTable_ThrowsMesaNoCompatibleException` | `PromoverSolicitud_WhenIncompatibleTable_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
