using System.Globalization;
using empleados.Datos.Entidades;
using empleados.Servicios;

namespace empleados.Vistas;

/// <summary>Paleta compartida por los convertidores. Debe seguir a ColoresSigem.xaml.</summary>
internal static class Paleta
{
    public static readonly Color Tinta = Color.FromArgb("#0F3D6E");
    public static readonly Color Sello = Color.FromArgb("#B4232A");
    public static readonly Color Ambar = Color.FromArgb("#B7791F");
    public static readonly Color Verde = Color.FromArgb("#1E7A4B");
    public static readonly Color Acero = Color.FromArgb("#5A6673");
    public static readonly Color Linea = Color.FromArgb("#E2E7ED");
    public static readonly Color Papel = Color.FromArgb("#F4F6F9");
    public static readonly Color Superficie = Color.FromArgb("#FFFFFF");

    public static readonly Color SelloTenue = Color.FromArgb("#FCEDED");
    public static readonly Color AmbarTenue = Color.FromArgb("#FDF6E7");
    public static readonly Color VerdeTenue = Color.FromArgb("#E8F3ED");
    public static readonly Color AceroTenue = Color.FromArgb("#EDF1F5");
}

/// <summary>
/// Completitud del expediente al borde izquierdo de la fila:
/// verde completo, ambar parcial, sello incompleto.
/// </summary>
public sealed class ConvertidorColorExpediente : IValueConverter
{
    public object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is EstadoExpediente estado
            ? estado switch
            {
                EstadoExpediente.Completo => Paleta.Verde,
                EstadoExpediente.Parcial => Paleta.Ambar,
                _ => Paleta.Sello
            }
            : Paleta.Acero;

    public object ConvertBack(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => throw new NotSupportedException();
}

/// <summary>Color del texto del sello de situacion laboral.</summary>
public sealed class ConvertidorColorEstado : IValueConverter
{
    public object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is EstadoColaborador estado
            ? estado switch
            {
                EstadoColaborador.Activo => Paleta.Verde,
                EstadoColaborador.Suspendido => Paleta.Ambar,
                _ => Paleta.Sello
            }
            : Paleta.Acero;

    public object ConvertBack(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => throw new NotSupportedException();
}

/// <summary>Fondo tenue del sello de situacion laboral.</summary>
public sealed class ConvertidorFondoEstado : IValueConverter
{
    public object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is EstadoColaborador estado
            ? estado switch
            {
                EstadoColaborador.Activo => Paleta.VerdeTenue,
                EstadoColaborador.Suspendido => Paleta.AmbarTenue,
                _ => Paleta.SelloTenue
            }
            : Paleta.AceroTenue;

    public object ConvertBack(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => throw new NotSupportedException();
}

/// <summary>Fondo de la pestana: blanca la activa, gris la que descansa.</summary>
public sealed class ConvertidorFondoPestana : IValueConverter
{
    public object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is true ? Paleta.Superficie : Paleta.Papel;

    public object ConvertBack(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => throw new NotSupportedException();
}

/// <summary>Texto de la pestana: azul la activa, gris la que descansa.</summary>
public sealed class ConvertidorTextoPestana : IValueConverter
{
    public object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is true ? Paleta.Tinta : Paleta.Acero;

    public object ConvertBack(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => throw new NotSupportedException();
}

/// <summary>Punto de la linea de tiempo: azul el movimiento vigente, gris el resto.</summary>
public sealed class ConvertidorColorPunto : IValueConverter
{
    public object Convert(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => valor is true ? Paleta.Tinta : Paleta.Linea;

    public object ConvertBack(object? valor, Type tipo, object? parametro, CultureInfo cultura)
        => throw new NotSupportedException();
}
