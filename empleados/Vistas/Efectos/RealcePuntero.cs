using empleados.Servicios;

namespace empleados.Vistas.Efectos;

/// <summary>
/// Realce al pasar el puntero por encima de un control pulsable.
///
/// Windows no da ninguna respuesta visual a los botones de MAUI: el color se
/// queda igual pase o no el raton por encima, y una pantalla llena de botones
/// que no reaccionan se siente muerta. Este efecto agrega lo que falta:
///
///   • El fondo viaja hasta el color de realce en 140 ms.
///   • El control se levanta dos pixeles, y se hunde uno al pulsarlo.
///   • Al salir el puntero vuelve exactamente a como estaba.
///
/// Se engancha desde el estilo, no desde cada boton:
///
///     &lt;Setter Property="ef:RealcePuntero.Activo" Value="True" /&gt;
///     &lt;Setter Property="ef:RealcePuntero.ColorEncima" Value="{StaticResource AzulClaro}" /&gt;
///
/// Por que propiedades adjuntas y no un Behavior: un Setter guarda UNA sola
/// instancia del valor y la repartiria entre todos los botones que usen el
/// estilo, el mismo motivo por el que StrokeShape no vive en el estilo de
/// Tarjeta. Una propiedad adjunta no comparte nada: su manejador corre una vez
/// por elemento y cada uno se queda con su propio reconocedor y su propio color.
///
/// Sobre la regla 11 de CLAUDE.md ("sin logica en el codigo subyacente"): esto
/// no es codigo subyacente de ninguna pantalla sino un efecto de presentacion
/// reutilizable. No consulta la base, no conoce entidades y no decide nada del
/// negocio; solo mueve un fondo y dos pixeles.
/// </summary>
public static class RealcePuntero
{
    /// <summary>Un solo nombre para las tres transiciones: la nueva cancela la anterior.</summary>
    private const string NombreAnimacion = "RealcePuntero";

    private const uint DuracionEntrada = 140;
    private const uint DuracionSalida = 110;
    private const uint DuracionPulsacion = 70;

    /// <summary>Cuanto se hunde el control mientras esta pulsado, en pixeles.</summary>
    private const double Hundimiento = 1;

    /// <summary>Se anota el primer fallo y nada mas: un efecto de puntero no puede llenar el archivo.</summary>
    private static bool _yaSeAnotoUnFallo;

    // ─── Propiedades adjuntas ───────────────────────────────────────────────

    /// <summary>Engancha o desengancha el realce en el control.</summary>
    public static readonly BindableProperty ActivoProperty = BindableProperty.CreateAttached(
        "Activo", typeof(bool), typeof(RealcePuntero), false, propertyChanged: AlCambiarActivo);

    /// <summary>
    /// Color del fondo mientras el puntero esta encima. Si no se declara se
    /// calcula solo: los fondos claros se oscurecen y los oscuros se aclaran.
    /// Hay que declararlo siempre que el fondo de reposo sea transparente,
    /// porque de un color invisible no se puede deducir a donde ir.
    /// </summary>
    public static readonly BindableProperty ColorEncimaProperty = BindableProperty.CreateAttached(
        "ColorEncima", typeof(Color), typeof(RealcePuntero), null);

    /// <summary>Pixeles que sube el control. Cero lo deja quieto y solo le cambia el color.</summary>
    public static readonly BindableProperty ElevacionProperty = BindableProperty.CreateAttached(
        "Elevacion", typeof(double), typeof(RealcePuntero), 2d);

    public static bool GetActivo(BindableObject objeto) => (bool)objeto.GetValue(ActivoProperty);

    public static void SetActivo(BindableObject objeto, bool valor) => objeto.SetValue(ActivoProperty, valor);

    public static Color? GetColorEncima(BindableObject objeto) => (Color?)objeto.GetValue(ColorEncimaProperty);

    public static void SetColorEncima(BindableObject objeto, Color? valor) => objeto.SetValue(ColorEncimaProperty, valor);

    public static double GetElevacion(BindableObject objeto) => (double)objeto.GetValue(ElevacionProperty);

