# Caso de Uso: Solicitar reserva

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-01 (capacidad de comensales), RN-02 (no solapamiento), RN-03 (horarios habilitados), RN-04 (derivación a lista de espera) y RN-05 (reserva única por horario) incorporadas en el diseño; cada flujo y caso borde cuenta con su test unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-01 |
| **Nombre** | Solicitar reserva |
| **Actor Principal** | Cliente (usuario autenticado con rol Cliente) |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Cliente → asegurar una mesa en fecha y horario deseados con confirmación inmediata o alternativa en lista de espera; Restaurante / Gerencia → maximizar ocupación sin sobrecargar capacidad ni incurrir en sobreventas; Personal de Salón → disponer de asignación ordenada de comensales |
| **Disparador (Trigger)** | El cliente selecciona la opción "Solicitar Reserva" en la interfaz de la aplicación |
| **Prioridad / Frecuencia** | Alta; alta frecuencia diaria |
| **Reglas de negocio relacionadas** | RN-01 (capacidad máxima de mesa); RN-02 (no solapamiento de reservas en misma mesa); RN-03 (horarios y turnos de atención habilitados); RN-04 (derivación a lista de espera por falta de disponibilidad); RN-05 (unicidad de reserva activa por cliente en un mismo turno) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un cliente autenticado solicitar una reserva en el restaurante indicando fecha, horario y cantidad de personas. El sistema valida las reglas de dominio, verifica la disponibilidad de mesas y, de ser viable, genera la reserva registrada o, en su defecto, ofrece el ingreso a la lista de espera.

### 2. PRECONDICIONES
- El cliente debe estar previamente registrado en el sistema.
- El cliente debe contar con una sesión activa y un token de autenticación válido (Token JWT).
- El sistema y la Capa de Persistencia deben encontrarse operativos y accesibles.
- El restaurante debe poseer mesas configuradas activas en la base de datos.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201)
1. El Actor envía una petición al endpoint `POST /api/reservas` con un JSON que contiene los datos de la reserva (`fecha`, `hora`, `cantidadPersonas` y `observaciones` opcionales).
2. La **Capa de Presentación** (`ReservasController.SolicitarReserva`) valida que la estructura del JSON sea correcta y que los campos obligatorios cumplan con las restricciones de tipo y rango (`[Required]`, `cantidadPersonas > 0`, fecha válida futura).
3. La **Capa de Negocio** (`ReservaService.SolicitarReservaAsync`) verifica:
   - Que el horario solicitado corresponda a los turnos habilitados por el restaurante (**RN-03**).
   - Que el cliente no posea ya una reserva activa para esa misma franja horaria (**RN-05**).
   - Que la cantidad de personas solicitada no supere la capacidad de las mesas disponibles (**RN-01**).
   - Que exista al menos una mesa adecuada sin solapamiento de horario (**RN-02**).
4. La **Capa de Negocio** asigna la mesa óptima disponible y construye la entidad `Reserva` con estado inicial `Pendiente` (o `Confirmada` según la política de confirmación directa).
5. La **Capa de Persistencia** genera un nuevo identificador único (GUID) y persiste la reserva en la tabla `Reservas`.
6. El Sistema devuelve un código **201 Created** con el DTO de la reserva generada (`id`, `fecha`, `hora`, `cantidadPersonas`, `estado`, `mesaAsignadaId`) y la cabecera `Location`.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. JSON inválido o malformado (HTTP 400 Bad Request):**
  1. Si en el Paso 1 el cuerpo de la petición no es un JSON parseable, posee sintaxis rota o el cuerpo está vacío.
  2. El Sistema (Capa de Presentación / model binding) rechaza la petición por error de deserialización.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Datos obligatorios faltantes o con formato erróneo (HTTP 400 Bad Request):**
  1. Si en el Paso 2 faltan campos requeridos (`fecha`, `hora`, `cantidadPersonas`) o poseen tipos de datos incompatibles.
  2. La Capa de Presentación rechaza la solicitud al evaluar `ModelState.IsValid == false`.
  3. El Sistema devuelve un código **400 Bad Request** detallando los campos observados. Fin del caso de uso.

