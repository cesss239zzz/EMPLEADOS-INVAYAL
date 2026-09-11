# Revisión técnica de EMPLEADOS-INVAYAL / SIGEM

**Fecha:** 11 de septiembre de 2026.
**Responsable del proyecto:** Cesar Portillo.
**Origen:** `EMPLEADOS-INVAYAL-main.zip`, exportación de GitHub con identificador `813cd6692bcee94a6b34e86adeb00d393611ad5d`.
**Estado de esta entrega:** compilación de Windows y 11 grupos de regresión verificados el 11 de septiembre de 2026; validación visual y recorrido manual pendientes.

## Verificación posterior en Windows — 11 de septiembre de 2026

Se comparó el ZIP corregido con `main` en `813cd6692bcee94a6b34e86adeb00d393611ad5d`. Contiene 20 archivos existentes modificados y 3 nuevos, sin archivos eliminados. Se conservaron las migraciones históricas y los recursos visuales; el único cambio XAML es el enlace del total de colaboradores en `PanelResumen.xaml`.

Se corrigió un error CS4007 en la prueba de documentos: se esperan por separado la lectura del contenido guardado y la del archivo original antes de comparar sus bytes. Esto evita conservar un `ReadOnlySpan<byte>` a través de un `await` y mantiene la comprobación del contenido real.

Comandos ejecutados desde la raíz del repositorio, con .NET SDK 10.0.301 y MAUI Windows instalado:

```text
dotnet build empleados/empleados.csproj -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet run --project pruebas/Sigem.Regresion/Sigem.Regresion.csproj
Resultado: 11/11 grupos correctos.
```

Ambos comandos terminaron con código 0. Las pruebas usan bases SQLite temporales y no abren la base de datos del usuario. No se ejecutó un recorrido visual de las pantallas ni una restauración funcional sobre una base real; esas comprobaciones continúan pendientes. Las notas de la revisión inicial que siguen describen su estado anterior a esta verificación.

## Resultado y límites

El proyecto recibido es una aplicación **.NET MAUI para Windows**, con MVVM, EF Core 9.0.19 y SQLite. No contiene vistas `.cshtml` ni controladores ASP.NET. El proyecto de aplicación apunta a `net10.0-windows10.0.19041.0` y `win-x64`.

Se revisaron los servicios de empleados, fichas, empresas, catálogos, documentos, novedades, alertas, resumen, autenticación, reportes y respaldos; sus entidades, acceso a datos, registros de dependencias y los flujos principales del ViewModel. Se inspeccionaron los enlaces del dashboard y las secciones pendientes. La revisión es de código fuente, no una auditoría de una instalación en funcionamiento.

Se modificaron **20 archivos existentes**. Se añadieron este informe y un proyecto de regresión que enlaza el código real de los servicios. Los cambios no requieren nuevas columnas ni alteran las migraciones históricas. No se modificaron estilos, recursos, distribución o controles de las pantallas. La única modificación de XAML es un enlace de datos del total del dashboard.

No se accedió a una base real ni se subieron cambios a GitHub. La conexión a GitHub continúa sin habilitarse. Esta entrega debe revisarse y compilarse antes de utilizarse con datos de producción. No representa una garantía de ausencia de errores.

## Hallazgos corregidos en el código

Todos los estados «corregido» de esta tabla significan **cambio escrito y revisado estáticamente**, con verificación funcional aún pendiente.

