namespace empleados.Servicios;

/// <summary>
/// Dialogos hacia el usuario. Existe para que los ViewModel no toquen la interfaz
/// directamente (CLAUDE.md, regla 11) y para poder sustituirlos en pruebas.
/// </summary>
public interface IServicioDialogo
{
    /// <summary>Muestra un aviso con un solo boton.</summary>
    Task AvisarAsync(string titulo, string mensaje, string boton = "Entendido");

    /// <summary>Muestra una pregunta de si o no. Devuelve verdadero si el usuario acepta.</summary>
    Task<bool> ConfirmarAsync(string titulo, string mensaje, string aceptar = "Si", string cancelar = "No");

    /// <summary>
    /// Pide al usuario que escriba un texto. Devuelve nulo si cancela.
    ///
    /// Se usa para la confirmacion escrita de las acciones que borran datos sin
    /// vuelta atras: pulsar "Aceptar" por inercia es facil, escribir el nombre
    /// exacto de lo que se va a destruir no lo es.
    /// </summary>
    Task<string?> PedirTextoAsync(
        string titulo,
        string mensaje,
        string marcador = "",
        string aceptar = "Aceptar",
        string cancelar = "Cancelar");
}
