using System.Globalization;
using empleados.Vistas.Efectos;
using Microsoft.Maui.Controls.Shapes;

namespace empleados.Vistas.Controles;

/// <summary>
/// Campo de fecha con captura por teclado y calendario opcional
/// (solicitud de cambios, CR-03).
///
/// El <c>DatePicker</c> de MAUI no sirve para lo que pide el cliente: obliga a
/// elegir del calendario y ese calendario solo avanza mes a mes, asi que capturar
/// una fecha de nacimiento de cuarenta años atras son cuarenta y ocho clics.
/// Este control resuelve las dos mitades del problema:
///
///   • Se escribe con el teclado. Solo se teclean numeros y las barras se ponen
///     solas: 15031985 queda como 15/03/1985.
///   • El calendario se abre UNICAMENTE al pulsar el icono, nunca al enfocar el
///     campo, y trae desplegables de mes y de año para saltar de golpe.
///
/// Si lo escrito no es una fecha valida se marca el campo y se explica el error,
/// pero NO se borra lo que el usuario escribio: perder lo tecleado por un digito
/// de mas es la forma mas rapida de que alguien deje de usar el sistema.
///
/// ─────────────────────────────────────────────────────────────────────────
/// Sobre la regla 11 de CLAUDE.md ("sin logica en el codigo subyacente"): esa
/// regla existe para que la logica de negocio no se esconda en las vistas, y se
/// respeta. Esto no es una pantalla sino un CONTROL reutilizable, y lo que lleva
/// dentro es exclusivamente comportamiento de presentacion: enmascarar el texto,
/// dibujar una rejilla de dias y validar el formato. No consulta la base, no
/// conoce ninguna entidad y no decide nada del negocio. La alternativa —repetir
/// el enmascarado en cada ViewModel— seria mucho peor.
/// ─────────────────────────────────────────────────────────────────────────
/// </summary>
public sealed class CampoFecha : ContentView
{
    private const string Formato = "dd/MM/yyyy";

    // ─── Propiedades enlazables ─────────────────────────────────────────────

    /// <summary>Fecha capturada. Nula cuando el campo esta vacio y se permite.</summary>
    public static readonly BindableProperty FechaProperty = BindableProperty.Create(
        nameof(Fecha), typeof(DateTime?), typeof(CampoFecha), null,
        BindingMode.TwoWay, propertyChanged: AlCambiarFechaDesdeAfuera);

    /// <summary>Primera fecha aceptada. Por omision, 1900.</summary>
    public static readonly BindableProperty FechaMinimaProperty = BindableProperty.Create(
        nameof(FechaMinima), typeof(DateTime), typeof(CampoFecha), new DateTime(1900, 1, 1),
        propertyChanged: AlCambiarLimites);

    /// <summary>Ultima fecha aceptada. Por omision, treinta años por delante.</summary>
    public static readonly BindableProperty FechaMaximaProperty = BindableProperty.Create(
        nameof(FechaMaxima), typeof(DateTime), typeof(CampoFecha), new DateTime(2100, 12, 31),
        propertyChanged: AlCambiarLimites);

    /// <summary>
    /// Si el campo puede quedar vacio. Cuando es falso, el control nunca escribe
    /// nulo hacia el enlace: conserva el ultimo valor bueno y marca el error.
    /// </summary>
    public static readonly BindableProperty PermiteVacioProperty = BindableProperty.Create(
        nameof(PermiteVacio), typeof(bool), typeof(CampoFecha), true);

    /// <summary>Texto guia dentro del campo.</summary>
    public static readonly BindableProperty MarcadorProperty = BindableProperty.Create(
        nameof(Marcador), typeof(string), typeof(CampoFecha), "dd/mm/aaaa");

    /// <summary>Deshabilita la captura sin ocultar el campo.</summary>
    public static readonly BindableProperty HabilitadoProperty = BindableProperty.Create(
        nameof(Habilitado), typeof(bool), typeof(CampoFecha), true,
        propertyChanged: AlCambiarHabilitado);

    public DateTime? Fecha
    {
        get => (DateTime?)GetValue(FechaProperty);
        set => SetValue(FechaProperty, value);
    }

    public DateTime FechaMinima
    {
        get => (DateTime)GetValue(FechaMinimaProperty);
        set => SetValue(FechaMinimaProperty, value);
    }

    public DateTime FechaMaxima
    {
        get => (DateTime)GetValue(FechaMaximaProperty);
        set => SetValue(FechaMaximaProperty, value);
    }