| ID / prioridad | Hallazgo original e impacto | Cambio realizado / ubicación |
|---|---|---|
| C01 / alta | El perfil SupervisorSucursal tenía `SucursalId`, pero las consultas de empleados y sus hijos solo filtraban empresa. Era posible consultar fichas, archivos y avisos de otra sucursal. | Filtros globales de empresa, sesión y sucursal en `Datos/ContextoRhManager.cs`, también para consultas directas a documentos, bytes, novedades e historial. Supervisor sin sucursal obtiene cero filas. |
| C02 / alta | El contexto leía un singleton mutable de empresa en cada consulta; una operación en curso podía cambiar de alcance. | Cada DbContext conserva empresa, autenticación y sucursal al crearse. La fábrica de diseño recibe una sesión vacía. Probar traducción EF y separación entre contextos con la misma caché del modelo. |
| C03 / alta | `ObtenerParaEdicionAsync` devolvía salario sin exigir permiso de captura. | Rechaza la lectura del DTO de edición para perfiles no autorizados, aunque se invoque el servicio directamente. `ServicioColaboradores.cs`. |
| C04 / alta | Los ID de sucursal, departamento y puesto enviados al guardado no se comprobaban contra la empresa activa. Una clave foránea simple no garantiza pertenencia a la misma empresa. | Verificación de pertenencia por consulta filtrada antes de guardar. Se conservan los catálogos opcionales y la independencia puesto/departamento del diseño existente. |
| C05 / alta | Las validaciones de empleados estaban principalmente en el formulario; invocar el servicio permitía datos inválidos. | El servicio valida obligatorios, tamaños, fechas, enumeraciones, correo, teléfono y rango salarial. Los errores de unicidad SQLite se distinguen de otros errores de base. |
| C06 / funcional | Altas y cambios laborales no generaban movimientos: el historial podía quedar vacío o desactualizado. | Alta y cambios de puesto, salario, sucursal y estado crean movimientos en el mismo guardado. Salarios redondeados a centavos; guardados sin cambios no duplican movimientos. Autor en la observación. No se inventa una fecha de salida efectiva. |
| C07 / alta | Se podían duplicar o solapar vacaciones, incluso compartiendo un día de inicio/fin. | Verificación inclusiva de cruces para la misma persona, excluyendo canceladas; validación de colaborador activo y fecha de ingreso. Comprobación e inserción dentro de una transacción. Se mantiene el cómputo existente de días calendario. |
| C08 / media | Incidencias aceptaban tipo inválido, fecha vacía/futura o textos excesivos desde el servicio. | Validaciones de tipo, fecha y longitudes. Manejo de descripción/observación nulas en novedades. |
| C09 / alta | Documento y contenido se guardaban en dos `SaveChanges` independientes. Un fallo del segundo dejaba metadatos sin archivo o incongruentes con él. | Una transacción abarca metadatos, bytes, historial y actualización de avisos. Prueba preparada con fallo deliberado en el segundo guardado. |
| C10 / media | El servicio permitía omitir vencimiento en un tipo que lo exige. La eliminación de avisos y del documento no era atómica. | Validación de emisión/vencimiento y longitud de descripción; transacción de eliminación con historial y contenido. |
| C11 / dashboard | «Total colaboradores» mostraba únicamente activos. | En `PanelResumen.xaml`, el enlace ahora usa `Resumen.ColaboradoresRegistrados`; el pie sigue indicando cuántos están activos. No cambia el diseño. |
| C12 / dashboard | Las tarjetas próximas excluían vencidos y podían parecer sin pendientes pese a que existían documentos o contratos vencidos. | Conteos adicionales de vencidos en `ServicioResumen.cs`; pies y señales de atención en `IServicioResumen.cs`. El número principal mantiene su significado de próximos, y el pie distingue vencidos. Se incluye el último día completo de la ventana de 30 días. |
| C13 / media | Un cumpleaños o aniversario pasado se mostraba como vencimiento crítico. | Las efemérides son informativas; contrato, documento y período de prueba conservan clasificación operativa. Normalización del 29/02 en el texto de próximo cumpleaños. |
| C14 / media | Los contratos usaban número + fecha como clave del aviso, aunque dos contratos pueden compartir ambos datos. Quedaban avisos pendientes obsoletos. | Claves por ID de contrato; reconciliación de avisos actuales y obsoletos dentro de transacción; renovación actualiza el estado de avisos de otra fecha. Los ya resueltos no se reabren automáticamente. |
| C15 / media | La caché del motor no contemplaba el cambio de día ni modificaciones externas durante una sesión larga. | Recalcula al abrir Resumen o Alertas. Se elimina la caché y sus referencias en las clases parciales. No se ejecuta como precarga al inicio. |
| C16 / alta | Las copias contienen todas las empresas, pero el servicio no exigía SuperAdministrador para respaldar manualmente o restaurar. | Controles de perfil en el servicio y al abrir el apartado/carpeta desde el ViewModel. El respaldo diario de infraestructura conserva su funcionamiento anterior al acceso. |
| C17 / alta | Restauración con `File.Copy` sobre una base abierta, borrado manual de WAL/SHM y destino fijo ajeno a la conexión configurada. | Restauración mediante `SqliteConnection.BackupDatabase` hacia la conexión configurada. Mantiene respaldo previo; comprueba integridad, relaciones, tablas y conjunto de migraciones. Rechaza respaldos de versiones distintas. Hay que probar este flujo en Windows con una copia de trabajo. |
| C18 / media | Nombres de respaldo con precisión de segundos podían colisionar. El resumen de borrado de empresa exponía conteos sin restringir al perfil que borra. Quedaba historial documental huérfano tras borrar una empresa. | Sufijo único de respaldo; resumen de borrado reservado a SuperAdministrador; eliminación del historial documental dentro de la transacción de borrado de empresa. |
| C19 / alta | La página principal podía conservar filtros y datos de una empresa o usuario anterior si el Shell la reutilizaba. | Limpieza de ficha, colecciones, filtros, métricas y vista previa al cambiar identidad de sesión/empresa y al cerrar sesión; cancelación de la búsqueda pendiente. |
| C20 / media | El diagnóstico aconsejaba borrar la base y prometía recuperar datos de demostración, aunque el proyecto ya permite datos reales. Los catálogos de tipo admitían 120 caracteres, pero su modelo limita 80. | Instrucción de diagnóstico corregida para conservar la base; límites de catálogo coherentes. Se bloquea cancelar formularios mientras guardan. |

