using System.Globalization;
using empleados.Datos.Entidades;
using empleados.Servicios;

namespace empleados.Vistas;

/// <summary>
/// Paleta compartida por los convertidores. Debe seguir a ColoresSigem.xaml:
/// son los mismos valores, aca en codigo porque un convertidor no puede leer
/// del diccionario de recursos.
/// </summary>
internal static class Paleta
{
    public static readonly Color Tinta = Color.FromArgb("#0A2540");
    public static readonly Color Texto = Color.FromArgb("#191C1E");
    public static readonly Color Accion = Color.FromArgb("#0453CD");
    public static readonly Color Sello = Color.FromArgb("#BA1A1A");
    public static readonly Color Ambar = Color.FromArgb("#9A5B00");
    public static readonly Color Verde = Color.FromArgb("#17603A");
    public static readonly Color Acero = Color.FromArgb("#43474D");
    public static readonly Color AceroSuave = Color.FromArgb("#74777E");
    public static readonly Color Linea = Color.FromArgb("#E1E4E8");
    public static readonly Color Papel = Color.FromArgb("#F8F9FB");
    public static readonly Color Superficie = Color.FromArgb("#FFFFFF");

    public static readonly Color SelloTenue = Color.FromArgb("#FFEDEA");
    public static readonly Color AmbarTenue = Color.FromArgb("#FEF4E4");
    public static readonly Color VerdeTenue = Color.FromArgb("#E6F5EC");
    public static readonly Color AceroTenue = Color.FromArgb("#EDEEF0");
    public static readonly Color AzulTenue = Color.FromArgb("#E8F0FE");
}

/// <summary>
/// Base de los convertidores de un solo sentido. La vista nunca escribe de
/// vuelta un color, asi que ConvertBack no tiene caso: se declara una vez aca
/// en lugar de repetirlo en cada convertidor.
/// </summary>
public abstract class ConvertidorDeIda : IValueConverter
{
    public abstract object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura);

    public object ConvertBack(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => throw new NotSupportedException(
            "Los convertidores de presentacion de SIGEM son de un solo sentido.");
}

/// <summary>
/// Completitud del expediente al borde izquierdo de la fila:
/// verde completo, ambar parcial, sello incompleto.
/// </summary>
public sealed class ConvertidorColorExpediente : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is EstadoExpediente estado
            ? estado switch
            {
                EstadoExpediente.Completo => Paleta.Verde,
                EstadoExpediente.Parcial => Paleta.Ambar,
                _ => Paleta.Sello
            }
            : Paleta.AceroSuave;
}

/// <summary>Color del texto del sello de situacion laboral.</summary>
public sealed class ConvertidorColorEstado : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is EstadoColaborador estado
            ? estado switch
            {
                EstadoColaborador.Activo => Paleta.Verde,
                EstadoColaborador.Suspendido => Paleta.Ambar,
                _ => Paleta.Acero
            }
            : Paleta.Acero;
}

/// <summary>Fondo tenue del sello de situacion laboral.</summary>
public sealed class ConvertidorFondoEstado : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is EstadoColaborador estado
            ? estado switch
            {
                EstadoColaborador.Activo => Paleta.VerdeTenue,
                EstadoColaborador.Suspendido => Paleta.AmbarTenue,
                _ => Paleta.AceroTenue
            }
            : Paleta.AceroTenue;
}

/// <summary>Texto de la pestana: azul de accion la activa, gris la que descansa.</summary>
public sealed class ConvertidorTextoPestana : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is true ? Paleta.Accion : Paleta.Acero;
}

/// <summary>Punto de la linea de tiempo: azul el movimiento vigente, gris el resto.</summary>
public sealed class ConvertidorColorPunto : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is true ? new SolidColorBrush(Paleta.Accion) : new SolidColorBrush(Paleta.Linea);
}

// ─── Semaforo documental ────────────────────────────────────────────────────

/// <summary>Trazo del icono de un documento del expediente.</summary>
public sealed class ConvertidorPincelDocumento : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => new SolidColorBrush(ColorDe(valor));

    internal static Color ColorDe(object? valor) => valor is SituacionDocumento situacion
        ? situacion switch
        {
            SituacionDocumento.Vencido => Paleta.Sello,
            SituacionDocumento.PorVencer => Paleta.Ambar,
            _ => Paleta.Verde
        }
        : Paleta.Verde;
}

/// <summary>Color del texto de un documento segun su situacion.</summary>
public sealed class ConvertidorColorDocumento : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is SituacionDocumento.Vigente
            ? Paleta.Texto
            : ConvertidorPincelDocumento.ColorDe(valor);
}

/// <summary>Fondo del renglon de un documento: blanco al dia, tenue si algo pasa.</summary>
public sealed class ConvertidorFondoDocumento : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is SituacionDocumento situacion
            ? situacion switch
            {
                SituacionDocumento.Vencido => Paleta.SelloTenue,
                SituacionDocumento.PorVencer => Paleta.AmbarTenue,
                _ => Colors.Transparent
            }
            : Colors.Transparent;
}

// ─── Gravedad de los avisos ─────────────────────────────────────────────────

/// <summary>Filo izquierdo de una tarjeta de aviso.</summary>
public sealed class ConvertidorColorAviso : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => ColorDe(valor);

    internal static Color ColorDe(object? valor) => valor is NivelAviso nivel
        ? nivel switch
        {
            NivelAviso.Critico => Paleta.Sello,
            NivelAviso.Advertencia => Paleta.Ambar,
            _ => Paleta.Accion
        }
        : Paleta.Accion;
}

/// <summary>Trazo del icono de una tarjeta de aviso.</summary>
public sealed class ConvertidorPincelAviso : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => new SolidColorBrush(ConvertidorColorAviso.ColorDe(valor));
}

/// <summary>Fondo tenue de una tarjeta de aviso.</summary>
public sealed class ConvertidorFondoAviso : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is NivelAviso nivel
            ? nivel switch
            {
                NivelAviso.Critico => Paleta.SelloTenue,
                NivelAviso.Advertencia => Paleta.AmbarTenue,
                _ => Paleta.AzulTenue
            }
            : Paleta.AzulTenue;
}

// ─── Menu lateral ───────────────────────────────────────────────────────────

/// <summary>Texto del menu lateral: blanco el activo, azul apagado el resto.</summary>
public sealed class ConvertidorTextoMenu : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is true ? Colors.White : Color.FromArgb("#9FB3C8");
}

/// <summary>Trazo del icono del menu lateral, con el mismo criterio que el texto.</summary>
public sealed class ConvertidorPincelMenu : ConvertidorDeIda
{
    public override object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is true ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb("#9FB3C8"));
}
