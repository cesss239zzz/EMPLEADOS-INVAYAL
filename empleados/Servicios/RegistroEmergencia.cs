using empleados.Configuracion;

namespace empleados.Servicios;

/// <summary>
/// Escritura directa a disco para fallos que ocurren antes de que exista el
/// contenedor de dependencias, o cuando el propio Serilog es lo que fallo.
/// No depende de nada. Nunca lanza: si no puede escribir, se calla.
/// </summary>
public static class RegistroEmergencia
{
    private static readonly object Candado = new();

    /// <summary>Anexa una linea al archivo de arranque critico.</summary>
    public static void Escribir(string origen, Exception? excepcion)
    {
        var texto = excepcion?.ToString() ?? "(sin detalle de excepción)";
        Escribir(origen + Environment.NewLine + texto);
    }

    /// <summary>Anexa un texto libre al archivo de arranque critico.</summary>
    public static void Escribir(string mensaje)
    {
        try
        {
            RutasRhManager.Asegurar();

            var linea = "[" + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz") + "] "
                + mensaje + Environment.NewLine
                + new string('-', 78) + Environment.NewLine;

            lock (Candado)
            {
                File.AppendAllText(RutasRhManager.ArchivoRegistroEmergencia, linea);
            }
        }
        catch
        {
            // Si ni siquiera se puede escribir el registro de emergencia no queda
            // nada por hacer. Propagar aqui cerraria la aplicacion en silencio,
            // que es exactamente lo que esta clase existe para evitar.
        }
    }
}
