# Resumen de Evaluación Integral del Proyecto

**Proyecto:** Grupo N°1 — Sistema de Reserva de Restaurantes
**Autores:** Martín Cagliero y Jerónimo Benassi
**Rol del evaluador:** Arquitecto de Software Senior / Analista Funcional / Auditor QA
**Alcance:** Documentación de negocio, 23 casos de uso (CU-01 a CU-23), código fuente (`src/`) y pruebas (`tests/`).

---

## 1. Visión General

La entrega demuestra un **SDLC con una base metodológica sólida y una madurez despareja**: el análisis de negocio y la especificación de casos de uso están muy por encima del promedio, mientras que la implementación y la verificación presentan brechas importantes entre lo documentado y lo construido.

| Fase | Nivel observado | Comentario general |
| --- | --- | --- |
| Análisis / Requerimientos | **Alto** | Documento de negocio completo (stakeholders, riesgos, MVP, 19 RF y 14 RNF). |
| Diseño / Casos de Uso | **Alto (en forma), medio (en exactitud)** | 23 CU con pre/postcondiciones, flujos alternativos, códigos HTTP y matrices de trazabilidad. Excelente formato, pero varias matrices referencian endpoints, clases y tests que no existen. |
| Implementación | **Medio** | Arquitectura por capas correcta y bien intencionada; cobertura funcional parcial y algunos flujos *core* ausentes. |
| Verificación / QA | **Bajo-Medio** | 50 pruebas unitarias reales (Moq/AAA) y ninguna prueba de integración, pese a que los CU afirman que existen. |
| Estado de ejecución | **Bloqueante** | La solución **no compila actualmente** por un conflicto de versiones NuGet (ver §4). |

**Diagnóstico en una frase:** *excelente diseño documental y arquitectura prometedora, con una implementación que aún no alcanza lo prometido y un pipeline de calidad que hoy no puede ejecutarse.*

---

## 2. Fortalezas Identificadas

1. **Arquitectura en capas clara y escalable.** Separación real en `Api` (presentación), `BusinessLogic` (dominio/aplicación), `DataAccess` (persistencia), `Shared` (contratos) y `Migrations`. Responde bien a RNF-12/RNF-13/RNF-14.
2. **Inyección de dependencias e inversión de control.** Interfaces de servicio y repositorio (`IReservaService`, `IMesaRepository`, etc.) con registro centralizado en `DependencyInjection.cs`. Favorece DIP (SOLID) y testeabilidad.
3. **Manejo centralizado de errores.** `GlobalExceptionHandler` con `ProblemDetails` (RFC 7807) y mapeo explícito excepción → código HTTP. Es una práctica profesional y cumple parcialmente RNF-11.
4. **Modelo de dominio razonable.** Enums de estado (`ReservaEstado`, `MesaEstado`), entidades con relaciones y `DeleteBehavior.Restrict`, DTOs de entrada/salida separados de las entidades.
5. **Seguridad base correcta.** Autenticación JWT Bearer, autorización por roles con `[Authorize(Roles = ...)]`, *hashing* de contraseñas con BCrypt y claims de identidad/rol.
6. **Documentación de casos de uso sobresaliente en formato.** Precondiciones, flujo principal, flujos alternativos, códigos HTTP, postcondiciones, nota "Validación vs. Verificación" y matriz CU → test. Es exactamente el nivel de detalle que se espera de un analista funcional.
7. **Cultura de pruebas unitarias.** 50 casos con patrón AAA y aislamiento por Moq, cubriendo reglas de negocio de mesas, reservas y usuarios.
8. **Detalles de producto valiosos.** `ReservaNoShowWorker` (liberación automática de mesas), auto-asignación de la mesa de menor capacidad suficiente y salvaguardas de gobernanza (no eliminar al último admin).
9. **Seed de datos y observabilidad.** Inicializador con datos demo y logging estructurado en el worker y el middleware.

---

## 3. Análisis de Brechas (Gap Analysis)

### 3.1 Trazabilidad Requerimiento Funcional → Caso de Uso → Código

