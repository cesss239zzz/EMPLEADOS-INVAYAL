# SIGEM — Estado del proyecto y decisiones tomadas

Este archivo es la memoria del proyecto. Se actualiza al cerrar cada etapa.
Lo que está acá no se vuelve a preguntar.

---

## Identidad

- **Nombre del producto:** SIGEM (antes SIGEM)
- **Cliente:** Inversiones Ayala Alvarenga S. de R.L. (Honduras)
- **Logotipo:** maletín, en `Resources/Images/logo.png` (512×512, PNG con transparencia)
- **`ApplicationId`:** `hn.invayal.rhmanager`
- **Ensamblado y espacio de nombres:** siguen siendo `empleados`. No se renombraron a propósito: es churn sin valor visible y con riesgo de romper el build.
- **Base de datos:** sigue llamándose `sigem`. Cambiarla es trivial hoy y caro después de la primera migración.

---

## Decisiones cerradas

| Tema | Decisión |
|---|---|
| Cultura | es-HN, fechas `dd/MM/yyyy`, moneda `L. #,##0.00`, UTC-6 sin horario de verano. Fijada en `MauiProgram.cs` |
| Perfiles | SuperAdministrador, Administrador, Supervisor de sucursal, Consulta. El salario solo lo ve Administrador o superior |
| Sucursal | Un colaborador pertenece a UNA sucursal |
| Usuario semilla | `cregalado` / SuperAdministrador, con acceso a todas las empresas. Contraseña con BCrypt factor 11 y cambio obligatorio al primer acceso |
| Configuración | `appsettings.json` junto al ejecutable. `appsettings.Local.json` lo sobreescribe y no se versiona |
| Empaquetado | Desempaquetado y autocontenido: `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `SelfContained=true` |
| Registros | `%LOCALAPPDATA%\SIGEM\registros`, archivo diario, 14 días de retención |
| **Motor de datos** | **SQLite** para la demostración: `sigem.db` en `%LOCALAPPDATA%\SIGEM`. MySQL queda para después de la presentación |
| **Datos de la demo** | Sembrados por migración: 2 empresas, 3 sucursales, catálogos y 12 colaboradores. Ver regla 14 de `CLAUDE.md` |
| **Precarga** | **Ninguna.** Cada pantalla carga lo suyo al abrirse. Ver regla 13 de `CLAUDE.md` |

### Restricción técnica que no es negociable

**El proyecto se queda en EF Core 9.0.19**, aunque SQLite sí tenga proveedor para EF Core 10. La razón es la vuelta a MySQL: `Pomelo.EntityFrameworkCore.MySql` 9.0.0 declara `Microsoft.EntityFrameworkCore.Relational [9.0.0, 9.0.999]`, un tope duro, y no existe Pomelo para EF Core 10. Manteniendo EF 9, volver a MySQL es cambiar un paquete; subiendo a EF 10 habría que bajar todo de nuevo.

`dotnet-ef` instalada es 10.0.9 y genera migraciones contra el runtime 9 sin problema. El comando lleva `--framework net10.0-windows10.0.19041.0`.

### Lo único que cambia entre SQLite y MySQL

Un solo método: `ContextoRhManager.AjustarTiposParaSqlite`. SQLite no tiene tipo decimal y EF lo guardaría como texto, con lo que ordenar o sumar importes daría resultados lexicográficos. Los importes se guardan como entero de centavos, exacto y ordenable; la propiedad en C# sigue siendo `decimal`, como exige la regla 10. Al volver a MySQL se borra ese método y se restauran `DECIMAL(12,2)` y `DATETIME(6)`.

---

## Plan de etapas

| Etapa | Contenido | Estado |
|---|---|---|
| **E1** | Proyecto, DI, Serilog a archivo, tres enganches globales de excepciones, pantalla de diagnóstico | ✅ **Terminada** |
| **E2** | `DbContext`, 14 entidades, filtro global, migración inicial y siembra completa | ✅ **Terminada** |
| **E3** | Acceso con BCrypt, selector de empresas, filtro global | ✅ **Terminada** |
| **E4** | Shell completo, sistema de diseño aplicado, tabla de colaboradores | ✅ **Terminada** |
| **E5** | Colaboradores: tabla con búsqueda y filtros | ✅ **Terminada** |
| **E6** | Ficha del colaborador, cuatro pestañas | ✅ **Terminada** |
| **E7** | Resumen con métricas reales, motor de alertas, panel de avisos | ✅ **Terminada** |
| **E8** | Notificación de Windows con `AppNotificationManager` | Pendiente |

### Rediseño de las vistas sobre las maquetas de Stitch

Las pantallas se rehicieron contra `stitch_sigem_digital_personnel_records`
(dashboard administrativo y ficha del colaborador) y su `DESIGN.md`.

| Tema | Decisión |
|---|---|
| Paleta | La del sistema "SIGEM Enterprise": estructura `#0A2540` sobre `#000F22`, acción `#0453CD`, lienzo `#F8F9FB`, tarjeta blanca con borde `#E1E4E8`. Las claves de `ColoresSigem.xaml` conservan su nombre; cambian los valores |
| Tipografía | El sistema pide **Inter** y **JetBrains Mono**. Ninguna viaja con Windows y empotrarlas no está autorizado: se usan **Segoe UI** y **Consolas**, que cumplen el mismo papel |
| Iconos | Dibujados con `Path` y datos de trazo en el propio XAML. No se usa fuente de iconos: una fuente que no resuelva deja cuadros vacíos en toda la interfaz |
| Estructura | `PaginaPrincipal` es solo el contenedor —menú lateral y barra superior—. Cada pantalla es un `ContentView` en `Vistas/Paneles/` que hereda el `BindingContext`. Antes las tres vivían en un archivo de 700 líneas |
| Pestañas de la ficha | La maqueta dibuja "Historial Salarial" y "Evaluaciones". No hay tabla para ninguna de las dos y **inventar campos está prohibido**: van Contratos y Contactos en su lugar. Los cambios de salario ya se ven dentro del historial laboral |
| Documentos | Salen de la tira de pestañas y pasan al panel "Expediente Digital" de la derecha, como en la maqueta, con semáforo verde/ámbar/rojo |
| Botones sin etapa | Exportar, Nuevo registro, Editar, Constancia, Incidencia y Vacaciones existen y responden: abren un aviso que nombra la etapa en que se habilitan. Ninguno queda mudo (regla 4) |
| Motor de alertas | Corre una vez por empresa y por sesión, al abrir Resumen o Alertas. Es idempotente, pero escribe: correrlo en cada aparición sería trabajo regalado |
| `StrokeShape` | Nunca en un `Setter` de estilo. Un `Setter` guarda una sola instancia de `RoundRectangle` y la repartiría entre todos los `Border`; cada uno declara la suya |

