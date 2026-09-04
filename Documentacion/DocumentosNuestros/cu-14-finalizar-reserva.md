# Caso de Uso: Finalizar reserva

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (solo podrán finalizarse reservas en estado En curso); RN-02 (al finalizar una reserva, la mesa asignada se libera automáticamente pasando a estado Libre); RN-03 (solo el usuario con rol Gerente podrá finalizarla) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-14 |
| **Nombre** | Finalizar reserva |
| **Actor Principal** | Gerente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Gerente → cerrar el ciclo de la reserva y registrar el tiempo real de ocupación; Mozos → conocer la desocupación de la mesa para su fajina/limpieza; Próximos clientes → disponer de la mesa liberada. |
| **Disparador (Trigger)** | Los clientes desocupan la mesa y el gerente selecciona la acción "Finalizar" en el panel de reservas. |
| **Prioridad / Frecuencia** | Alta; alta frecuencia durante el servicio |
| **Reglas de negocio relacionadas** | RN-01 (solo podrán finalizarse reservas en estado En curso); RN-02 (al finalizar una reserva, la mesa asignada se libera automáticamente pasando a estado Libre); RN-03 (solo el usuario con rol Gerente podrá finalizarla) |

---

### 1. BREVE DESCRIPCIÓN
Permite al gerente dar por concluido el servicio de una reserva activa, cambiando su estado a Finalizada, registrando la hora de finalización y liberando automáticamente la mesa para nuevas asignaciones.

### 2. PRECONDICIONES
- El sistema se encuentra operativo con acceso a la base de datos.
- El actor debe poseer un token JWT válido con rol "Gerente".
- La reserva debe encontrarse en estado `En curso`.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/reservas/{id}/finalizar` con su token JWT.
2. La **Capa de Presentación** (`ReservasController.FinalizarReserva`) valida la autorización del rol `Gerente` (**RN-03**).
3. La **Capa de Negocio** (`ReservaService.FinalizarReservaAsync`) verifica la existencia de la reserva y confirma que su estado actual sea `En curso` (**RN-01**).
4. La Capa de Negocio cambia el estado de la reserva a `Finalizada`, registra la hora de cierre y actualiza el estado de la mesa asociada a `Libre` (**RN-02**).
5. La **Capa de Persistencia** guarda los cambios de forma transaccional en las tablas `Reservas` y `Mesas`.
6. El Sistema devuelve un código **200 OK** con los datos de la reserva finalizada y la confirmación de la mesa liberada.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Usuario no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 2 el usuario no posee rol `Gerente` (**RN-03**).
  2. La Capa de Presentación rechaza la petición.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

* **3a. Reserva no encontrada (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID no existe en el sistema.
  2. La Capa de Negocio lanza una `ReservaNoEncontradaException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Reserva no se encuentra en curso (HTTP 409 Conflict):**
  1. Si en el Paso 3 la reserva no posee estado `En curso` (por ejemplo, está en `Confirmada` o ya fue `Finalizada`) (**RN-01**).
  2. La Capa de Negocio frena la ejecución y lanza una `TransicionEstadoInvalidaException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "Solo es posible finalizar reservas que se encuentren en curso". Fin del caso de uso.

* **5a. Error en persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre una falla técnica en la base de datos.
  2. El Sistema revierte la transacción y registra el incidente.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. La finalización puede accionarse desde la vista de mesa en el mapa interactivo o desde la lista de reservas en curso.
2. Ambas operaciones invocan `PATCH /api/reservas/{id}/finalizar`.

### 6. POSTCONDICIONES
- La reserva pasa a estado `Finalizada` con registro de hora de egreso.
- La mesa asociada pasa a estado `Libre` quedando inmediatamente disponible en el mapa de mesas.
- Se recalculan métricas operativas de tiempo de rotación.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Finalización exitosa de la reserva y liberación de la mesa. |
| `401` | Unauthorized | Token JWT ausente o inválido. |
| `403` | Forbidden | Usuario sin rol Gerente (RN-03). |
| `404` | Not Found | Reserva no encontrada. |
| `409` | Conflict | La reserva no está en estado En curso (RN-01). |
| `500` | Internal Server Error | Error técnico transaccional en persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de token y rol Gerente en Capa de Presentación.
- **Verificación (Negocio, → 400/404/409):** RN-01 (estado actual En curso), RN-02 (liberación de mesa a Libre) y RN-03 (rol Gerente) en `ReservaService`.

### Matriz de trazabilidad CU-14 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `FinalizarReservaAsync_WhenEnCurso_TransitionsToFinalizadaAndFreesTable` | `FinalizarReserva_Returns200OK` |
| 2a. Sin rol Gerente | `403 Forbidden` | `— (middleware de autorización)` | `FinalizarReserva_AsCliente_Returns403Forbidden` |
| 3a. Reserva inexistente | `404 Not Found` | `FinalizarReservaAsync_WhenNotFound_ThrowsReservaNoEncontradaException` | `FinalizarReserva_NonExistingId_Returns404NotFound` |
| 3b. Estado inválido | `409 Conflict` | `FinalizarReservaAsync_WhenNotEnCurso_ThrowsTransicionEstadoInvalidaException` | `FinalizarReserva_WhenConfirmada_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