| RF | Descripción | CU asociado | Estado real en código |
| --- | --- | --- | --- |
| RF-01 | Registrar mesa | CU-21 | ✅ Implementado (sin `Ubicacion` ni validaciones de capacidad) |
| RF-02 | Modificar mesa | CU-22 | ⚠️ Parcial: solo actualiza `Capacidad` y `Activa`; ignora número/ubicación y no valida RN-03 |
| RF-03 | Eliminar mesa | CU-23 | ⚠️ Parcial: bloquea si existe **cualquier** reserva (incluida histórica); sin confirmación |
| RF-04 | Listar mesas disponibles | CU-10 | ⚠️ Parcial: filtro devuelve 404 cuando no hay coincidencias |
| RF-05 | Cambiar estado de mesa | CU-03 | ✅ Implementado |
| RF-06 | Crear reserva | CU-01 | ✅ Implementado |
| RF-07 | **Modificar reserva** | — | ❌ **No implementado** (existe `ReservaUpdateDTO` sin endpoint ni servicio) |
| RF-08 | Cancelar reserva | CU-07 / CU-15 | ⚠️ Parcial: cancelación de gerente sin motivo obligatorio |
| RF-09 | Listar reservas por fecha | CU-06 / CU-11 | ✅ Implementado (rutas distintas a las documentadas) |
| RF-10 | **Cambiar estado manual de reserva** | — | ❌ **No implementado** (`ReservaCambiarEstadoDTO` sin uso) |
| RF-11 | Confirmar reserva | CU-12 | ⚠️ Parcial: no revalida mesa/solapamiento (RN-03 del CU) |
| RF-12 | Verificar disponibilidad | CU-01 / CU-17 | ⚠️ Embebido, sin caso de uso dedicado |
| RF-13 | **Reasignación automática desde lista de espera** | CU-18 | ❌ **No implementado**: solo se listan reservas `Pendiente`; no hay promoción ni reasignación |
| RF-14 | Iniciar sesión | CU-05 | ✅ Implementado |
| RF-15 | Registrarse | CU-04 | ⚠️ Parcial: no existe `POST /api/auth/register`; se cubre vía `ClientesController` |
| RF-16 | Vista general del día | CU-02 | ⚠️ Parcial: solo lectura; "gestionar" no tiene acciones |
| RF-17 | Registrar datos de contacto | CU-04 | ⚠️ Parcial (vía alta de cliente) |
| RF-18 | — | — | ⚠️ **Hueco de numeración**: el requerimiento no existe en el documento |
| RF-19 | Filtrar reservas multi-criterio | CU-11 | ✅ Implementado (404 en vacío) |
| RF-20 | Restringir por rol | Transversal | ✅ Implementado con `[Authorize]` |

**Casos de uso huérfanos (sin RF que los justifique):**
- **CU-08 (Cerrar sesión):** no hay RF de *logout*; además el servicio es un *no-op*.
- **CU-09 (Volver a pantalla inicial):** no hay RF ni implementación (`UsuarioController` inexistente).
- **CU-20 (Eliminar usuarios):** no hay RF explícito de baja de usuarios.
- **CU-02 (Gestionar reserva):** se justifica solo parcialmente con RF-16.

**Requerimientos huérfanos (sin CU):** RF-07, RF-10 y RF-13 (este último tiene CU pero no implementación real).

### 3.2 Mapeo de Actores / Stakeholders

| Stakeholder del documento de negocio | Rol en el código | Estado |
| --- | --- | --- |
| Dueño/Administrador | `Admin` | ✅ Mapeado |
| Gerente/Encargado | `Gerente` | ✅ Mapeado |
| Cliente | `Cliente` | ✅ Mapeado |
| Personal de salón (mozos) | — | ❌ Sin rol ni CU |
| Personal de cocina | — | ❌ Sin rol ni CU (además sus funciones —comandas— están fuera del MVP) |
| Cajero | — | ❌ Sin rol ni CU (facturación/caja están fuera de alcance) |

**Conclusión:** hay **stakeholders huérfanos** y una confusión de alcance: el documento describe funciones de cocina/caja/facturación que no forman parte del MVP ni de los RF. Deben reclasificarse explícitamente como *out of scope* o eliminarse del perfil de interesados.

### 3.3 Reflejo de los RNF en pre/postcondiciones de los CU

| RNF | ¿Reflejado en los CU? | Observación |
| --- | --- | --- |
| RNF-01/03/04 (seguridad de acceso) | Parcial | Se exige JWT/rol en precondiciones, pero la **propiedad del recurso** (RNF-04) no se verifica en código (IDOR). |
| RNF-02 (contraseñas cifradas) | Parcial | BCrypt presente, pero existe *fallback* a texto plano. |
| RNF-05 (latencias <300/500 ms, 95%) | ❌ No | Ningún CU define tiempos medibles ni pruebas de performance. |
| RNF-06 (concurrencia sin inconsistencias) | ❌ No | No hay control de concurrencia ni transacciones. |
| RNF-07 (asignación inmediata) | Parcial | Implícito en CU-01, sin postcondición medible. |
| RNF-08 (reserva en ≤5 pasos) | ❌ No | No se especifica en ningún CU. |
| RNF-09 (claridad de información) | ❌ No | No hay criterios verificables. |
| RNF-10 (disponibilidad en horario operativo) | ❌ No | No se traduce en pre/postcondiciones. |
| RNF-11 (mensajes claros) | ✅ Parcial | Tablas de códigos HTTP + `ProblemDetails`. |
| RNF-12/13 (escalabilidad) | ⚠️ Arquitectura | La arquitectura lo soporta, pero no hay CU que lo verifique. |
| RNF-14 (organización del sistema) | ✅ | Capas e interfaces claras. |