    public static void SetElevacion(BindableObject objeto, double valor) => objeto.SetValue(ElevacionProperty, valor);

    // ─── Estado por control ─────────────────────────────────────────────────

    /// <summary>
    /// Lo que hay que recordar de cada control realzado. Vive en una propiedad
    /// adjunta privada para que cada elemento tenga el suyo y se vaya con el.
    /// </summary>
    private sealed class Estado(PointerGestureRecognizer puntero)
    {
        public PointerGestureRecognizer Puntero { get; } = puntero;

        public EventHandler? AlDescargar { get; set; }

        /// <summary>Fondo de reposo. Se toma al entrar el puntero, no antes (ver AlEntrar).</summary>
        public Color? ColorBase { get; set; }

        public bool Encima { get; set; }
    }

    private static readonly BindableProperty EstadoProperty = BindableProperty.CreateAttached(
        "Estado", typeof(Estado), typeof(RealcePuntero), null);

    // ─── Enganche ───────────────────────────────────────────────────────────

    private static void AlCambiarActivo(BindableObject objeto, object anterior, object nuevo)
    {
        // View y no VisualElement: los reconocedores de gestos viven en View.
        if (objeto is not View elemento)
        {
            return;
        }

        var estado = (Estado?)elemento.GetValue(EstadoProperty);

        if (nuevo is true)
        {
            if (estado is not null)
            {
                return;
            }

            var puntero = new PointerGestureRecognizer();
            puntero.PointerEntered += (_, _) => AlEntrar(elemento);
            puntero.PointerExited += (_, _) => AlSalir(elemento);
            puntero.PointerPressed += (_, _) => AlPulsar(elemento);
            puntero.PointerReleased += (_, _) => AlEntrar(elemento);

            estado = new Estado(puntero);

            // Un boton dentro de un CollectionView se recicla para otra fila. Si
            // eso pasa mientras esta levantado, la fila nueva heredaria el realce
            // porque el puntero nunca llego a salir: al descargarse se reposa.
            estado.AlDescargar = (_, _) => Reposar(elemento, estado);
            elemento.Unloaded += estado.AlDescargar;

            elemento.SetValue(EstadoProperty, estado);
            elemento.GestureRecognizers.Add(puntero);
        }
        else
        {
            if (estado is null)
            {
                return;
            }

            elemento.GestureRecognizers.Remove(estado.Puntero);

            if (estado.AlDescargar is not null)
            {
                elemento.Unloaded -= estado.AlDescargar;
            }

            Reposar(elemento, estado);
            elemento.SetValue(EstadoProperty, null);
        }
    }

    // ─── Reacciones del puntero ─────────────────────────────────────────────

    /// <remarks>
    /// Los eventos del puntero llegan ya en el hilo de interfaz, asi que no hace
    /// falta el Dispatcher de la regla 5: aca no hay ninguna tarea de fondo.
    /// </remarks>
    private static void AlEntrar(View elemento)
    {
        if (elemento.GetValue(EstadoProperty) is not Estado estado || !elemento.IsEnabled)
        {
            return;
        }

        if (!estado.Encima)
        {
            // El fondo se lee ahora y no al enganchar: cuando se engancha, el
            // estilo todavia puede no haber aplicado su BackgroundColor.
            estado.ColorBase = elemento.BackgroundColor;
            estado.Encima = true;
        }

        Animar(elemento, ColorDeRealce(elemento, estado), -GetElevacion(elemento), DuracionEntrada);
    }

    private static void AlSalir(View elemento)
    {
        if (elemento.GetValue(EstadoProperty) is not Estado estado || !estado.Encima)
        {
            return;
        }

        estado.Encima = false;
        Animar(elemento, estado.ColorBase ?? Colors.Transparent, 0, DuracionSalida);
    }

