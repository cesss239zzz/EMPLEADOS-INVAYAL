# SIGEM — Preparación del repositorio y prompt de arranque

---

## Paso 1: Armar el repositorio antes de escribir un solo prompt

```
SIGEM/
├── CLAUDE.md                    ← reglas invariantes
└── docs/
    ├── SIGEM-modelo-datos.md
    ├── SIGEM-adenda-01-multiempresa-mysql.md
    └── SIGEM-adenda-02-notificaciones.md
```

**Por qué esto importa más que el prompt:** Claude Code lee `CLAUDE.md` en cada turno de la conversación, automáticamente. Las reglas contra crashes no se olvidan a la fase 6, cuando el contexto ya está lleno. Un prompt largo, en cambio, se diluye.

Todo lo que sea invariante va en `CLAUDE.md`. El prompt queda corto.

---

## Paso 2: Antes de arrancar, verificar el entorno

Sin esto la Fase 1 falla y se pierde media hora entendiendo por qué:

```bash
dotnet --version                    # .NET 9 o superior
dotnet workload list                # debe aparecer "maui"
mysql --version                     # MySQL 8.0+
dotnet tool install --global dotnet-ef
```

MySQL corriendo, y una base vacía creada a mano:

```sql
CREATE DATABASE sigem
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;
```

---

## Paso 3: El prompt de arranque

Este es el único prompt largo. Los siguientes son de una línea.

---

```
Vas a construir SIGEM, un sistema de expediente digital del personal
en .NET MAUI para Windows con MySQL local.

ANTES DE ESCRIBIR CÓDIGO:

1. Lee CLAUDE.md completo. Sus reglas son obligatorias y se aplican a
   todo lo que generes, sin que yo tenga que repetirlas.
2. Lee los tres documentos de /docs/. Ahí está la especificación
   funcional y el modelo de datos completo.
3. Dime en no más de 15 líneas qué entendiste del alcance y cuáles son
   las tres decisiones técnicas más riesgosas que ves.

DESPUÉS DE QUE YO CONFIRME:

Ejecuta ÚNICAMENTE la Fase 0 del plan de fases de CLAUDE.md.

La Fase 0 termina cuando:
- La solución compila con 0 errores
- La aplicación abre y muestra una pantalla vacía sin cerrarse
- Serilog escribe a archivo
- Los tres enganches de excepciones globales están activos
- Provoco un error a propósito, queda en el registro y la app NO se cierra

No implementes entidades, ni acceso a datos, ni pantallas de negocio.
Solo el esqueleto que sostiene todo lo demás.

Al terminar, repórtame:
- Archivos creados
- Salida literal de dotnet build
- Cómo pruebo la Fase 0 paso a paso
- Qué supusiste

Luego DETENTE y espera mi confirmación. No avances a la Fase 1.
```

---

## Paso 4: Los prompts siguientes

Ya no hace falta explicar nada. La spec está en disco:

```
Fase 1. Mismas reglas de CLAUDE.md. Detente al terminar.
```

Y así hasta la 10.

---

## Cuando algo falle

**Prompt de corrección:**

```
Error al ejecutar la Fase N:

[pegar el error o la traza completa]

Diagnostica la causa raíz antes de tocar código. Dime qué la provocó
y qué regla de CLAUDE.md la habría evitado.
Corrige solo eso. No refactorices nada más.
```

Esa última línea importa: sin ella, el agente aprovecha para reorganizar
código que ya funcionaba y aparecen errores nuevos.

**Prompt de auditoría** — vale la pena correrlo cada tres fases:

```
No generes código. Audita todo lo construido hasta ahora contra la
lista de verificación de CLAUDE.md. Reporta cada incumplimiento con
archivo y línea. Si no hay ninguno, dilo.
```

---

## Por qué este método y no un solo prompt

| Un solo prompt | Por fases con verificación |
|---|---|
| Miles de líneas que nunca compilaron | Cada fase compila antes de la siguiente |
| El primer build tira decenas de errores encadenados | Los errores aparecen de a uno y en contexto |
| Imposible saber qué error causó cuál | La causa está en lo último que se tocó |
| El contexto se llena y las reglas se olvidan | `CLAUDE.md` se relee en cada turno |
| Depurar código ajeno que nunca corrió | Probar algo que acabás de ver funcionar |

**No es más lento. Es más rápido**, porque el tiempo de un proyecto así
no se va escribiendo código: se va depurando código que nunca se probó.

---

## Tres cosas que hay que decidir antes de la Fase 1

Están pendientes de los documentos anteriores y bloquean el modelo de datos:

1. **Multiempresa: ¿cuántas empresas realmente?** Dos o tres del grupo, o
   la idea es vender SIGEM a terceros. Cambia el aislamiento de archivos.
2. **¿Los dos perfiles adicionales son Supervisor de sucursal y Consulta?**
   Define todo el control de acceso desde la Fase 3.
3. **¿MSIX o ejecutable suelto?** Define si en la Fase 8 hay que registrar
   `AppUserModelID` para que Windows acepte las notificaciones.

Sin la primera y la segunda, la Fase 1 genera entidades que después hay
que migrar. Con migraciones ya aplicadas, eso duele.
