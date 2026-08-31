using System.Globalization;
using System.Text;

namespace empleados.Datos;

/// <summary>
/// Normaliza texto para poder buscarlo sin que las tildes estorben
/// (solicitud de cambios, CR-08).
///
/// El problema que resuelve es concreto: en cuanto los expedientes se capturan
/// bien escritos —"José Núñez Peña"— el LIKE de SQLite deja de encontrarlos si
/// la secretaria escribe "jose nunez" en el buscador, porque para SQLite "u" y
/// "ú" son dos caracteres distintos. SQLite no trae cotejamiento que ignore
/// acentos, asi que se resuelve guardando aparte una copia del texto ya plegada:
/// sin tildes y en mayusculas.
///
/// Se pliega al guardar, no al buscar. Plegar en cada consulta obligaria a traer
/// la tabla entera a memoria para compararla, que es justo lo que prohibe la
/// regla 13 de CLAUDE.md. Con la copia guardada, la comparacion la sigue
/// haciendo la base.
/// </summary>
public static class TextoBusqueda
{
    /// <summary>
    /// Quita tildes y pasa a mayusculas. "José Núñez" queda "JOSE NUNEZ".
    ///
    /// La eñe se pliega a "n" a proposito: quien busca teclea "nunez" mucho mas
    /// a menudo de lo que teclea "núñez".
    /// </summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        // FormD separa cada letra de su tilde; luego se descartan las tildes,
        // que Unicode clasifica como marcas sin espaciado.
        var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                limpio.Append(caracter);
            }
        }

        return limpio.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    /// <summary>
    /// Arma el texto por el que se busca un colaborador: nombres, apellidos,
    /// codigo e identidad, todo plegado y en una sola cadena.
    /// </summary>
    public static string DeColaborador(
        string? primerNombre,
        string? segundoNombre,
        string? primerApellido,
        string? segundoApellido,
        string? codigo,
        string? identidad)
    {
        var partes = new[] { primerNombre, segundoNombre, primerApellido, segundoApellido, codigo, identidad }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        return Normalizar(string.Join(' ', partes));
    }
}
