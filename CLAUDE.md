# CLAUDE.md — SIGEM

Reglas obligatorias de este repositorio. Se aplican a **todo** código generado, sin excepción y sin necesidad de que se repitan en cada instrucción.

---

## Contexto del proyecto

**SIGEM** — Sistema de Expediente Digital del Personal. Multiempresa.
Cliente: Inversiones Ayala Alvarenga S. de R.L. (Honduras).

**Stack fijo. No sustituir ningún componente sin autorización explícita:**

| Componente | Elección | Nota |
|---|---|---|
| UI | .NET MAUI (Windows primero) | Android queda para fase posterior |
| Patrón | MVVM con CommunityToolkit.Mvvm | Estricto |
| Datos | EF Core + Pomelo.EntityFrameworkCore.MySql | MySQL 8 local |
| Contraseñas | BCrypt.Net-Next | |
| PDF | QuestPDF | Licencia Community |
| Excel | ClosedXML | **Nunca EPPlus** (licencia comercial) |
| Notificaciones | Microsoft.Windows.AppNotifications | |
| Registro | Microsoft.Extensions.Logging + Serilog a archivo | |

**Idioma:** código, clases y propiedades en español (`Colaborador`, `FechaIngreso`). Comentarios en español. Nombres de tabla y columna en `snake_case` español.

**Especificación funcional completa** — leerla antes de implementar cualquier módulo:
- `/docs/SIGEM-modelo-datos.md`
- `/docs/SIGEM-adenda-01-multiempresa-mysql.md`
- `/docs/SIGEM-adenda-02-notificaciones.md`

Si algo de la especificación contradice estas reglas, **preguntar antes de decidir**. No inventar la resolución.

---

## Reglas que previenen los crashes reales

Estas no son preferencias de estilo. Cada una corresponde a una forma concreta en que una aplicación MAUI se cierra sin mensaje.

### 1. Inyección de dependencias completa
Toda página, ViewModel y servicio **debe** estar registrado en `MauiProgram.cs`. Un `Page` que se resuelve sin registrar cierra la aplicación al navegar, sin excepción visible.

- Servicios de datos: `AddTransient`
- ViewModels: `AddTransient`
- Páginas: `AddTransient`
- Estado de sesión y empresa activa: `AddSingleton`

**Al crear una página o ViewModel nuevo, registrarlo en el mismo cambio.** Nunca dejarlo para después.

### 2. `DbContext` por fábrica, jamás compartido
MAUI no tiene ámbito por petición como ASP.NET. Un `DbContext` inyectado como singleton o scoped se comparte entre operaciones concurrentes y **`DbContext` no es seguro entre hilos** — produce `InvalidOperationException` intermitente, el peor tipo de error para depurar.

```
Usar:  AddDbContextFactory<SigemDbContext>(...)
```

En cada operación: crear el contexto, usarlo, liberarlo con `using`. Nunca guardar un `DbContext` como campo de un ViewModel.

### 3. Nada de `async void`
Únicamente permitido en manejadores de eventos de la plataforma. En todo lo demás: `async Task`.
Una excepción dentro de un `async void` no puede capturarse y termina el proceso.