Servicios nuevos: `IServicioResumen` / `ServicioResumen`, que calcula las cuatro
métricas con `COUNT` en la base. `IServicioAlertas` **no estaba registrado en
`MauiProgram`**: resolver el ViewModel habría lanzado excepción al abrir la
pantalla principal. Ya está registrado, junto con el de resumen.

`FiltroColaboradores` acepta un `Tope` que viaja a SQL como `LIMIT`, para el
directorio rápido del resumen. Sin él habría que traer la tabla entera y quedarse
con las primeras filas, que es justo lo que prohíbe la regla 13.

### Sin compilar en este entorno

El rediseño se escribió en un contenedor Linux **sin .NET SDK**, y el proyecto
apunta a `net10.0-windows10.0.19041.0`: aquí no hay forma de compilarlo ni de
abrirlo. Se verificó a mano que todo XAML es XML válido, que cada
`StaticResource` resuelve contra los diccionarios, que cada `{Binding}` existe
en el ViewModel o en el tipo del `x:DataType`, y que ningún `Stroke` recibe un
`Color` donde se espera un `Brush`. **Falta correr `dotnet build` en Windows
antes de dar la etapa por cerrada.**

### Cambios al plan original

1. **Datos sembrados, con valores provisionales.** `SiembraDemostracion.cs` sí siembra 2 empresas, 3 sucursales, los catálogos y 12 colaboradores con historial, contratos y documentos. Los valores son inventados porque `SIGEM-prototipo.html` no estaba en el disco; el propio archivo lo advierte en su cabecera. Cuando aparezca el prototipo se reemplazan los arreglos y se regenera la migración.