**Hallazgo clave:** los RNF de rendimiento, concurrencia y disponibilidad **no están operacionalizados** en los casos de uso ni en las pruebas. Son declaraciones aspiracionales sin criterio de aceptación.

### 3.4 Diseño vs. Código: discrepancias de contrato

| CU | Diseño documentado | Implementación real | Impacto |
| --- | --- | --- | --- |
| CU-02 | `GET /api/gerente/reservas`, `GerenteReservaService` | `GET /api/v1/reservas/lista-reservas`, `ReservaService` | Clases/endpoint inexistentes |
| CU-04 | `POST /api/auth/register`, `UsuarioRegisterDTO`, `AuthService.RegisterAsync` | No existe; alta vía `ClientesController` | Flujo y contrato distintos |
| CU-06 | `GET /api/cliente/reservas?fecha=` | `GET /api/v1/reservas/fecha/{fecha:datetime}` | Contrato distinto |
| CU-09 | `GET /api/usuario/contexto-inicio`, `UsuarioController` | No existe | Funcionalidad ausente |
| CU-12 | `PATCH .../confirmar` | `PUT .../confirmar` | Verbo HTTP distinto |
| CU-15 | `PATCH /api/gerente/reservas/{id}/cancelar` + `motivo` | `PATCH /api/v1/reservas/gerente/{id}/cancelar` sin cuerpo | Motivo no implementado |
| CU-16 | `PATCH .../rechazar` + `motivoRechazo` | `PATCH /api/v1/reservas/{id}/rechazar` sin cuerpo; estado `Cancelada` | Motivo y estado `Rechazada` ausentes |
| CU-17 | `mesaId` (ID de mesa) | `GetByNumeroAsync(mesaId)` (número de mesa) | Semántica confusa/incorrecta |
| CU-18 | `ListaEsperaController`, `ListaEsperaService`, `PATCH .../promover` | Solo `GET .../lista-espera` | Núcleo no implementado |
| CU-21/22 | `[Required]`, `[Range(1,30)]`, sector/ubicación | DTOs sin validaciones; sin `Ubicacion` | Contrato incompleto |
| CU-19/20 | Revocación de tokens/auditoría | JWT *stateless* sin revocación; sin auditoría | Seguridad y trazabilidad |

Además, los CU referencian excepciones que **no existen** en el código (`EmailDuplicadoException`, `CuentaInactivaException`, `EntidadNoEncontradaException`, `NumeroMesaDuplicadoException`, `MesaNoCompatibleException`, `OperacionInvalidaException`, `CapacidadIncompatibleReservaException`). El código usa `ConflictException`/`NotFoundException`/`ValidationException`. La nomenclatura debe unificarse en ambos sentidos.

### 3.5 Scope Creep (lógica no documentada)

- **`ReservaNoShowWorker`**: transición automática a `NoShow` y liberación de mesa cada minuto. Es valiosa, pero **no tiene RF ni CU** que la respalde y no se menciona en las matrices.
- **`ClientesController.GetById`** y **`POST /api/v1/Clientes`**: endpoints sin caso de uso asociado; el `GET` es además anónimo.
- **Auto-selección de mesa óptima** (menor capacidad suficiente): comportamiento no especificado en CU-01.
- **Salvaguarda "no eliminar/despojar al último admin"** en baja de usuarios: no está en CU-20 (sí en CU-19).
- **`NoShow`** existe en el enum pero no hay flujo documentado que lo produzca desde la API.

### 3.6 Verificación (QA)

- Existen **50 pruebas unitarias** (13 mesas, 26 reservas, 11 usuarios) con Moq.
- **No existe ninguna prueba de integración HTTP**, aunque las 23 matrices de trazabilidad listan nombres de tests de integración (p. ej. `SolicitarReserva_Returns201Created`).
- Los **nombres de tests documentados no coinciden** con los implementados (p. ej. `SolicitarReservaAsync_WithValidData_ReturnsCreatedReserva` vs. `SolicitarReservaAsync_ConDatosValidos_CreaReservaYRetornaDTO`).
- **No hay pruebas** para `AuthService`, `ClienteService` ni para el worker de no-show.
- Consecuencia: la trazabilidad CU → test es **declarativa, no verificable**. Es el hallazgo de QA más relevante después del build roto.

---

## 4. Conclusión de Arquitectura y Calidad

### 4.1 Estado de compilación y ejecución

La solución **no compila** en el estado actual del repositorio:

```
error NU1605: Degradación del paquete detectada:
Microsoft.Extensions.Configuration.Abstractions de 10.0.14 a 10.0.13
```