Prohibido igualmente: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`. Producen bloqueo mutuo en el hilo de interfaz.

### 4. Comandos con protección
Todo comando expuesto a un botón:

- Es `[RelayCommand]` de CommunityToolkit.Mvvm
- Es asíncrono si toca base de datos o archivos
- Tiene `try / catch / finally` completo
- Establece `IsBusy = true` al entrar y `false` en el `finally`
- Registra la excepción con `ILogger` y muestra al usuario un mensaje comprensible en español, **nunca** el `ex.Message` crudo
- Usa `[RelayCommand(CanExecute = ...)]` o revisa `IsBusy` para impedir doble pulsación

**Ningún botón queda sin comando enlazado.** Si un botón aún no tiene función, no se dibuja.

### 5. Actualizaciones de interfaz desde el hilo correcto
Toda modificación de una `ObservableCollection` o propiedad enlazada que ocurra dentro de una tarea asíncrona pasa por:

```
Dispatcher.Dispatch(() => { ... });
```

Modificar una colección enlazada a `CollectionView` desde un hilo secundario cierra la aplicación de inmediato en Windows.

### 6. Rutas de navegación registradas
Toda ruta usada en `Shell.Current.GoToAsync()` debe estar registrada con `Routing.RegisterRoute()`. Registrarla en el mismo cambio en que se crea la página.

### 7. Manejo global de excepciones
Desde la Fase 0 el proyecto tiene enganchados:

- `AppDomain.CurrentDomain.UnhandledException`
- `TaskScheduler.UnobservedTaskException`
- En Windows: `Application.Current.UnhandledException`

Los tres escriben al archivo de registro con traza completa y muestran una ventana de error controlada. **La aplicación nunca se cierra en silencio.**

### 8. Fallos de infraestructura no son crashes
Si MySQL no responde, si falta la cadena de conexión o si la migración no está aplicada, la aplicación muestra una **pantalla de diagnóstico** con el problema y qué hacer. No lanza excepción sin manejar.

Se verifica la conexión al arrancar, antes de mostrar la pantalla de acceso.

### 9. Multiempresa: filtro global obligatorio
El aislamiento entre empresas se implementa con **filtro de consulta global** en `OnModelCreating`:

```
HasQueryFilter(e => e.EmpresaId == _contexto.EmpresaActivaId)
```

**Prohibido** escribir `Where(x => x.EmpresaId == ...)` manualmente en repositorios. Se olvida una vez y una empresa ve datos de otra. Es un fallo de seguridad, no un error menor.

Toda consulta se ejecuta con empresa activa establecida. Sin ella, el servicio lanza una excepción controlada.

### 10. Dinero y fechas
- Todo importe: `decimal`. **Nunca `float` ni `double`.** Columna `DECIMAL(12,2)`
- Toda marca de tiempo: `DATETIME(6)`. **Nunca `TIMESTAMP`** en MySQL
- Cotejamiento de la base: `utf8mb4_0900_ai_ci`
- Enumeraciones persistidas como `TINYINT`, nunca como `ENUM` de MySQL

### 11. Sin lógica en el código subyacente
`.xaml.cs` contiene únicamente el constructor y la asignación del `BindingContext`. Toda lógica vive en el ViewModel. Todo acceso a datos pasa por una interfaz de servicio inyectada.

### 12. Contraseñas
Siempre BCrypt con factor de trabajo 11. Nunca se guarda, registra ni muestra una contraseña en texto plano. Nunca se escribe una contraseña en el archivo de registro.

### 13. Carga bajo demanda: la aplicación no precarga datos
Al arrancar, la aplicación **no** trae información a memoria. Cada pantalla consulta lo suyo cuando se abre, y nada más que lo suyo.

- **Ningún constructor consulta la base de datos.** Ni de ViewModel, ni de servicio, ni de página. El constructor asigna dependencias y valores fijos, nada más.
- Toda lectura vive en `CargarDatosAsync()`, la sobreescritura de `VistaModeloBase`. La página la dispara desde `OnAppearing` con `AparecerCommand`, nunca desde el constructor.
- Se recarga en cada aparición de la pantalla, para que al volver de otra vista no queden datos viejos. La reentrada la corta `EstaOcupado`.
- Las páginas se resuelven con fábrica perezosa en `AppShell`: una pantalla que nadie abre nunca se construye.
- Las listas se traen paginadas o filtradas. **Nunca `ToListAsync()` sobre una tabla completa** para después filtrar en memoria.
- Lo único que corre al arrancar es la verificación de infraestructura de la regla 8, que no lee datos de negocio: abre la conexión, comprueba y la cierra.

Una excepción a esta regla necesita autorización explícita y va comentada en el código con el motivo.

### 14. La demostración corre sobre datos sembrados por migración
La versión demostrable **no** se entrega vacía. La siembra es parte de la migración inicial y entra por `HasData`, no por pantallas de alta. Por eso el alta y la edición quedan fuera de alcance sin dejar la aplicación inservible: la demostración es de solo lectura sobre datos que ya están.

Contenido sembrado: 2 empresas, 3 sucursales, los catálogos completos y 12 colaboradores con su historial laboral, contratos y documentos.

- La siembra vive en **un solo archivo**, `Datos/SiembraDemostracion.cs`, para poder reemplazarla de una vez cuando cambien los datos de referencia.
- Los valores salen de `SIGEM-prototipo.html`. **Nada de inventar** nombres, identidades, puestos ni fechas cuando ese archivo esté disponible: se copian exactos.
- Las contraseñas sembradas van con BCrypt factor 11, nunca en texto plano, ni siquiera en la migración.

---

## Protocolo de trabajo

### Regla de oro
**Después de cada fase, la solución debe compilar y ejecutarse.** Sin excepción.

No se avanza a la fase siguiente hasta que:

1. `dotnet build` termina con **0 errores y 0 advertencias nuevas**
2. La aplicación abre sin cerrarse
3. Lo construido en la fase se puede probar manualmente

Si el compilado falla, **corregir antes de continuar**. Nunca acumular errores entre fases.

### Al terminar cada fase, reportar
1. Archivos creados o modificados
2. Resultado literal de `dotnet build`
3. Cómo probar lo hecho, paso a paso
4. Qué quedó pendiente o supuesto
5. **Detenerse y esperar confirmación**

### Prohibiciones de proceso
- No generar más de una fase por turno
- No dejar `throw new NotImplementedException()` en código que se enlaza a un botón
- No inventar campos que no estén en la especificación — preguntar
- No instalar paquetes fuera de los listados sin autorización
- No modificar migraciones ya aplicadas; crear una nueva

---

## Plan de fases

Cada fase parte de una aplicación que compila y termina en una aplicación que compila.

| Fase | Contenido | Prueba de aceptación |
|---|---|---|
| **0** | Solución, proyectos, DI, Serilog, manejo global de excepciones, pantalla de diagnóstico | Abre y muestra pantalla vacía. Se provoca un error a propósito y se registra sin cerrar la app |
| **1** | Cadena de conexión, `SigemDbContext`, entidades, migración inicial, siembra del SuperAdministrador | `dotnet ef database update` crea la base. Se ve el usuario semilla en MySQL |
| **2** | Acceso con usuario y contraseña, BCrypt, bloqueo por intentos, sesión | Se entra con el usuario semilla. Contraseña mala muestra mensaje, no cierra la app |
| **3** | Selector de empresas, contexto de empresa, filtro global, alta de empresa | Se crea una empresa, se selecciona, aparece en la barra superior |
| **4** | Catálogos: sucursal, departamento, puesto, tipo de contrato, tipo de documento | ABM completo de cada catálogo, aislado por empresa |
| **5** | Ficha del colaborador, contactos de emergencia, movimientos laborales | Se registra un colaborador, se le cambia el puesto, se ve el historial |
| **6** | Contratos, documentos digitalizados, tabla de archivos | Se adjunta un PDF, se guarda en carpeta, se abre desde la ficha |
| **7** | Motor de alertas, tabla de avisos, panel de pendientes, idempotencia | Se corre el motor dos veces; no se duplican avisos |
| **8** | Notificaciones de Windows, permisos, preferencias, agrupación | Aparece una notificación real de Windows. Al pulsarla abre la ficha correcta |
| **9** | Reportes PDF y Excel, constancias con QR | Se generan los 7 reportes. El QR verifica |
| **10** | Auditoría, importación inicial desde Excel, ajustes | Toda modificación queda registrada con usuario y hora |

---

## Verificación antes de dar por terminada una fase

Antes de reportar, revisar:

- [ ] `dotnet build` sin errores
- [ ] Cada página nueva registrada en DI **y** en `Routing`
- [ ] Cada botón visible tiene comando enlazado y funcional
- [ ] Ningún `async void` fuera de manejadores de evento
- [ ] Ningún `.Result` ni `.Wait()`
- [ ] Ningún `DbContext` guardado como campo
- [ ] Toda operación con base de datos dentro de `try/catch` con registro
- [ ] Toda entidad de empresa lleva `EmpresaId` y filtro global
- [ ] Ningún importe en `double` o `float`
- [ ] Ningún constructor consulta la base; toda lectura está en `CargarDatosAsync()`
- [ ] Ninguna consulta trae una tabla completa para filtrar en memoria
- [ ] La siembra está en `SiembraDemostracion.cs` y ninguna contraseña va en texto plano
- [ ] La aplicación abre y la fase se puede probar a mano
