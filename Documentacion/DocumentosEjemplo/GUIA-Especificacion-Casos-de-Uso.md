# Guía Técnica: Especificación de Casos de Uso en Entornos .NET

> **Público:** estudiantes de Ingeniería de Software (Prácticas Profesionalizantes I).
> **Caso de uso de referencia:** la API `Clínica San Salud` (turnos médicos).

---

## 1. Introducción a la Especificación Técnica

En el desarrollo de sistemas distribuidos sobre el ecosistema .NET, la transición de un
requerimiento funcional a una implementación técnica requiere una precisión quirúrgica.
Como futuros ingenieros, deben comprender que la documentación de un Caso de Uso no es un
simple relato de interacciones, sino la **definición del contrato técnico** que regirá la
arquitectura de la solución.

Documentar considerando la **separación de responsabilidades entre capas** (Presentación,
Negocio, Persistencia) y los **protocolos de comunicación (HTTP)** es fundamental para
asegurar la calidad del software. Esta guía les proporcionará las herramientas para
especificar comportamientos que impactan directamente en la Capa de Presentación, la Capa
de Negocio y la Capa de Persistencia, garantizando la robustez y seguridad mediante el uso
de JWT y estándares de la industria.

---

## 2. Fundamentos: ¿qué es un Caso de Uso según la Ingeniería de Software?

Según el estándar UML, un **caso de uso** describe un conjunto de escenarios que comparten
una meta común de un actor. Alistair Cockburn (*Writing Effective Use Cases*, Addison-Wesley,
2001) lo define como: *"todas las formas de usar un sistema para alcanzar una meta particular
para un usuario particular"*.

De la literatura profesional (Cockburn; UML; ISO/IEC/IEEE 29148 sobre ingeniería de
requerimientos) se desprenden elementos que **una buena especificación técnica no puede
omitir**:

| Elemento | Pregunta que responde |
| --- | --- |
| `Alcance (Scope)` y `Nivel (Level)` | ¿Qué sistema se describe y con qué grado de detalle? |
| `Stakeholders e intereses` | ¿Quiénes participan y qué esperan obtener? |
| `Disparador (Trigger)` | ¿Qué evento inicia el caso de uso? |
| `Precondiciones` y `Poscondiciones` | ¿Qué estado debe existir antes y cuál queda después? |
| `Condición de éxito / fracaso` | ¿Qué define que el caso terminó bien o mal? |
| `Flujo principal + Extensiones` | ¿Cuál es el camino feliz y cuáles los desvíos? |
| `Reglas de negocio referenciadas` | ¿Qué invariantes del dominio intervienen? |
| `Requerimientos no funcionales` | ¿Tiempo de respuesta, seguridad, concurrencia? |

> 💡 **Por qué importa:** estos campos existen porque en proyectos reales los casos de uso
> son la base para estimar, diseñar, implementar **y testear**. Un caso de uso sin *trigger*
> o sin *condición de fallo* es ambiguo y genera errores en la implementación.

---

## 3. Plantilla Estándar de Caso de Uso (para reutilización)

> Las secciones marcadas **← mejora** incorporan campos de la práctica profesional
> (Cockburn/UML) que no estaban en la versión original de la guía. Son obligatorias salvo
> indicación contraria.

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | [Completar, ej: CU-01] |
| **Nombre** | [Completar nombre de la funcionalidad, frase de verbo activo corta] |
| **Actor Principal** | [Identificar el rol que inicia la acción] |
| **Alcance / Nivel** | [Sistema o negocio; resumen / meta de usuario / subfunción] ← mejora |
| **Stakeholders e intereses** | [Rol → qué espera del sistema] ← mejora |
| **Disparador (Trigger)** | [Evento que inicia el caso de uso] ← mejora |
| **Prioridad / Frecuencia** | [Alta/Media/Baja; usos estimados por período] ← mejora |
| **Reglas de negocio relacionadas** | [RN-XX que intervienen] ← mejora |

---

### 1. BREVE DESCRIPCIÓN
[Resumen de una línea del objetivo del Caso de Uso]

### 2. PRECONDICIONES
1. [Estado previo necesario del sistema / Existencia de datos]
2. [Estado de Autenticación / Requerimiento de Token JWT]

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201/200)
1. El Actor envía una petición al endpoint `<VERBO> /api/<recurso>` con los datos necesarios.
2. La **Capa de Presentación** valida que el formato de los datos (esquema) sea correcto.
3. La **Capa de Negocio** ejecuta las validaciones lógicas y verifica las reglas de negocio.
4. El Sistema persiste los cambios en la **Capa de Persistencia** (Base de Datos).
5. El Sistema devuelve un código **HTTP 201** o **200** con la información resultante.