## Lo que realmente está implementado y lo que falta

| Área | Situación en esta entrega | Siguiente trabajo necesario |
|---|---|---|
| Acceso | BCrypt y bloqueo por intentos; sesión y perfiles. | `DebeCambiarContrasena` se carga, pero no hay flujo obligatorio de cambio. No está resuelto en esta entrega. Añadir cambio seguro, recuperación y administración de usuarios. |
| Empresas y catálogos | Consulta, creación, edición y controles de eliminación/desactivación. | Pruebas entre roles; revisar operaciones concurrentes y consistencia cuando se desactivan tipos con documentos existentes. |
| Empleados | Alta, edición, búsqueda, filtros y ficha; historial automático nuevo. | Paginación real; seguimiento de edición concurrente; fecha efectiva de baja/reingreso y cambios históricos. |
| Contactos de emergencia | Entidad y lectura en ficha. | Alta, edición y selección de contacto principal desde la aplicación. |
| Contratos | Entidad, lectura y alertas. | Alta, renovación, cierre, adjunto del contrato y validación de períodos. En un sistema vacío no hay un flujo normal completo para alimentarlos. |
| Documentos por persona | Adjuntar, editar, consultar, descargar, eliminar e historial. | Políticas de retención, validación del contenido real del archivo y tamaño después de lectura; flujo de aprobación de documentos. |
| Sección general Documentos | `PanelPendiente`, no un centro documental operativo. | Buscador global por colaborador/tipo/vencimiento, manteniendo los permisos. |
| Historial | Lectura por expediente y movimientos agregados en esta entrega. Sección global pendiente. | Historial global filtrable; auditoría estructurada de cambios en catálogos, usuarios y datos personales. |
| Vacaciones | Programación y listado; validación de cruces nueva. | Aprobar, cancelar y marcar gozadas desde la UI; saldo por período; calendario laboral y política de cómputo acordada con RR. HH. |
| Incidencias | Registro y consulta por colaborador. | Adjuntos, seguimiento, permisos por tipo y trazabilidad de correcciones. |
| Dashboard | Cuatro métricas reales, directorio y alertas; total y vencidos corregidos. | Expedientes pendientes por requisito, vacaciones actuales/próximas, movimientos de personal por período y filtros coherentes entre tarjeta y detalle. |
| Reportes | Exportación de directorio PDF/Excel, ficha PDF y constancia desde flujos existentes. Sección global aún pendiente. | Centro de reportes y criterios de consulta; no hay siete reportes completos, nómina ni QR verificable. |
| Notificaciones | Avisos dentro de la aplicación. | No se encontró integración operativa de notificaciones de Windows. |
| Respaldos | Copia diaria/manual, listado, purga y restauración; controles corregidos. | Verificar restauración en Windows, copias externas, acceso al archivo local, cifrado y una política de recuperación. |

## Riesgos pendientes prioritarios