Causa: `BusinessLogic.csproj` fija `Microsoft.Extensions.Configuration.Abstractions 10.0.13`, mientras que `DataAccess` usa `Microsoft.EntityFrameworkCore.Sqlite 10.0.*`, que hoy resuelve `10.0.14` y exige `>= 10.0.14`. Al ser un proyecto con *warnings as errors*, el build se detiene. **Es un bloqueante de entrega** y debe resolverse antes de cualquier otra tarea.

Adicionalmente, la migración `InitialCreate` **no incluye** las columnas `PasswordHash`, `Rol` ni `Activo` de `Clientes`, porque fue generada antes de esos campos. Como `DbInitializer` usa `EnsureCreatedAsync()` (ignorando migraciones), el esquema real y el migrado divergen; un `dotnet ef database update` produciría una base sin autenticación funcional.

### 4.2 Prácticas de Clean Code y diseño

**Positivo**
- Separación de responsabilidades, DTOs, interfaces y DI: buena aplicación de SRP/DIP.
- Excepciones de dominio y mapeo centralizado: diseño limpio y consistente.
- Nombres de clases y métodos mayormente expresivos; tests en AAA.

**A mejorar**
- **Métodos de servicio con múltiples responsabilidades** (p. ej. `SolicitarReservaAsync` valida horario, cliente, duplicados, busca mesa, valida solapamiento y persiste). Conviene extraer políticas de negocio (Strategy/Policy) y un `IUnitOfWork`.
- **Ausencia de transacciones** en operaciones que tocan `Reservas` y `Mesas` a la vez: riesgo de inconsistencia ante fallo parcial (viola RNF-06).
- **Acoplamiento de `BusinessLogic` a `DataAccess.Entities`**: los servicios devuelven/reciben entidades y DTOs mezclados; convendría un mapeo explícito o AutoMapper y modelos de dominio.
- **Repositorios con `AsNoTracking` + `Update` manual**: patrón frágil; usar entidades *tracked* en escrituras o un contexto por unidad de trabajo.
- **Nombres/espacios inconsistentes**: `MesaService` vive en `BusinessLogic/Reservas/` con namespace `BusinessLogic.Mesas`; `UsuarioService` en `Reservas/` con namespace `Usuarios`.
- **Código muerto**: `AccesoDenegadoException.cs` vacío; `ReservaUpdateDTO`, `ReservaCreateDTO`, `ReservaCambiarEstadoDTO`, `ClienteUpdateDTO` sin uso; `Api.http` con `weatherforecast`.

### 4.3 Seguridad y calidad de datos

- 🔴 **IDOR en cancelación y consulta de reservas**: `CancelarReservaClienteAsync` recibe `clienteId` pero **nunca lo compara** con `reserva.ClienteId`; `GetByIdAsync` tampoco valida propiedad. Cualquier usuario autenticado puede cancelar/ver reservas ajenas.
- 🔴 **`ClientesController` sin `[Authorize]`**: expone datos personales (email/teléfono) por GUID y permite altas anónimas.
- 🟠 **JWT**: secreto *hardcodeado* como fallback, expiración de desarrollo de 43 200 minutos (30 días) y **sin revocación** al cambiar rol; un admin degradado conserva privilegios hasta que expire el token.
- 🟠 **Contraseñas**: *fallback* a comparación en texto plano si el hash no es BCrypt (contradice RNF-02); los 4 clientes semilla no tienen `PasswordHash`, por lo que no pueden iniciar sesión.
- 🟠 **Concurrencia**: patrón *check-then-act* sin transacción ni restricción única `(MesaId, FechaHora)`; dos solicitudes simultáneas pueden reservar la misma mesa.
- 🟢 Validaciones de entrada incompletas (`MesaCreateDTO`, `MesaUpdateDTO`, `MesaCambiarEstadoDTO`) y `[Required]` inefectivo sobre tipos de valor (`DateTime`, `int`).

### 4.4 Veredicto

El proyecto tiene **fundaciones de ingeniería correctas y una documentación funcional admirable**, pero la distancia entre lo documentado y lo ejecutable es hoy demasiado grande: build roto, migración desincronizada, flujos *core* ausentes (modificar reserva, cambio manual de estado, lista de espera real, registro/contexto), controles de acceso incompletos y una estrategia de pruebas que promete integración inexistente.

Con un esfuerzo acotado y priorizado (ver `issues_github_pendientes.md`), el proyecto puede pasar de "prototipo con buena arquitectura" a "MVP defendible": primero restaurar el build y el esquema, luego cerrar los huecos de seguridad y los RF faltantes, y finalmente alinear documentación y pruebas con la realidad del código.

**Recomendación de priorización:** 🔴 build + migración + IDOR + RF-07/RF-10/RF-13 → 🟡 consistencia de contratos y transacciones → 🟢 deuda técnica y nomenclatura.