> Reglas de redacción del flujo (Cockburn): cada paso debe mostrar **una meta que
> satisface un interés de un stakeholder**; indicar la intención del actor (no la UI);
> usar tiempos simples y verbos activos.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)
> *Instrucción: referenciar el paso exacto del Flujo Principal donde ocurre el desvío
> (ej: 2a, 3b). Listar **todas** las condiciones que el sistema puede detectar y debe
> manejar.*

* **[Paso del desvío]. Nombre del Error (HTTP 400/404/409):**
  1. Condición que dispara la excepción.
  2. Acción de la capa correspondiente (Presentación o Negocio).
  3. Código de respuesta y mensaje de error. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional) ← mejora
[Variaciones de datos/mecanismo de un mismo paso, sin cambiar su resultado. Ej:
"paso 1: el cliente puede consultar por web, teléfono o app".]

### 6. POSTCONDICIONES
1. [Cambio de estado persistente en la Base de Datos]
2. [Impacto en la visibilidad de los datos para otros procesos o actores]

---

## 4. Ejemplo de Aplicación — Reservar Turno Médico

Ejemplo completo aplicando la plantilla (sección 3) a la API de la clínica.

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-01 |
| **Nombre** | Reservar Turno Médico |
| **Actor Principal** | Paciente |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Paciente → obtener un horario confirmado; Médico → no tener solapamientos en su agenda; Administración → registrar la reserva |
| **Disparador (Trigger)** | El paciente solicita un turno para un médico |
| **Prioridad / Frecuencia** | Alta |
| **Reglas de negocio relacionadas** | RN-02 (sin solapamiento de turnos del mismo médico) |

### 1. BREVE DESCRIPCIÓN
Permite a un paciente registrado reservar un horario disponible para un médico específico.

### 2. PRECONDICIONES
1. El sistema debe tener al menos un médico registrado en la Capa de Persistencia.
2. El actor debe poseer un estado de autenticación activo (Token JWT válido).

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201/200)
1. El Actor envía una petición al endpoint `POST /api/turnos` con los datos de la reserva
   (JSON con ID Médico, Fecha, Hora).
2. El Sistema (Capa de Presentación) valida que el formato de los datos en el JSON sea correcto.
3. El Sistema (Capa de Negocio) verifica que el horario solicitado esté libre, aplicando la
   regla de negocio **RN-02**.