1. **Cambio obligatorio de contraseña:** el indicador existe, pero no se impone. Debe implementarse antes de distribuir acceso real. No se cambió ni divulgó la contraseña inicial en esta entrega.
2. **Migraciones de una demo a datos reales:** `BaseLimpia` contiene borrados de datos sembrados; el arranque aplica migraciones automáticamente. La migración de documentos elimina la ruta anterior y crea almacenamiento de contenido, sin un proceso que importe los archivos antiguos. No se alteraron migraciones históricas: una actualización de una instalación previa requiere respaldo y ensayo de migración/conversión en copia.
3. **Estado «expediente completo»:** `ServicioColaboradores` todavía lo deduce de contar tres documentos. Eso no demuestra que estén los documentos exigidos ni que sean distintos o vigentes. Falta acordar y modelar requisitos por puesto/tipo de colaborador. No se inventó esa política.
4. **Fecha de salida e historia completa:** registrar un cambio de estado no establece una fecha efectiva de baja porque el formulario no la captura. Los movimientos nuevos empiezan desde esta versión; no se reconstruyen cambios anteriores que nunca se guardaron.
5. **Vacaciones y período de prueba:** se mantiene el modelo existente de días calendario y duración fija de prueba de 60 días. Son reglas de software heredadas, no una validación legal. RR. HH. debe definir calendario, derechos, excepciones y aprobación antes de automatizar saldos o decisiones.
6. **Auditoría parcial:** el historial de movimientos no sustituye una bitácora completa con valores anteriores/nuevos, ID de actor y trazabilidad de correcciones. Los archivos descargados quedan fuera de los permisos de la aplicación.
7. **Escala y concurrencia:** el directorio puede traer toda la tabla sin `Tope`; listas de documentos/avisos pueden crecer. No se realizaron pruebas de volumen ni estrés de múltiples procesos. SQLite local y sus archivos no equivalen a un servidor central seguro para varios equipos.
8. **Documentación desactualizada:** `CLAUDE.md` describe MySQL y restricciones de una demo; `SIGEM-estado-y-decisiones.md` contiene decisiones antiguas contradictorias. Faltan los tres documentos de especificación allí referenciados. Se conservó el stack real y no se inventaron entidades de módulos nuevos.
9. **Validación de respaldos de otra versión:** ahora se rechazan para evitar restaurar esquemas incompatibles. La conversión de un respaldo antiguo debe resolverse como tarea específica, preservando el original. No se ejecutó aquí una restauración real.
10. **Pruebas de ejecución pendientes:** especialmente traducción LINQ de los filtros compuestos, generadores de CommunityToolkit, XAML compilado y operaciones SQLite. La revisión estática no demuestra que estas rutas funcionen en Windows.

## Funciones recomendadas para el departamento de Recursos Humanos

Primero conviene cerrar los flujos que ya tienen entidades y pantallas parciales; después incorporar capacidades nuevas.

| Orden | Función | Utilidad para INVAYAL | Criterio de aceptación propuesto |
|---|---|---|---|
| 1 | Requisitos documentales por puesto y sucursal | Saber exactamente qué falta en cada expediente, sin confiar en un conteo arbitrario. | La tarjeta de pendientes abre personas y requisitos faltantes; un duplicado no completa otro requisito; un vencido se identifica aparte. |
| 2 | Contratos y contactos de emergencia editables | Alimentar desde el sistema los datos que la ficha y las alertas ya esperan. | Crear/renovar/cerrar contrato con historial; registrar contacto principal y consultarlo desde la ficha. |
| 3 | Vacaciones con aprobación y calendario | Evitar cruces y controlar cobertura de turnos/sucursales. | Solicitud, aprobación, cancelación y goce con autor/fecha; las canceladas restituyen el saldo según la política aprobada. |
| 4 | Centro de reportes de RR. HH. | Directorios, vencimientos, pendientes, ingresos/bajas, vacaciones e incidencias con filtros por período/sucursal. | Totales del reporte coinciden con su consulta; salarios ausentes para perfiles no autorizados. |
| 5 | Ingreso, traslado y salida con lista de tareas | Ordenar documentos, inducción, entrega y devolución de equipo y cierre de accesos. | Cada tarea tiene responsable, vencimiento y evidencia; el historial conserva el proceso. |
| 6 | Gestión de usuarios y bitácora de cambios | Separar captura, revisión y consulta y saber quién modificó datos. | Cambio inicial de contraseña obligatorio; permisos verificados en servicios; auditoría visible solo a autorizados. |
| 7 | Capacitación y evaluaciones | Seguir cursos, certificaciones y desempeño sin depender de hojas separadas. | Alertas de vencimiento de certificaciones, historial por persona y acceso restringido a evaluaciones. |
| 8 | Autoservicio del colaborador | Solicitar vacaciones, constancias y actualización de información. | Cada persona solo accede a su expediente; las modificaciones sensibles requieren aprobación. Requiere diseñar acceso central seguro. |

Las funciones 1, 3, 5, 7 y 8 necesitan acordar campos y reglas de negocio. No están implementadas ni se incluyeron maquetas que aparenten estar conectadas.

## Verificaciones y cómo probar

### Realizado en este entorno

- Comparación de los archivos con el ZIP recibido: 20 existentes modificados y 131 existentes conservados.
- Análisis XML de 25 archivos XAML/proyecto: sin errores de XML.
- Revisión léxica de delimitadores de 104 archivos C#: sin desbalances detectados. No es un compilador.
- Cotejo de campos privados en las partes de `VistaModeloPrincipal`: sin referencias a campos eliminados.
- Confirmación de que estilos, recursos y migraciones históricas no fueron alterados. Único cambio XAML: `ColaboradoresActivos` → `ColaboradoresRegistrados` en la tarjeta total.
- Revisión de dependencias: las nuevas dependencias de constructores (`SesionUsuario`) ya se registraban en `MauiProgram`; la fábrica de diseño fue adaptada.

