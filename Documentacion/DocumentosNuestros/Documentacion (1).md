

Índice

[**Introducción	3**](#introducción)

[**Desarrollo	4**](#desarrollo)

[Requerimientos del negocio	4](#requerimientos-del-negocio)

[Situación actual o Propósito	4](#situación-actual-o-propósito)

[Oportunidad del negocio	4](#oportunidad-del-negocio)

[Riesgos	5](#riesgos)

[Visión de la Solución	6](#visión-de-la-solución)

[Funciones principales	6](#funciones-principales)

[Contexto del Negocio	7](#contexto-del-negocio)

[Perfil de los interesados (Stakeholders)	7](#perfil-de-los-interesados-\(stakeholders\))

[Alcance y limitaciones	8](#alcance-y-limitaciones)

[Alcance inicial (MVP \- Minimum Viable Product)	8](#alcance-inicial-\(mvp---minimum-viable-product\))

[Objetivo del MVP	9](#objetivo-del-mvp)

[Limitaciones y exclusiones (Out of Scope)	9](#limitaciones-y-exclusiones-\(out-of-scope\))

[Requerimientos	10](#requerimientos)

[Requerimientos Funcionales	10](#requerimientos-funcionales)

[Requerimientos No Funcionales	11](#requerimientos-no-funcionales)

[**Conclusión	13**](#heading)

# **Introducción**

El presente proyecto consiste en el desarrollo de un sistema integral de gestión de reservas diseñado para centralizar y automatizar la operativa diaria de un establecimiento gastronómico. El objetivo primordial es sustituir los métodos manuales y fragmentados, como anotaciones en papel o mensajes de WhatsApp, por una solución digital que permita administrar de forma eficiente el mapa de mesas, las franjas horarias y la demanda de clientes en tiempo real. Al actuar como un "empleado digital", el sistema optimiza el flujo de clientes y garantiza una organización superior en el servicio.

La implementación de este software busca maximizar la rentabilidad y la eficiencia operativa mediante la reducción de errores humanos, tales como la sobreasignación de mesas o el solapamiento de turnos. A través de un Producto Mínimo Viable (MVP), se establece una base tecnológica sólida y escalable que permite gestionar procesos críticos como la lista de espera y el control de disponibilidad, mejorando significativamente la experiencia del cliente y sentando las bases para futuras actualizaciones comerciales inteligentes.

# **Desarrollo**

## Requerimientos del negocio

### *Situación actual o Propósito*

Actualmente, el restaurante gestiona sus reservas de manera manual, principalmente a través de WhatsApp, llamadas telefónicas y anotaciones en papel. Estas reservas son realizadas de manera manual por el personal del restaurante (recepción, encargados o dueños). 

Esta gestión puede estar centralizada en una sola persona o distribuida entre varios miembros del equipo. En el primer caso, se generan demoras en la atención y una alta dependencia operativa. En el segundo, la falta de un sistema unificado incrementa el riesgo de descoordinación, errores en la asignación de mesas y superposición de reservas. 

En la operación diaria, los encargados deben responder consultas de clientes, registrar reservas y organizar la asignación de mesas de forma manual. Esto genera diversos problemas, como la asignación incorrecta de mesas, superposición de reservas en un mismo horario y dificultades para administrar los turnos en momentos de alta demanda.

Además, el restaurante no cuenta con una lista de espera organizada. Cuando se producen cancelaciones o ausencias de clientes, las mesas muchas veces quedan sin ocupar, lo que representa una pérdida directa de ingresos. A su vez, no existe un control automatizado sobre los clientes que no se presentan a su reserva, lo que provoca ineficiencia en la rotación de mesas.

Otro inconveniente importante es la falta de reglas claras y automatizadas para la gestión del tiempo de ocupación de las mesas. Actualmente, no se controla de forma precisa la duración de cada reserva, lo que puede generar conflictos entre clientes y demoras en la asignación de nuevas reservas.

Por otro lado, el restaurante no dispone de herramientas para aplicar promociones específicas según el día y horario, ni de un sistema que permita a los encargados gestionar excepciones de manera controlada, como sobreasignar mesas en situaciones especiales. 

Actualmente, la gestión de reservas no cuenta con un responsable claramente definido y , a través de distintos canales como WhatsApp, llamadas telefónicas y anotaciones en papel. 

### *Oportunidad del negocio*

El desarrollo de un sistema de reservas propio representa una oportunidad clara para resolver los problemas operativos actuales del restaurante mediante la digitalización y automatización de sus procesos clave.

La solución propuesta consiste en una aplicación que permitirá gestionar reservas de manera centralizada, automatizando la asignación de mesas según capacidad, disponibilidad horaria y reglas de negocio específicas. De esta forma, el sistema actuará como un “empleado digital”, capaz de organizar el flujo de clientes en tiempo real sin intervención manual constante.

El valor principal del sistema radica en mejorar la eficiencia operativa y aumentar la rentabilidad del restaurante. Por un lado, se reducirá significativamente la cantidad de errores humanos, como la sobreasignación de mesas o el solapamiento de reservas. Por otro lado, se optimizará la ocupación mediante funcionalidades como la lista de espera automática, que permitirá reasignar mesas ante cancelaciones, y la gestión de “no-shows”, liberando recursos que de otro modo quedarían inutilizados.

Para lograr esto, el sistema registrará de forma automática cada interacción vinculada a las reservas, incluyendo la creación, modificación y cancelación de las mismas, así como el horario, la cantidad de comensales y el estado de asistencia del cliente. Además, se almacenará información sobre la asignación de mesas, tiempos reales de ocupación y eventos como cancelaciones tardías o ausencias.

A partir de estos datos, será posible generar estadísticas e indicadores clave, como tasa de ocupación, porcentaje de no-shows, tiempos promedio de permanencia y eficiencia en la rotación de mesas, permitiendo al restaurante tomar decisiones informadas y optimizar su operación.

Además, el sistema permitirá implementar estrategias comerciales inteligentes, como promociones condicionadas por día y horario, lo que contribuirá a aumentar el flujo de clientes en momentos menos concurridos. 

El entorno en el que operará este sistema es digital, accesible tanto desde dispositivos internos del restaurante como potencialmente desde interfaces de autogestión para clientes. Esto permitirá una operación continua, reduciendo la dependencia de la atención manual por parte del personal.

Si bien existen soluciones predeterminadas (SaaS) en el mercado para la gestión de reservas, estas no se adaptan completamente a las necesidades específicas del restaurante. En particular, presentan limitaciones a la hora de implementar reglas de negocio personalizadas, tales como: control de tiempos entre reservas, gestión de lista de espera, manejo de ausencias de clientes y aplicación de reglas específicas del negocio , manejo de excepciones mediante roles (como acciones especiales de gerentes con auditoría obligatoria) y la incorporación de promociones condicionadas directamente en la lógica del sistema.

En este sentido, el desarrollo de una solución a medida permite contar con un sistema totalmente alineado a la operatoria del restaurante, garantizando el control sobre los datos, mayor flexibilidad para evolucionar el sistema según nuevas necesidades y la posibilidad de escalar sin depender de costos recurrentes o restricciones impuestas por plataformas externas.

### *Riesgos*

**Riesgo 1: Resistencia al cambio por parte del personal del restaurante**  
(Severidad: Alta)  
El personal puede mostrar dificultades para adaptarse al uso de un sistema digital, especialmente si están acostumbrados a métodos manuales.

**Riesgo 2: Problemas de conectividad a internet**  
(Severidad: Media)  
Al tratarse de un sistema digital (posiblemente en la nube), una caída de internet podría impedir el acceso a las reservas en tiempo real.

**Riesgo 3: Errores en la implementación de reglas del sistema relacionadas con horarios, asignación de mesas y tiempos de espera**   
(Severidad: Alta)  
La lógica de reservas incluye condiciones críticas como solapamientos, tiempos de bloqueo, no-shows y lista de espera, lo que puede generar errores si no se implementa correctamente.

**Riesgo 4: Sobrecarga del sistema en horarios pico**  
(Severidad: Media)  
En momentos de alta demanda, múltiples reservas simultáneas podrían generar problemas de múltiples reservas al mismo tiempo podrían generar lentitud o errores en el sistema 

**Riesgo 5: Mala configuración inicial del sistema**  
(Severidad: Media)  
Errores en la carga inicial de mesas, capacidades o franjas horarias pueden afectar el funcionamiento desde el inicio.

**Riesgo 6: Dependencia del sistema sin respaldo manual**  
(Severidad: Baja)  
Una adopción total del sistema sin alternativas puede generar problemas ante fallas inesperadas.

**Riesgo 7: Falta de adopción por parte de los clientes**  
(Severidad: Media)  
Los clientes pueden seguir prefiriendo WhatsApp o llamadas.

**Riesgo 8: Integración futura con otros sistemas**  
(Severidad: Baja)  
Dificultades al integrar con sistemas de facturación o gestor de clientes.

**Riesgo 9: Pérdida o errores en la información**   
(Severidad: Alta)  
Errores en el funcionamiento del sistema o cortes de energía que causen la pérdida de pedidos activos o registros de caja. 

**Riesgo 10: Desincronización en los tiempos de reserva y ocupación de mesas**  
(Severidad: Alta)  
Una mala configuración o implementación de los tiempos puede generar inconsistencias, como liberar mesas antes de tiempo o impedir nuevas reservas cuando sí hay disponibilidad. Esto impacta negativamente directo en la rotación de mesas y en la experiencia del cliente.

## Visión de la Solución

### *Funciones principales*

1. **Módulo de Gestión de Reservas**  
   Permite registrar, modificar y cancelar reservas de clientes, validando automáticamente disponibilidad, capacidad de mesas y reglas de negocio.  
2. **Sistema de Asignación Inteligente de Mesas**  
   Asigna mesas de forma automática según cantidad de comensales, horarios y ocupación, evitando solapamientos y optimizando el uso del espacio.  
3. **Gestión de Lista de Espera Automática**  
   Administra una cola de clientes en espera y reasigna mesas automáticamente ante cancelaciones o liberaciones. Inicialmente, el cliente se va a enterar de la asignación por medio del sistema en el apartado de notificaciones. //A futuro, esto se automatizará enviando notificaciones automáticas a través de WhatsApp.  
4. **Control de Estados de Reserva (Flujo de Vida)**  
   Gestiona los estados de una reserva (Reserva creada, confirmada, en curso, finalizada, cancelada, no-show), incluyendo transiciones automáticas según el tiempo.

5. **Reglas de Negocio Automatizadas**  
   Aplica condiciones como bloqueos de tiempo entre reservas, validaciones de capacidad, reglas de no-show y restricciones horarias.  
6. **Módulo de Gestión de Mesas**  
   Permite configurar el mapa de mesas, capacidades y disposición del restaurante.  
7. **Sistema de Roles y Permisos**  
   Define distintos niveles de acceso (ej: personal, gerente) incluyendo acciones especiales como overrides con auditoría.  
8. **Panel de Control Operativo**  
   Visualiza en tiempo real el estado del restaurante: mesas ocupadas, reservas próximas, lista de espera y disponibilidad.  
9. **Módulo de Promociones Inteligentes**  
   Aplica automáticamente beneficios o descuentos según día, horario o condiciones específicas de la reserva.

## Contexto del Negocio

### *Perfil de los interesados (Stakeholders)*

| Stakeholder | Beneficio y valor percibido | Actitudes | Funciones de interés mayor | Restricciones |
| :---: | :---: | :---: | :---: | :---: |
| **Dueño/ Administrador** | Aumentar rentabilidad y reducir mermas mediante el control total de ventas e inventario. | Muy interesado, pero exige seguridad de datos y bajo costo de mantenimiento. | Reportes de reservas. | Presupuesto limitado para licencias; requiere autonomía técnica. |
| **Personal de salón (mozos)** | Facilitar la toma de pedidos; eliminar desplazamientos físicos innecesarios a la cocina. | Miedo a que el sistema sea lento en hora pico; prefieren la agilidad del papel. | Carga rápida de comandos y estado de disponibilidad de mesas. | Deben usar dispositivos móviles o terminales de uso rudo y de fácil limpieza. |
| **Personal de cocina** | Organización visual de pedidos por orden de llegada; fin de comandas ilegibles. | Pragmáticos; no quieren interactuar mucho con pantallas por el ritmo de trabajo. | Panel de pedidos pendientes y gestión de estados de preparación. | Ambiente con calor, grasa y humedad; requiere interacción mínima (pocos clics). |
| **Cajero** | Realizar cierres de caja exactos en minutos y gestionar múltiples medios de pago. | Preocupación por la integridad de los cálculos y errores de redondeo. | Facturación, división de cuentas y reporte de cierre de jornada | El sistema debe ser infalible en la lógica aritmética y funcionar sin internet (LAN). |
| **Cliente** | Recibir el pedido con mayor rapidez y una cuenta clara | Alta expectativa de inmediatez; intolerancia a errores en la facturación | Visualización de la cuenta y opciones de pago ágiles. | El sistema debe ser invisible para ellos; solo perciben su eficiencia. |
| **Gerente/ Encargado** | Mejor control operativo del salón y mayor capacidad de tomar decisiones en tiempo real. | Positivo, pero exige control total del sistema. | Panel en tiempo real, gestión de reservas, overrides y lista de espera. | Necesita un sistema confiable y rápido, sin errores. |

## Alcance y limitaciones

### *Alcance inicial (MVP \- Minimum Viable Product)*

La versión 1.0 del sistema estará enfocada en resolver de forma simple, ordenada y eficiente la gestión de reservas del restaurante, permitiendo tener control desde el primer día sin necesidad de procesos complejos.

En esta primera etapa, el sistema incluirá:

* La **gestión de mesas del restaurante**, donde se podrán cargar, editar y eliminar mesas, definiendo su capacidad y disponibilidad.  
* La **gestión de reservas**, permitiendo registrar nuevas reservas, modificarlas o cancelarlas de forma rápida y sencilla.  
* Un control automático para asegurar que **la cantidad de personas de una reserva no supere la capacidad de la mesa**, evitando errores en la asignación.  
* La **asignación de mesas con control de horarios**, evitando que se generen reservas superpuestas. Cada mesa tendrá un tiempo de ocupación definido, lo que permite mantener organizada la rotación de clientes.  
* Un sistema básico de **lista de espera**, donde se podrán registrar clientes cuando no haya disponibilidad inmediata.  
* Un **acceso mediante usuario y contraseña** para el personal del restaurante, asegurando que la información esté centralizada y protegida.  
* Una vista clara y ordenada de las reservas del día, facilitando la organización del salón y la atención al cliente.

### **Objetivo del MVP**

El objetivo de esta primera versión es que el restaurante pueda:

* Organizar sus reservas sin errores ni confusiones  
* Tener control sobre la ocupación de sus mesas  
* Reducir tiempos de gestión y mejorar la atención al cliente  
* Contar con una base digital sólida sobre la cual seguir creciendo

Esta versión prioriza lo esencial: que el sistema funcione bien, sea fácil de usar y aporte valor inmediato en la operación diaria del negocio. A partir de aquí, se podrán incorporar mejoras y automatizaciones en futuras etapas.

### *Limitaciones y exclusiones (Out of Scope)*

Con el objetivo de lograr una implementación rápida, ordenada y enfocada en resultados concretos, en esta primera versión del sistema se priorizan únicamente las funcionalidades esenciales para la gestión de reservas.

Por este motivo, algunas funcionalidades más avanzadas o complementarias no serán incluidas en esta etapa inicial, aunque podrán evaluarse e incorporarse en futuras versiones.

En esta entrega no se incluirá:

1. **Registro de auditoría (historial de acciones)**  
   El sistema no guardará un historial detallado de acciones realizadas por los usuarios (como cambios, cancelaciones o decisiones manuales).  
2. **Integración con sistemas externos**  
   No habrá conexión con sistemas de facturación, caja, delivery, ni otras plataformas externas.  
3. **Notificaciones automáticas a clientes**  
   No se enviarán recordatorios de reservas por WhatsApp, email u otros medios.  
4. **Mapa visual interactivo del salón**  
   La gestión de mesas se realizará mediante listados, sin una representación gráfica o interactiva del espacio.  
5. **No habrá reservas desde app móvil nativa**   
6. **No habrá integración con redes sociales (Instagram, Facebook)**   
7. **No se podrán cobrar reservas anticipadas**   
8. **Programas de fidelización (Puntos)**   
   No habrá módulos para acumular puntos, gestión de "clientes frecuentes" ni cupones de descuento personalizados.   
9. **El sistema estará disponible solo en español**  
10. **Autenticación de usuarios**  
    Tanto para clientes, como para colaboradores del restaurante, el acceso al sistema se realizará exclusivamente mediante correo electrónico y contraseña. No se integrarán métodos de autenticación adicionales como biometría (huella digital, reconocimiento facial) 

**Enfoque de esta decisión**

Estas exclusiones permiten concentrar el desarrollo en una solución funcional, estable y fácil de implementar, evitando demoras innecesarias.

El sistema estará preparado para evolucionar, incorporando estas funcionalidades en etapas futuras según las necesidades del negocio y el crecimiento del restaurante.

## Requerimientos

### *Requerimientos Funcionales*

Los siguientes requerimientos describen las funcionalidades que el sistema debe cumplir para gestionar de forma eficiente las reservas del restaurante.

**Gestión de Mesas**

* **RF-01:** El sistema debe permitir registrar una nueva mesa indicando su capacidad máxima de personas.  
* **RF-02:** El sistema debe permitir modificar los datos de una mesa existente.  
* **RF-03:** El sistema debe permitir eliminar una mesa del sistema.  
* **RF-04:** El sistema debe mostrar el listado de mesas disponibles con su respectiva capacidad.  
* **RF-05:** El sistema debe permitir cambiar el estado de una mesa (Libre, ocupada, reservada)

**Gestión de Reservas**

* **RF-06:** El sistema debe permitir crear una nueva reserva indicando nombre del cliente, cantidad de personas, fecha y horario.  
* **RF-07:** El sistema debe permitir modificar los datos de una reserva existente.  
* **RF-08:** El sistema debe permitir cancelar una reserva.  
* **RF-09:** El sistema debe mostrar un listado de reservas filtrado por fecha.  
* **RF-10**: El sistema debe permitir cambiar manualmente el estado de una reserva.  
* **RF-11**: El sistema debe permitir marcar una reserva como confirmada.


**Gestión de Disponibilidad**

* **RF-12:** El sistema debe verificar la disponibilidad de mesas en función de la fecha, horario y cantidad de personas.

**Lista de Espera**

* **RF-13:** Ante una cancelación de reserva, el sistema debe asignar automáticamente una mesa a un cliente en lista de espera.

**Acceso al Sistema**

* **RF-14:** El sistema debe permitir a los usuarios iniciar sesión mediante mail y contraseña.  
* **RF-15:** El sistema debe permitir a los usuarios registrarse mediante nombre, mail y contraseña.

**Visualización y Operación Diaria**

* **RF-16:** El sistema debe mostrar una vista general de las reservas del día.

**Gestión de clientes**

* **RF-17**: El sistema debe permitir registrar los datos de contacto del cliente.

**Filtros y búsquedas**

* **RF-19**: El sistema debe permitir filtrar reservas por múltiples criterios.

**Roles**

* **RF-20**: El sistema debe restringir funcionalidades según rol.


### *Requerimientos No Funcionales*

Los siguientes requerimientos definen cómo debe comportarse el sistema en términos de rendimiento, seguridad, usabilidad y calidad general, asegurando una experiencia confiable y eficiente para el restaurante.

**Seguridad**

* **RNF-01:** El sistema debe garantizar que el acceso esté protegido mediante usuario y contraseña.  
* **RNF-02:** Todas las contraseñas deben almacenarse de forma encriptada en la base de datos.  
* **RNF-03:** El sistema debe impedir el acceso a usuarios no autenticados.  
* **RNF-04:** La información de reservas y clientes debe ser accesible únicamente para usuarios autorizados.

**Rendimiento**

* **RNF-05:** El sistema debe garantizar tiempos de respuesta adecuados para asegurar una experiencia de usuario fluida. En particular:  
  Las búsquedas simples (como consulta de disponibilidad de mesas) deben responder en menos de **300 ms**.  
  Las operaciones estándar (como listado de reservas o carga de información) deben responder en menos de **500 ms**.  
  Estos valores deberán cumplirse al menos en el **95% de las solicitudes**, considerando un uso concurrente habitual del sistema.  
* **RNF-06:** El sistema debe ser capaz de gestionar múltiples reservas simultáneamente sin generar errores ni inconsistencias.  
* **RNF-07:** La asignación de mesas debe realizarse de forma inmediata al crear una reserva.

**Usabilidad**

* **RNF-08:** El sistema debe permitir que un usuario pueda crear una reserva completa en un máximo de 5 pasos y sin necesidad de conocimientos técnicos previos.   
* **RNF-09:** La información debe mostrarse de forma clara y ordenada para facilitar la operación diaria.

**Disponibilidad**

* **RNF-10:** El sistema debe estar disponible durante el horario operativo del restaurante sin interrupciones.  
* **RNF-11:** En caso de error, el sistema debe mostrar mensajes claros que permitan al usuario entender qué ocurrió.

**Escalabilidad**

* **RNF-12:** El sistema debe estar preparado para incorporar nuevas funcionalidades en el futuro sin necesidad de rediseñar completamente.  
* **RNF-13:** La estructura del sistema debe permitir agregar módulos como promociones, notificaciones o reportes en futuras versiones.

**Arquitectura y Desarrollo**

* **RNF-14:** El sistema debe estar organizado de forma clara, permitiendo realizar cambios o mejoras sin afectar el funcionamiento general. 

# 

