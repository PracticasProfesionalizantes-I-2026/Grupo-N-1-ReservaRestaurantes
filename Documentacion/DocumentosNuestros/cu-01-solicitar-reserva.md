# Caso de Uso: Solicitar reserva

> Especificación elaborada siguiendo la guía
> `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (capacidad máxima de mesa); RN-02 (sin reservas superpuestas); RN-03 (franjas horarias válidas); RN-04 (derivación a lista de espera) **implementadas** en la solución; cada caso borde cuenta con su test
> unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-01 |
| **Nombre** | Solicitar reserva |
| **Actor Principal** | Cliente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Cliente → solicitar una mesa para una fecha, horario y cantidad de personas específicas; Restaurante / Gerencia → organizar la demanda anticipada y optimizar la rotación de mesas; Mozos → recibir la planificación de comensales. |
| **Disparador (Trigger)** | El cliente selecciona la opción "Solicitar Reserva" desde la aplicación. |
| **Prioridad / Frecuencia** | Alta; alta frecuencia |
| **Reglas de negocio relacionadas** | RN-01 (capacidad máxima de mesa); RN-02 (sin reservas superpuestas); RN-03 (franjas horarias válidas); RN-04 (derivación a lista de espera) |

---

### 1. BREVE DESCRIPCIÓN
Permite al cliente solicitar una reserva indicando la fecha, horario y cantidad de personas. El sistema valida la disponibilidad de mesas y, si existe disponibilidad, la reserva queda registrada para su confirmación. En caso de no existir mesas libres, se ofrece el ingreso a la lista de espera.

### 2. PRECONDICIONES
- El sistema debe encontrarse en funcionamiento y con la base de datos accesible.
- El cliente debe encontrarse registrado en el sistema.
- El cliente debe poseer una sesión activa (Token JWT válido con rol Cliente).

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/reservas/solicitar` con un JSON conteniendo `fecha`, `hora` y `cantidadPersonas` (`ReservaSolicitudDTO`).
2. La **Capa de Presentación** (`ReservasController.SolicitarReserva`) valida que el JSON sea estructuralmente correcto y que los campos requeridos estén presentes y dentro de rangos válidos (data annotations `[Required]`, `[Range(1, 20)]`).
3. La **Capa de Negocio** (`ReservaService.SolicitarReservaAsync`) verifica que la fecha y hora correspondan a una franja habilitada (**RN-03**), que el cliente no posea ya una reserva activa en ese horario y busca mesas con capacidad suficiente (**RN-01**) sin solapamiento de turnos (**RN-02**).
4. La **Capa de Persistencia** (`ApplicationDbContext.Reservas`) persiste la nueva entidad `Reserva` con estado `Pendiente` y genera un nuevo identificador único (GUID).
5. El Sistema devuelve un código **201 Created** con la información resultante (ID de reserva, fecha, hora, comensales y estado asignado).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. JSON inválido o ilegible (HTTP 400 Bad Request):**
  1. Si en el Paso 1 el cuerpo de la petición no es un JSON válido o presenta errores de sintaxis.
  2. El Sistema (Capa de Presentación / Model Binding) rechaza la petición por error de esquema.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Dato obligatorio faltante o formato inválido (HTTP 400 Bad Request):**
  1. Si en el Paso 2 faltan campos obligatorios (`fecha`, `hora`, `cantidadPersonas`) o son menores o iguales a cero.
  2. El Sistema (Capa de Presentación) rechaza la petición por error de validación de modelo (`ModelState.IsValid == false`).
  3. El Sistema devuelve un código **400 Bad Request** detallando el campo observado. Fin del caso de uso.

* **3a. Horario fuera del rango operativo del restaurante (HTTP 400 Bad Request):**
  1. Si en el Paso 3 la fecha/hora seleccionada corresponde a un día de cierre o un horario no habilitado según la regla **RN-03**.
  2. El Sistema (Capa de Negocio) frena la operación y lanza una `HorarioNoHabilitadoException`.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "El horario seleccionado no se encuentra habilitado para reservas". Fin del caso de uso.

