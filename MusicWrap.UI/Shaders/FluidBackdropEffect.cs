using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace MusicWrap.UI.Shaders
{
    public sealed class FluidBackdropEffect : ShaderEffect
    {
        private static readonly PixelShader Shader = new()
        {
            UriSource = new Uri("pack://application:,,,/Shaders/AmbientBackdrop.ps", UriKind.Absolute)
        };

        public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(
            nameof(Input),
            typeof(FluidBackdropEffect),
            0);

        public static readonly DependencyProperty TimeProperty =
            DependencyProperty.Register(
                nameof(Time),
                typeof(double),
                typeof(FluidBackdropEffect),
                new UIPropertyMetadata(
                    0.0,
                    PixelShaderConstantCallback(0)));

        public static readonly DependencyProperty StrengthProperty =
            DependencyProperty.Register(
                nameof(Strength),
                typeof(double),
                typeof(FluidBackdropEffect),
                new UIPropertyMetadata(
                    1.0,
                    PixelShaderConstantCallback(1)));

        public static readonly DependencyProperty OverlayColorProperty = DependencyProperty.Register(
            nameof(OverlayColor),
            typeof(Color),
            typeof(FluidBackdropEffect),
            new UIPropertyMetadata(
                Colors.Transparent,
                PixelShaderConstantCallback(2)));

        public Color OverlayColor
        {
            get => (Color)GetValue(OverlayColorProperty);
            set => SetValue(OverlayColorProperty, value);
        }

        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public double Time
        {
            get => (double)GetValue(TimeProperty);
            set => SetValue(TimeProperty, value);
        }

        public double Strength
        {
            get => (double)GetValue(StrengthProperty);
            set => SetValue(StrengthProperty, value);
        }

        public FluidBackdropEffect()
        {
            PixelShader = Shader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TimeProperty);
            UpdateShaderValue(StrengthProperty);
            UpdateShaderValue(OverlayColorProperty);
        }

        public void StartAnimation()
        {
            double randomDuration = new Random().NextDouble() * 20 + 20;

            BeginAnimation(
                TimeProperty,
                new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(randomDuration),
                    RepeatBehavior = RepeatBehavior.Forever
                });
        }

        public void StopAnimation()
        {
            BeginAnimation(TimeProperty, null);
        }
    }
}