    public bool PermiteVacio
    {
        get => (bool)GetValue(PermiteVacioProperty);
        set => SetValue(PermiteVacioProperty, value);
    }

    public string Marcador
    {
        get => (string)GetValue(MarcadorProperty);
        set => SetValue(MarcadorProperty, value);
    }

    public bool Habilitado
    {
        get => (bool)GetValue(HabilitadoProperty);
        set => SetValue(HabilitadoProperty, value);
    }

    // ─── Partes del control ─────────────────────────────────────────────────

    private readonly Entry _entrada;
    private readonly Border _marcoEntrada;
    private readonly Button _botonCalendario;
    private readonly Label _etiquetaError;
    private readonly Border _marcoCalendario;
    private readonly Picker _selectorMes;
    private readonly Picker _selectorAno;
    private readonly Grid _rejillaDias;

    /// <summary>Impide que escribir el texto enmascarado vuelva a disparar el evento.</summary>
    private bool _reescribiendoTexto;

    /// <summary>Impide que rellenar los desplegables dispare el redibujado.</summary>
    private bool _rellenandoSelectores;

    /// <summary>Mes que se esta mostrando en el calendario, siempre dia 1.</summary>
    private DateTime _mesMostrado = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public CampoFecha()
    {
        _entrada = new Entry
        {
            Keyboard = Keyboard.Numeric,
            FontSize = 14,
            TextColor = Paleta.Texto,
            BackgroundColor = Colors.Transparent,
            HeightRequest = 40,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalOptions = LayoutOptions.Center
        };
        _entrada.TextChanged += AlEscribir;
        _entrada.Unfocused += AlSalirDelCampo;
        _entrada.Completed += AlPulsarEnter;

        _botonCalendario = new Button
        {
            Text = "📅",
            FontSize = 15,
            BackgroundColor = Colors.Transparent,
            TextColor = Paleta.Acero,
            WidthRequest = 40,
            HeightRequest = 38,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };

        // El calendario se abre SOLO desde aca. Enfocar el campo no lo despliega:
        // es exactamente lo que pide CR-03.
        _botonCalendario.Clicked += AlPulsarIcono;

        ConRealce(_botonCalendario, Paleta.AzulTenue);

        var filaEntrada = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        filaEntrada.Add(_entrada, 0);
        filaEntrada.Add(_botonCalendario, 1);

        _marcoEntrada = new Border
        {
            Stroke = new SolidColorBrush(Paleta.LineaFuerte),
            StrokeThickness = 1,
            BackgroundColor = Paleta.Superficie,
            Padding = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4) },
            Content = filaEntrada
        };

        _etiquetaError = new Label
        {
            FontSize = 11,
            TextColor = Paleta.Sello,
            IsVisible = false,
            Margin = new Thickness(2, 4, 0, 0)
        };

        _selectorMes = new Picker
        {
            FontSize = 13,
            TextColor = Paleta.Texto,
            BackgroundColor = Colors.Transparent,
            HeightRequest = 36
        };
        _selectorMes.SelectedIndexChanged += AlCambiarMesOAno;

        _selectorAno = new Picker
        {
            FontSize = 13,
            TextColor = Paleta.Texto,
            BackgroundColor = Colors.Transparent,
            HeightRequest = 36,
            WidthRequest = 92
        };
        _selectorAno.SelectedIndexChanged += AlCambiarMesOAno;

        var anterior = BotonNavegacion("‹");
        anterior.Clicked += (_, _) => MoverMes(-1);

        var siguiente = BotonNavegacion("›");
        siguiente.Clicked += (_, _) => MoverMes(1);

        var cabecera = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 6
        };
        cabecera.Add(anterior, 0);
        cabecera.Add(_selectorMes, 1);
        cabecera.Add(_selectorAno, 2);
        cabecera.Add(siguiente, 3);

        _rejillaDias = new Grid { RowSpacing = 2, ColumnSpacing = 2 };
        for (var columna = 0; columna < 7; columna++)
        {
            _rejillaDias.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(34)));
        }

        // Una cabecera de dias y hasta seis semanas.
        for (var fila = 0; fila < 7; fila++)
        {
            _rejillaDias.RowDefinitions.Add(new RowDefinition(new GridLength(30)));
        }

        var botonHoy = BotonPie("Hoy");
        botonHoy.Clicked += (_, _) => ElegirDia(DateTime.Today);

        var botonLimpiar = BotonPie("Limpiar");
        botonLimpiar.Clicked += (_, _) => Limpiar();
        botonLimpiar.SetBinding(IsVisibleProperty, new Binding(nameof(PermiteVacio), source: this));

        var pie = new HorizontalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { botonHoy, botonLimpiar }
        };

        _marcoCalendario = new Border
        {
            Stroke = new SolidColorBrush(Paleta.LineaFuerte),
            StrokeThickness = 1,
            BackgroundColor = Paleta.Superficie,
            Padding = new Thickness(10),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalOptions = LayoutOptions.Start,
            IsVisible = false,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(6) },
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children = { cabecera, _rejillaDias, pie }
            }
        };

        Content = new VerticalStackLayout
        {
            Children = { _marcoEntrada, _etiquetaError, _marcoCalendario }
        };

        _entrada.SetBinding(Entry.PlaceholderProperty, new Binding(nameof(Marcador), source: this));
    }

    /// <summary>
    /// Enciende el realce de puntero en un boton del calendario. Solo color, sin
    /// levantarlo: son botones chicos y en rejilla, y moverlos seria puro ruido.
    /// </summary>
    private static Button ConRealce(Button boton, Color? colorEncima = null)
    {
        RealcePuntero.SetActivo(boton, true);
        RealcePuntero.SetElevacion(boton, 0);

        if (colorEncima is not null)
        {
            RealcePuntero.SetColorEncima(boton, colorEncima);
        }

        return boton;
    }

    private static Button BotonNavegacion(string texto) => ConRealce(new()
    {
        Text = texto,
        FontSize = 17,
        WidthRequest = 30,
        HeightRequest = 32,
        Padding = 0,
        BackgroundColor = Colors.Transparent,
        TextColor = Paleta.Acero
    }, Paleta.AzulTenue);

    private static Button BotonPie(string texto) => ConRealce(new()
    {
        Text = texto,
        FontSize = 12,
        HeightRequest = 30,
        Padding = new Thickness(10, 0),
        BackgroundColor = Colors.Transparent,
        TextColor = Paleta.Accion
    }, Paleta.AzulTenue);

    // ─── Enmascarado del texto ──────────────────────────────────────────────

    /// <summary>
    /// Deja solo digitos y coloca las barras. El usuario teclea 15031985 y ve
    /// 15/03/1985 sin escribir una sola barra.
    /// </summary>
    private void AlEscribir(object? emisor, TextChangedEventArgs e)
    {
        if (_reescribiendoTexto)
        {
            return;
        }

        var digitos = new string((e.NewTextValue ?? string.Empty).Where(char.IsDigit).Take(8).ToArray());
        var enmascarado = Enmascarar(digitos);

        if (enmascarado != e.NewTextValue)
        {
            _reescribiendoTexto = true;
            _entrada.Text = enmascarado;
            _entrada.CursorPosition = enmascarado.Length;
            _reescribiendoTexto = false;
        }

        // Con los ocho digitos puestos ya se puede confirmar sin esperar a que
        // el usuario salga del campo: la fecha queda disponible de inmediato.
        if (digitos.Length == 8)
        {
            Confirmar(enmascarado);
        }
        else
        {
            MostrarError(string.Empty);
        }
    }

    private static string Enmascarar(string digitos) => digitos.Length switch
    {
        0 => string.Empty,
        <= 2 => digitos,
        <= 4 => digitos[..2] + "/" + digitos[2..],
        _ => digitos[..2] + "/" + digitos[2..4] + "/" + digitos[4..]
    };

    private void AlSalirDelCampo(object? emisor, FocusEventArgs e) => Confirmar(_entrada.Text);

    private void AlPulsarEnter(object? emisor, EventArgs e) => Confirmar(_entrada.Text);

    /// <summary>
    /// Valida lo escrito y, si vale, lo publica. Si no vale, marca el error y
    /// deja el texto tal cual: no se borra lo que el usuario tecleo.
    /// </summary>
    private void Confirmar(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();

        if (limpio.Length == 0)
        {
            if (PermiteVacio)
            {
                MostrarError(string.Empty);
                if (Fecha is not null)
                {
                    Fecha = null;
                }
            }
            else
            {
                MostrarError("La fecha es obligatoria.");
            }

            return;
        }

        if (!DateTime.TryParseExact(limpio, Formato, CultureInfo.CurrentCulture,
                DateTimeStyles.None, out var fecha))
        {
            MostrarError("Fecha invalida. Escribala como dd/mm/aaaa.");
            return;
        }

        if (fecha.Date < FechaMinima.Date || fecha.Date > FechaMaxima.Date)
        {
            MostrarError("La fecha debe estar entre "
                + FechaMinima.ToString(Formato, CultureInfo.CurrentCulture) + " y "
                + FechaMaxima.ToString(Formato, CultureInfo.CurrentCulture) + ".");
            return;
        }

        MostrarError(string.Empty);

        if (Fecha?.Date != fecha.Date)
        {
            Fecha = fecha.Date;
        }
    }

    private void MostrarError(string mensaje)
    {
        _etiquetaError.Text = mensaje;
        _etiquetaError.IsVisible = mensaje.Length > 0;

        // El borde tambien avisa: el mensaje solo no se ve en un formulario largo.
        _marcoEntrada.Stroke = new SolidColorBrush(
            mensaje.Length > 0 ? Paleta.Sello : Paleta.LineaFuerte);
    }

    // ─── Cambios que vienen del enlace ──────────────────────────────────────

    private static void AlCambiarFechaDesdeAfuera(BindableObject objeto, object anterior, object nuevo)
    {
        if (objeto is not CampoFecha campo)
        {
            return;
        }

        var texto = nuevo is DateTime fecha
            ? fecha.ToString(Formato, CultureInfo.CurrentCulture)
            : string.Empty;

        // Solo se reescribe si de verdad cambio: asignar el mismo texto moveria
        // el cursor mientras el usuario escribe.
        if (campo._entrada.Text == texto)
        {
            return;
        }

        campo._reescribiendoTexto = true;
        campo._entrada.Text = texto;
        campo._reescribiendoTexto = false;
        campo.MostrarError(string.Empty);
    }

    private static void AlCambiarLimites(BindableObject objeto, object anterior, object nuevo)
    {
        if (objeto is CampoFecha campo && campo._marcoCalendario.IsVisible)
        {
            campo.RellenarSelectores();
            campo.DibujarDias();
        }
    }

    private static void AlCambiarHabilitado(BindableObject objeto, object anterior, object nuevo)
    {
        if (objeto is not CampoFecha campo)
        {
            return;
        }

        var habilitado = nuevo is true;
        campo._entrada.IsEnabled = habilitado;
        campo._botonCalendario.IsEnabled = habilitado;
        campo._marcoEntrada.BackgroundColor = habilitado ? Paleta.Superficie : Paleta.AceroTenue;

        if (!habilitado)
        {
            campo._marcoCalendario.IsVisible = false;
        }
    }

    // ─── Calendario ─────────────────────────────────────────────────────────

    private void AlPulsarIcono(object? emisor, EventArgs e)
    {
        if (_marcoCalendario.IsVisible)
        {
            _marcoCalendario.IsVisible = false;
            return;
        }

        // Se abre sobre el mes de la fecha capturada, o sobre el mes en curso
        // si todavia no hay ninguna.
        var referencia = Fecha ?? DateTime.Today;
        referencia = Recortar(referencia);
        _mesMostrado = new DateTime(referencia.Year, referencia.Month, 1);

        RellenarSelectores();
        DibujarDias();
        _marcoCalendario.IsVisible = true;
    }

    private DateTime Recortar(DateTime fecha)
    {
        if (fecha.Date < FechaMinima.Date) { return FechaMinima.Date; }
        if (fecha.Date > FechaMaxima.Date) { return FechaMaxima.Date; }
        return fecha.Date;
    }

    /// <summary>
    /// Rellena mes y año. Son desplegables y no flechas justamente para poder
    /// saltar de golpe a 1985 sin pasar por los cuatrocientos ochenta meses que
    /// hay en medio (CR-03).
    /// </summary>
    private void RellenarSelectores()
    {
        _rellenandoSelectores = true;
        try
        {
            var meses = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames
                .Where(m => !string.IsNullOrEmpty(m))
                .Select(m => char.ToUpper(m[0], CultureInfo.CurrentCulture) + m[1..])
                .ToList();

            if (_selectorMes.ItemsSource is not IList<string> mesesActuales || mesesActuales.Count != meses.Count)
            {
                _selectorMes.ItemsSource = meses;
            }

            var anos = Enumerable
                .Range(FechaMinima.Year, Math.Max(1, FechaMaxima.Year - FechaMinima.Year + 1))
                .ToList();

            if (_selectorAno.ItemsSource is not IList<int> anosActuales
                || anosActuales.Count != anos.Count
                || (anosActuales.Count > 0 && anosActuales[0] != anos[0]))
            {
                _selectorAno.ItemsSource = anos;
            }

            _selectorMes.SelectedIndex = _mesMostrado.Month - 1;
            _selectorAno.SelectedIndex = _mesMostrado.Year - FechaMinima.Year;
        }
        finally
        {
            _rellenandoSelectores = false;
        }
    }

    private void AlCambiarMesOAno(object? emisor, EventArgs e)
    {
        if (_rellenandoSelectores || _selectorMes.SelectedIndex < 0 || _selectorAno.SelectedIndex < 0)
        {
            return;
        }

        var ano = FechaMinima.Year + _selectorAno.SelectedIndex;
        var mes = _selectorMes.SelectedIndex + 1;

        _mesMostrado = new DateTime(ano, mes, 1);
        DibujarDias();
    }

    private void MoverMes(int meses)
    {
        var destino = _mesMostrado.AddMonths(meses);

        // Se frena en los extremos. Sin esto, pulsar "‹" en enero del primer año
        // deja el desplegable de año en un indice que no existe.
        var primerMes = new DateTime(FechaMinima.Year, FechaMinima.Month, 1);
        var ultimoMes = new DateTime(FechaMaxima.Year, FechaMaxima.Month, 1);

        if (destino < primerMes) { destino = primerMes; }
        if (destino > ultimoMes) { destino = ultimoMes; }

        if (destino == _mesMostrado)
        {
            return;
        }

        _mesMostrado = destino;
        RellenarSelectores();
        DibujarDias();
    }

    /// <summary>Dibuja la cabecera de dias de la semana y la rejilla del mes.</summary>
    private void DibujarDias()
    {
        _rejillaDias.Children.Clear();

        var formato = CultureInfo.CurrentCulture.DateTimeFormat;
        var primerDiaSemana = (int)formato.FirstDayOfWeek;

        for (var columna = 0; columna < 7; columna++)
        {
            var dia = (DayOfWeek)((primerDiaSemana + columna) % 7);
            var nombre = formato.AbbreviatedDayNames[(int)dia];

            var etiqueta = new Label
            {
                Text = nombre.Length > 0
                    ? char.ToUpper(nombre[0], CultureInfo.CurrentCulture).ToString()
                    : string.Empty,
                FontSize = 10,
                TextColor = Paleta.AceroSuave,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            _rejillaDias.Add(etiqueta, columna, 0);
        }

        var desplazamiento = ((int)_mesMostrado.DayOfWeek - primerDiaSemana + 7) % 7;
        var diasDelMes = DateTime.DaysInMonth(_mesMostrado.Year, _mesMostrado.Month);

        for (var dia = 1; dia <= diasDelMes; dia++)
        {
            var fecha = new DateTime(_mesMostrado.Year, _mesMostrado.Month, dia);
            var posicion = desplazamiento + dia - 1;
            var fila = posicion / 7 + 1;
            var columna = posicion % 7;

            if (fila > 6)
            {
                break;
            }

            var dentroDeRango = fecha.Date >= FechaMinima.Date && fecha.Date <= FechaMaxima.Date;
            var esElegida = Fecha?.Date == fecha.Date;
            var esHoy = fecha.Date == DateTime.Today;

            var boton = new Button
            {
                Text = dia.ToString(CultureInfo.CurrentCulture),
                FontSize = 12,
                Padding = 0,
                HeightRequest = 28,
                WidthRequest = 32,
                CornerRadius = 4,
                IsEnabled = dentroDeRango,
                BackgroundColor = esElegida ? Paleta.Accion
                    : esHoy ? Paleta.AzulTenue
                    : Colors.Transparent,
                TextColor = !dentroDeRango ? Paleta.Linea
                    : esElegida ? Colors.White
                    : Paleta.Texto
            };

            // El dia elegido ya esta en azul: ese realce lo calcula solo el efecto
            // aclarando su propio fondo. Los demas se tiñen de azul tenue.
            ConRealce(boton, esElegida ? null : Paleta.AzulTenue);

            // La fecha se captura afuera del cierre para no depender de la
            // variable del bucle.
            var elegida = fecha;
            boton.Clicked += (_, _) => ElegirDia(elegida);

            _rejillaDias.Add(boton, columna, fila);
        }
    }

    private void ElegirDia(DateTime fecha)
    {
        var recortada = Recortar(fecha);

        _reescribiendoTexto = true;
        _entrada.Text = recortada.ToString(Formato, CultureInfo.CurrentCulture);
        _reescribiendoTexto = false;

        MostrarError(string.Empty);
        Fecha = recortada;
        _marcoCalendario.IsVisible = false;
    }

    private void Limpiar()
    {
        if (!PermiteVacio)
        {
            return;
        }

        _reescribiendoTexto = true;
        _entrada.Text = string.Empty;
        _reescribiendoTexto = false;

        MostrarError(string.Empty);
        Fecha = null;
        _marcoCalendario.IsVisible = false;
    }
}