* **3b. No hay disponibilidad de mesas en la franja seleccionada (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que no existen mesas disponibles compatibles con la cantidad de personas solicitada (**RN-01**, **RN-02**).
  2. El Sistema frena la creación directa de la reserva y, conforme a la regla **RN-04**, ofrece la incorporación a la Lista de Espera.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "No hay disponibilidad de mesas para el horario y cantidad de personas solicitadas. Puede ingresar a la lista de espera". Fin del caso de uso.

* **3c. El cliente ya posee una reserva activa en ese horario (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que el cliente autenticado ya cuenta con una reserva activa para la misma fecha y horario.
  2. El Sistema (Capa de Negocio) lanza una `ReservaDuplicadaClienteException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: "Ya posee una reserva registrada para ese horario". Fin del caso de uso.

* **4a. Fallo técnico en la persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 ocurre una falla en el acceso a la base de datos (timeout o pérdida de conexión).
  2. El Sistema captura la excepción técnica y registra el error no controlado.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El cliente puede solicitar la reserva desde el portal web o desde un cliente HTTP (Swagger / Postman / Bruno).
2. En todas las variantes el payload y el código resultante (`201 Created`) son idénticos.

### 6. POSTCONDICIONES
- Se registra una nueva entidad `Reserva` persistida en estado `Pendiente` en la base de datos.
- La reserva queda visible en el panel del cliente y en el panel operativo del restaurante.
- Si no hubo disponibilidad y el cliente aceptó el ingreso a lista de espera, se registra la solicitud correspondiente.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Creación y persistencia exitosa de la solicitud de reserva. |
| `400` | Bad Request | JSON mal formado, datos faltantes o fuera de franja horaria habilitada (RN-03). |
| `401` | Unauthorized | Token JWT ausente, expirado o inválido. |
| `409` | Conflict | Falta de mesas disponibles (RN-01, RN-02) o reserva activa preexistente para el cliente. |
| `500` | Internal Server Error | Error técnico no controlado durante la persistencia. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Comprobación de esquema: campos obligatorios presentes, formato de fecha válido y cantidad de comensales positiva (`[Required]`, `[Range]` en `ReservaSolicitudDTO`).
- **Verificación (Negocio, → 400/404/409):** RN-01 (capacidad de mesa asignable), RN-02 (ausencia de solapamiento de reservas), RN-03 (franja horaria permitida) y control de reserva duplicada en `ReservaService`.

### Matriz de trazabilidad CU-01 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `SolicitarReservaAsync_WithValidData_ReturnsCreatedReserva` | `SolicitarReserva_Returns201Created` |
| 1a. JSON inválido | `400 Bad Request` | `— (validado por ASP.NET Model Binding)` | `SolicitarReserva_WithMalformedJson_Returns400BadRequest` |
| 2a. Datos incompletos | `400 Bad Request` | `— (detectado por DataAnnotations en DTO)` | `SolicitarReserva_WithMissingFields_Returns400BadRequest` |
| 3a. Horario no habilitado | `400 Bad Request` | `SolicitarReservaAsync_OutsideOperatingHours_ThrowsHorarioNoHabilitadoException` | `SolicitarReserva_WhenOutsideHours_Returns400BadRequest` |
| 3b. Sin disponibilidad | `409 Conflict` | `SolicitarReservaAsync_NoTablesAvailable_ThrowsSinDisponibilidadException` | `SolicitarReserva_WhenNoTables_Returns409Conflict` |
| 3c. Reserva duplicada cliente | `409 Conflict` | `SolicitarReservaAsync_ExistingActiveReserva_ThrowsReservaDuplicadaException` | `SolicitarReserva_WhenClientHasDuplicate_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe tener al menos un test. En los flujos
> resueltos en la Capa de Presentación el test aplicable es el de integración
> HTTP, ya que la lógica de negocio no se invoca. Los tests se ejecutan con
> `dotnet test ReservaRestaurantes.slnx`.