4. El Sistema registra la reserva en la Base de Datos con estado "Confirmado".
5. El Sistema devuelve un código **201 Created** con el ID de la nueva reserva.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Datos Incompletos (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON no incluye el ID del Médico o la Fecha.
  2. El Sistema (Capa de Presentación) rechaza la petición por error de esquema.
  3. El Sistema devuelve un código **400 Bad Request** detallando el campo faltante.
     Fin del caso de uso.
* **3a. Horario Ocupado / Concurrencia (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que ya existe un turno en ese horario, violando
     la **RN-02**.
  2. El Sistema frena la ejecución en la Capa de Negocio y lanza una excepción de dominio.
  3. El Sistema devuelve un error **409 Conflict** con el mensaje: "El horario seleccionado
     ya no se encuentra disponible". Fin del caso de uso.
* **3b. Médico Inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID del Médico enviado no existe en los registros de la base de datos.
  2. La Capa de Negocio no encuentra la entidad correspondiente.
  3. El Sistema devuelve un error **404 Not Found**. Fin del caso de uso.

### 5. POSTCONDICIONES
1. Se ha creado un nuevo registro persistente en la tabla `Turnos`.
2. El horario seleccionado ya no aparece en las listas de disponibilidad (Impacto en la
   visibilidad).

---

## 5. Resumen de Códigos HTTP y Capas del Sistema

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Confirmación de persistencia exitosa del nuevo recurso Turno. |
| `200` | OK | Éxito en operaciones de lectura o actualización (sin recurso nuevo). |
| `204` | No Content | Éxito en operaciones de borrado (sin cuerpo en la respuesta). |
| `400` | Bad Request | Fallo en la validación de esquema o sintaxis del JSON recibido. |
| `404` | Not Found | Inexistencia del recurso referenciado (Médico) en la Capa de Persistencia. |
| `409` | Conflict | Violación de invariantes de negocio o reglas de concurrencia (RN-02). |

> Los códigos adicionales respecto de la versión original de la guía (`200`, `204`) son
> los que usan los verbos HTTP del proyecto real (consultas con `200`, borrados con `204`).
> Deben elegir el código **según la semántica del caso de uso**, no "el que ven en un ejemplo".

## 6. Nota Técnica sobre Responsabilidades: Validación vs. Verificación

Es imperativo que el desarrollador distinga entre **Validación** y **Verificación**.

- La **Capa de Presentación** (Controllers y DTOs) asume la responsabilidad de la
  **Validación de sintaxis**: se asegura de que el JSON sea estructuralmente correcto,
  devolviendo un **400 Bad Request** si hay datos faltantes.
- La **Capa de Negocio** (Business Layer) se encarga de la **Verificación de la semántica
  y la integridad del dominio**. Es en esta capa donde se aplican reglas como la **RN-02**
  y se lanzan excepciones que resultan en códigos **409 Conflict** o **404 Not Found**.

Esta separación garantiza que la lógica de dominio permanezca **aislada** de las
preocupaciones de la interfaz o del protocolo de entrada.

---

## 7. ¿De dónde salen los tests del sistema? (Trazabilidad CU → Test)

El caso de uso **es la fuente directa** de los tests automatizados del proyecto
(`SanSaludAPI.Tests/`). Cada flujo (principal o alternativo) se convierte en **un test por
capa**:

| Artículo del Caso de Uso | Test que genera |
| --- | --- |
| **Flujo principal** (camino feliz) | Test de éxito: verifica el resultado esperado y el código HTTP (200/201/204). |
| **Cada flujo alternativo** (excepción) | Test de error: verifica la excepción lanzada y el código HTTP (400/404/409). |
| **Precondiciones** | Configuración inicial del test (*Arrange*). |
| **Poscondiciones** | Verificaciones finales del test (*Assert*). |
| **Reglas de negocio (RN-XX)** | Tests específicos que las ejercitan (ej. RN-02 → solapamiento). |

El proyecto usa **dos niveles de prueba**, siguiendo el mismo flujo Controller → Service →
Repository de la arquitectura:

1. **Tests unitarios** (`MedicoServiceTests.cs`, `TurnoServiceTests.cs`): prueban la **Capa
   de Negocio** aislada. Simulan los repositorios con **Moq** y verifican que, ante una
   condición, el service lanza la excepción correcta o retorna el DTO esperado.
2. **Tests de integración** (`IntegrationTests/`): prueban el **flujo HTTP completo** con
   `WebApplicationFactory` y una base SQLite *en memoria*. Verifican el código HTTP real que
   devuelve el endpoint.

> Regla de oro: **si un caso de uso define un flujo, ese flujo debe tener al menos un test.**
> Un flujo alternativo sin test es una regla de negocio sin verificar.

### 7.1 Convención de nombres de tests

Se usan nombres descriptivos en inglés con el patrón `[Método]_When[Condición]_[Resultado]`:

- `CreateTurnoAsync_WithOverlappingSchedule_ThrowsOverlappingScheduleException` → test unitario para el **3a** del CU-01.
- `CreateTurno_WhenOverlappingSchedule_Returns409Conflict` → test de integración para el **3a** del CU-01.

### 7.2 Ejemplo de trazabilidad: CU-01 Reservar Turno Médico

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CreateTurnoAsync_WithValidData_ReturnsCreatedTurnoResponseDTO` | `CreateTurno_WithValidData_Returns201Created` |
| 2a. Datos incompletos | `400 Bad Request` | — (validación de esquema) | `CreateTurno_WithPastDate_Returns400BadRequest` |
| 3a. Horario ocupado | `409 Conflict` | `CreateTurnoAsync_WithOverlappingSchedule_ThrowsOverlappingScheduleException` | `CreateTurno_WhenOverlappingSchedule_Returns409Conflict` |
| 3b. Médico inexistente | `404 Not Found` | `CreateTurnoAsync_WithNonExistentMedico_ThrowsMedicoNotFoundException` | `GetMedicoById_WhenNonExistent_ReturnsNotFound` |

> Al completar un caso de uso, incorporen la traza de tests en un apartado extra del
> documento (ej. *"Anexo: matriz de trazabilidad"*); así el docente puede ver de dónde sale
> cada test. Ejecución completa de la suite: `dotnet test Clinica-San-Salud.slnx`.

---

## 8. Referencias

- Cockburn, A. *Writing Effective Use Cases.* Addison-Wesley, 2001.
- Cockburn, A. *Structuring Use Cases with Goals.* Humans and Technology, 1997.
- OMG. *Unified Modeling Language (UML) — Use Case Diagrams.*
- ISO/IEC/IEEE 29148: *Systems and software engineering — Life cycle processes —
  Requirements engineering.*

---

## Anexo A. Lista de verificación (checklist) antes de entregar

- [ ] ¿El nombre del caso de uso es una meta de verbo activo?
- [ ] ¿Se completaron: Alcance, Disparador, Stakeholders, Precondiciones y Poscondiciones?
- [ ] ¿El flujo principal tiene pasos numerados y un código HTTP de éxito coherente
      (200/201/204)?
- [ ] ¿Cada flujo alternativo referencia el paso exacto del flujo principal?
- [ ] ¿Se nombraron las reglas de negocio (RN-XX) que intervienen?
- [ ] ¿Se distingue validación (Presentación → 400) de verificación (Negocio → 404/409)?
- [ ] ¿Las poscondiciones describen el estado persistente y el impacto en la visibilidad?
- [ ] ¿Cada flujo (principal y alternativos) tiene su test unitario y su test de integración
      asociado?