2. **El motor de alertas no corre al abrir la app.** No puede: el filtro global exige empresa activa, así que lo más temprano posible es justo después de elegir empresa, en E3. Además, correrlo al arrancar contradice la regla 13.

3. **Resuelto: la demostración es de solo lectura sobre datos sembrados.** El bloqueo era que, con la app vacía y el alta fuera de alcance, toda pantalla quedaría en blanco. Lo resuelve la siembra por migración: hay 12 expedientes con historial, contratos y documentos que ver. El alta y la edición siguen fuera de alcance, y sus botones lo dicen en vez de fallar.

---

## E1 — lo que quedó construido

`Configuracion/` `OpcionesRhManager`, `RutasRhManager`
`Servicios/` `IServicioDiagnostico` + `ServicioDiagnostico`, `ResultadoDiagnostico`, `IServicioDialogo` + `ServicioDialogo`, `IServicioNavegacion` + `ServicioNavegacion` + `RutasNavegacion`, `ManejadorExcepcionesGlobales`, `RegistroEmergencia`, `EstadoAplicacion`
`VistaModelos/` `VistaModeloBase`, `VistaModeloArranque`, `VistaModeloDiagnostico`, `VistaModeloPendiente`
`Vistas/` `PaginaArranque`, `PaginaDiagnostico`, `PaginaPendiente`

### Tres bugs reales que E1 destapó

1. **La ventana nunca se creaba.** `OnAppearing` de la primera página llamaba a `GoToAsync` dentro de la cadena síncrona que monta la vista nativa del Shell; MAUI lanzaba `Pending Navigations still processing`. La app quedaba viva pero sin ventana. Corregido: la verificación se dispara desde `Loaded` con un tic de retraso, y `ServicioNavegacion` reintenta una vez si Shell está ocupado.

2. **El registro dejaba de escribirse después de la primera corrida.** `shared: true` en el sink de archivo de Serilog usa un mutex entre procesos; una vez creado desde otro contexto, las corridas siguientes no lo adquirían y **descartaban todo en silencio**. Corregido: sin `shared`, con `flushToDiskInterval` y con `Serilog.Debugging.SelfLog` volcando al registro de emergencia.

3. **`FontFamily="Consolas"`** hacía que MAUI buscara una fuente incrustada inexistente, con dos excepciones por arranque en el log.

---

## Bloqueos vigentes

### Faltan los documentos de especificación

Buscados en el repositorio, Descargas, Escritorio, Documentos y `C:\Proyectos`. No existen:

- `docs/SIGEM-modelo-datos.md` — **bloquea E2.** Sin él habría que inventar campos, que `CLAUDE.md` prohíbe
- `docs/SIGEM-adenda-01-multiempresa-mysql.md` — bloquea el diseño del aislamiento
- `SIGEM-prototipo.html` — ya no bloquea E2, porque no hay datos que sembrar, pero sí la paleta y el diseño de E4

### MySQL Server no está instalado

Solo está MySQL Workbench 8.0 CE. No hay servicio y el puerto 3306 está cerrado. E2 necesita el servidor corriendo y la base creada:

```sql
CREATE DATABASE sigem
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;
```

---

## Entorno verificado

| Requisito | Estado |
|---|---|
| .NET SDK | ✅ 10.0.301 |
| Workload `maui-windows` | ✅ 10.0.20 |
| `dotnet-ef` | ✅ 10.0.9 |
| MySQL Server 8 | ❌ no instalado |
| `dotnet build` | ✅ 0 errores, 0 advertencias |