* **2b. Cantidad de comensales menor o igual a cero (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el campo `cantidadPersonas` es menor o igual a 0 (`[Range(1, int.MaxValue)]`).
  2. La Capa de Presentación rechaza la petición por validación de esquema.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "La cantidad de personas debe ser al menos 1". Fin del caso de uso.

* **2c. Fecha u horario en el pasado (HTTP 400 Bad Request):**
  1. Si en el Paso 2 la fecha y hora enviadas corresponden a un instante cronológico ya transcurrido.
  2. La Capa de Presentación (mediante validación personalizada de fecha) o la Capa de Negocio detecta la inconsistencia temporal.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "No se pueden solicitar reservas para fechas pasadas". Fin del caso de uso.

* **3a. Horario fuera de turnos de atención habilitados (HTTP 400 Bad Request):**
  1. Si en el Paso 3 el horario requerido se encuentra fuera de los rangos de atención del establecimiento, violando la regla **RN-03**.
  2. La Capa de Negocio lanza la excepción de dominio `HorarioFueraDeTurnoException`.
  3. El Sistema devuelve un código **400 Bad Request** indicando los turnos y franjas horarias permitidas. Fin del caso de uso.

* **3b. Cantidad de comensales excede la capacidad máxima de cualquier mesa (HTTP 400 Bad Request):**
  1. Si en el Paso 3 la cantidad de comensales solicitada supera la capacidad de la mesa más grande disponible en el establecimiento, violando la regla **RN-01**.
  2. La Capa de Negocio detecta que ninguna mesa individual o combinación habilitada puede albergar dicho tamaño de grupo y lanza `CapacidadExcedidaException`.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: "La cantidad de comensales excede la capacidad máxima habilitada". Fin del caso de uso.

* **3c. Cliente con reserva activa preexistente en la misma franja horaria (HTTP 409 Conflict):**
  1. Si en el Paso 3 se detecta que el cliente ya posee una reserva en estado Activa/Pendiente/Confirmada en el mismo turno y fecha, violando la regla **RN-05**.
  2. La Capa de Negocio lanza la excepción de dominio `ReservaClienteDuplicadaException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: "El usuario ya posee una reserva registrada para ese horario". Fin del caso de uso.

* **3d. No hay disponibilidad de mesas para la fecha, hora y pax solicitados (HTTP 409 Conflict):**
  1. Si en el Paso 3 todas las mesas con capacidad suficiente se encuentran ocupadas o reservadas en esa franja horaria (**RN-02**).
  2. La Capa de Negocio verifica la falta de cupos y, en cumplimiento de la regla **RN-04**, prepara una respuesta de conflicto que incluye la opción de ingresar a la lista de espera.
  3. El Sistema devuelve un código **409 Conflict** con un cuerpo descriptivo: "No hay mesas disponibles para el horario y comensales indicados. ¿Desea sumarse a la lista de espera?". Fin del caso de uso.

* **5a. Error interno en la persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 5 ocurre una falla no controlada de infraestructura (pérdida de conexión con la base de datos o timeout).
  2. El Sistema captura el fallo no controlado a través del middleware global de excepciones.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El cliente puede solicitar la reserva desde la interfaz web, una aplicación móvil o mediante herramientas cliente de API (Postman, Swagger/Scalar, Bruno).
2. En todas las variantes, el contrato de la petición JSON y el código de respuesta (`201 Created` ante el éxito) se mantienen invariantes.

### 6. POSTCONDICIONES
- Se crea un nuevo registro persistente en la tabla `Reservas` con su ID único y estado asignado.
- La mesa seleccionada queda temporalmente reservada para esa franja horaria, impactando en la visibilidad del catálogo de disponibilidad (**RN-02**).
- Si el cliente acepta ingresar a lista de espera tras el flujo 3d, se genera un registro en la tabla `ListaEspera`.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Confirmación de persistencia exitosa de la nueva solicitud de reserva. |
| `400` | Bad Request | Error de esquema sintáctico en JSON, parámetros obligatorios faltantes, valores negativos, fechas pasadas o fuera de turno (RN-01, RN-03). |
| `409` | Conflict | Violación de invariantes de negocio: reserva simultánea del cliente (RN-05) o falta de disponibilidad de mesa libre (RN-02 / oferta RN-04). |
| `500` | Internal Server Error | Falla no controlada de persistencia o conectividad en el servidor. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** Se asegura de que la petición cumpla con el contrato de interfaz: cuerpo JSON no nulo, campos requeridos presentes (`[Required]`), comensales en rango positivo (`[Range]`) y tipos de datos válidos.
- **Verificación (Negocio, → 400/409):** Aplica la lógica de dominio: validación de turnos habilitados del restaurante (**RN-03**), control de unicidad de reserva activa por comensal (**RN-05**), cotejo de disponibilidad física y reglas de no superposición (**RN-01**, **RN-02**) y activación de mecanismo de lista de espera (**RN-04**).

### Matriz de trazabilidad CU-01 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `SolicitarReservaAsync_WithValidData_ReturnsCreatedReservaDTO` | `SolicitarReserva_WithValidData_Returns201Created` |
| 1a. JSON inválido | `400 Bad Request` | — *(manejado por el model binding de ASP.NET Core)* | `SolicitarReserva_WithMalformedJson_Returns400BadRequest` |
| 2a. Datos requeridos faltantes | `400 Bad Request` | — *(detectado mediante data annotations en Presentación)* | `SolicitarReserva_WithMissingFields_Returns400BadRequest` |
| 2b. Comensales inválidos (<= 0) | `400 Bad Request` | `SolicitarReservaAsync_WithZeroOrNegativePax_ThrowsValidationException` | `SolicitarReserva_WithZeroPax_Returns400BadRequest` |
| 2c. Fecha/hora en el pasado | `400 Bad Request` | `SolicitarReservaAsync_WithPastDateTime_ThrowsValidationException` | `SolicitarReserva_WithPastDateTime_Returns400BadRequest` |
| 3a. Horario fuera de turnos | `400 Bad Request` | `SolicitarReservaAsync_WithDisabledSchedule_ThrowsHorarioFueraDeTurnoException` | `SolicitarReserva_WhenScheduleDisabled_Returns400BadRequest` |
| 3b. Pax excede capacidad máxima | `400 Bad Request` | `SolicitarReservaAsync_WhenPaxExceedsMaxCapacity_ThrowsCapacidadExcedidaException` | `SolicitarReserva_WhenPaxExceedsCapacity_Returns400BadRequest` |
| 3c. Reserva duplicada cliente | `409 Conflict` | `SolicitarReservaAsync_WhenClientAlreadyHasActiveBooking_ThrowsReservaClienteDuplicadaException` | `SolicitarReserva_WhenClientHasDuplicateBooking_Returns409Conflict` |
| 3d. Sin disponibilidad de mesa | `409 Conflict` | `SolicitarReservaAsync_WhenNoTablesAvailable_ThrowsNoTableAvailableException` | `SolicitarReserva_WhenNoTablesAvailable_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso debe contar con cobertura de pruebas. Los flujos resueltos en Presentación se verifican mediante pruebas de integración HTTP, mientras que las reglas de dominio son validadas exhaustivamente mediante pruebas unitarias en la Capa de Negocio.