    private static void AlPulsar(View elemento)
    {
        if (elemento.GetValue(EstadoProperty) is not Estado estado || !elemento.IsEnabled)
        {
            return;
        }

        // Se hunde por debajo del reposo: es la contraparte del levantado y da la
        // sensacion de que el boton acepta la pulsacion.
        Animar(elemento, ColorDeRealce(elemento, estado), Hundimiento, DuracionPulsacion);
    }

    /// <summary>Devuelve el control a su sitio de golpe, sin transicion.</summary>
    private static void Reposar(View elemento, Estado estado)
    {
        elemento.AbortAnimation(NombreAnimacion);

        if (estado.ColorBase is not null)
        {
            elemento.BackgroundColor = estado.ColorBase;
        }

        elemento.TranslationY = 0;
        estado.Encima = false;
    }

    // ─── Transicion ─────────────────────────────────────────────────────────

    private static void Animar(View elemento, Color colorDestino, double desplazamiento, uint duracion)
    {
        try
        {
            elemento.AbortAnimation(NombreAnimacion);

            var colorDesde = elemento.BackgroundColor ?? Colors.Transparent;
            var desdeY = elemento.TranslationY;

            // Sin handler no hay reloj de animacion. Pasa mientras la pantalla se
            // esta cerrando: se asignan los valores finales y se acabo.
            if (elemento.Handler is null)
            {
                elemento.BackgroundColor = colorDestino;
                elemento.TranslationY = desplazamiento;
                return;
            }

            var animacion = new Animation();

            animacion.Add(0, 1, new Animation(
                avance => elemento.BackgroundColor = Mezclar(colorDesde, colorDestino, avance), 0, 1));

            if (Math.Abs(desdeY - desplazamiento) > 0.01)
            {
                animacion.Add(0, 1, new Animation(
                    valor => elemento.TranslationY = valor, desdeY, desplazamiento));
            }

            animacion.Commit(elemento, NombreAnimacion, 16, duracion, Easing.CubicOut);
        }
        catch (Exception ex)
        {
            // Que falle un realce no puede cerrar la aplicacion. Se deja el
            // control en reposo y se sigue; el fallo queda anotado una sola vez.
            elemento.TranslationY = 0;

            if (!_yaSeAnotoUnFallo)
            {
                _yaSeAnotoUnFallo = true;
                RegistroEmergencia.Escribir("RealcePuntero no pudo animar el control.", ex);
            }
        }
    }

    /// <summary>
    /// Color de llegada: el declarado en el estilo o, si no hay, el mismo fondo
    /// un paso mas claro o mas oscuro segun lo claro que sea.
    /// </summary>
    private static Color ColorDeRealce(View elemento, Estado estado)
    {
        var declarado = GetColorEncima(elemento);
        if (declarado is not null)
        {
            return declarado;
        }

        var fondo = estado.ColorBase ?? Colors.Transparent;

        // De un fondo transparente no se deduce nada: no se sabe de que color es
        // lo que hay detras. Se usa un velo negro muy tenue, que sirve sobre
        // cualquier superficie clara.
        if (fondo.Alpha < 0.05f)
        {
            return new Color(0f, 0f, 0f, 0.06f);
        }

        return fondo.GetLuminosity() > 0.5f
            ? fondo.AddLuminosity(-0.05f)
            : fondo.AddLuminosity(0.09f);
    }

    /// <summary>
    /// Interpola dos colores. Cuando uno de los dos es transparente se toma el
    /// otro con alfa cero: sin esto, el paso por el negro mete un gris sucio a
    /// mitad de la transicion.
    /// </summary>
    private static Color Mezclar(Color desde, Color hasta, double avance)
    {
        if (desde.Alpha < 0.001f)
        {
            desde = hasta.WithAlpha(0);
        }
        else if (hasta.Alpha < 0.001f)
        {
            hasta = desde.WithAlpha(0);
        }

        var t = (float)avance;

        return new Color(
            desde.Red + ((hasta.Red - desde.Red) * t),
            desde.Green + ((hasta.Green - desde.Green) * t),
            desde.Blue + ((hasta.Blue - desde.Blue) * t),
            desde.Alpha + ((hasta.Alpha - desde.Alpha) * t));
    }
}