### No realizado

Se intentó:

```text
dotnet build empleados/empleados.csproj
/bin/bash: dotnet: command not found
```

El comando terminó con código 127. No hay salida de compilación C#; no se han ejecutado los tests ni abierto la aplicación. No se declara «cero errores» ni «pruebas aprobadas».

### Pruebas de regresión preparadas

`pruebas/Sigem.Regresion` es una aplicación de consola .NET 10. Enlaza los servicios y entidades reales; usa SQLite temporal y las mismas versiones de paquetes existentes. No usa la base del usuario ni requiere MAUI para comprobar servicios.

Contiene 11 grupos: migraciones en base nueva; separación por empresa/sucursal y contenido; protección del DTO salarial; validaciones de empleados; historial y guardados sin cambios; vacaciones con extremos inclusivos; dashboard; idempotencia/renovación de alertas; fallo del segundo guardado documental; efemérides; permisos de respaldo global.

Desde la raíz del proyecto, en un equipo con .NET 10:

```powershell
dotnet run --project pruebas/Sigem.Regresion/Sigem.Regresion.csproj
```

El resultado debe ser 11/11 grupos correctos y código de salida 0. Un fallo es un bloqueo para usar esta entrega con datos reales. La prueba de migraciones usa una base nueva; no certifica conversión de bases antiguas. Las pruebas de respaldos solo comprueban rechazo por perfil, no restauración funcional.

### Compilación y recorrido en Windows

En Windows con .NET 10 y la carga de trabajo MAUI Windows:

```powershell
dotnet build empleados/empleados.csproj -c Debug
dotnet run --project empleados/empleados.csproj -c Debug
```

Usar una copia de la base, configurada en `appsettings.Local.json`, para el recorrido:

1. Abrir dos empresas, cambiar varias veces entre ellas y verificar que ficha, filtros, vista previa y tarjetas nunca conservan información de la anterior.
2. Probar SuperAdministrador, Administrador, SupervisorSucursal y Consulta; incluir supervisor sin sucursal. Intentar acceder a una ficha/documento ajenos por ID en las pruebas de servicios.
3. Crear empleado y cambiar salario, sucursal y estado. Comprobar los movimientos y que guardar nuevamente no crea otros.
4. Registrar vacaciones de 5 días; intentar repetir, contener, solapar y usar el día final del período. Un período adyacente debe aceptarse; uno cancelado no debe bloquear.
5. Adjuntar un documento, renovar su fecha y eliminarlo. Comprobar contenido, historial y desaparición de avisos obsoletos al abrir Alertas.
6. Comparar total/activos y documentos/contratos próximos y vencidos con la base de prueba. Incluir vencimiento al final del día 30, cumpleaños pasado y cambio de fecha del sistema de pruebas.
7. Generar directorio PDF/Excel, ficha y constancia con diferentes perfiles; comprobar importes, campos nulos, paginación y ausencia de salarios cuando corresponda.
8. Como SuperAdministrador, respaldar y restaurar la copia de trabajo. Verificar todos los documentos después del reinicio. Probar respaldo corrupto, de otra versión y conexión local personalizada; los rechazos deben conservar la copia previa.
9. Comprobar tamaño de textos del dashboard y que la apariencia se mantiene a distintas resoluciones. No se realizó inspección visual aquí.

## Incorporación al repositorio

El ZIP contiene el proyecto completo actualizado. Crear una rama de revisión desde la versión original, copiar los archivos, revisar diferencias y ejecutar las pruebas anteriores. No mezclar estos archivos a ciegas con una rama que haya recibido cambios posteriores al ZIP del 1 de septiembre.

```powershell
git switch -c revision/logica-rrhh
# Copiar los archivos de esta entrega y revisar el diff.
git diff --stat
git diff
```

No hay cambios publicados ni un pull request generado por esta sesión. Este informe no sustituye el resultado de las pruebas en el equipo de desarrollo.

## Referencias técnicas consultadas

- Microsoft Learn, [filtros globales de EF Core](https://learn.microsoft.com/en-us/ef/core/querying/filters): composición de filtros en EF anterior a 10, contexto de empresa y navegación entre entidades.
- Microsoft Learn, [copia en línea de Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup): API `SqliteConnection.BackupDatabase` y bloqueo de escrituras durante la copia.
