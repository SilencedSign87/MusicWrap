
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace MusicWrap.UI.Shaders
{
    public sealed class FluidBackdropEffect : ShaderEffect
    {
        private static readonly PixelShader FluidShader = new()
        {
            UriSource = new Uri(
                "pack://application:,,,/Shaders/AmbientBackdrop.ps",
                UriKind.Absolute)
        };

        private static readonly Random Random = new();

        #region Dependency Properties

        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty(
                nameof(Input),
                typeof(FluidBackdropEffect),
                0);

        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }


        public static readonly DependencyProperty TimeProperty =
            DependencyProperty.Register(
                nameof(Time),
                typeof(double),
                typeof(FluidBackdropEffect),
                new UIPropertyMetadata(
                    0.0,
                    PixelShaderConstantCallback(0)));

        public double Time
        {
            get => (double)GetValue(TimeProperty);
            set => SetValue(TimeProperty, value);
        }


        public static readonly DependencyProperty StrengthProperty =
            DependencyProperty.Register(
                nameof(Strength),
                typeof(double),
                typeof(FluidBackdropEffect),
                new UIPropertyMetadata(1.0, PixelShaderConstantCallback(1)));

        public double Strength
        {
            get => (double)GetValue(StrengthProperty);
            set => SetValue(StrengthProperty, value);
        }


        public static readonly DependencyProperty OverlayColorProperty =
            DependencyProperty.Register(
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


        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(
                nameof(Parameters),
                typeof(Point4D),
                typeof(FluidBackdropEffect),
                new UIPropertyMetadata(
                    // x = Tint opacity
                    // y = Saturation
                    // z = Contrast
                    // w = Noise intensity
                    new Point4D(
                        0.50,
                        1.20,
                        1.10,
                        0.025),
                    PixelShaderConstantCallback(3)));

        public Point4D Parameters
        {
            get => (Point4D)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        #endregion


        public FluidBackdropEffect()
        {
            PixelShader = FluidShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TimeProperty);
            UpdateShaderValue(StrengthProperty);
            UpdateShaderValue(OverlayColorProperty);
            UpdateShaderValue(ParametersProperty);
        }

        public void StartAnimation()
        {
            double duration;

            lock (Random)
            {
                duration = Random.NextDouble() * 20.0 + 20.0;
            }

            BeginAnimation(
                TimeProperty,
                new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(duration),
                    RepeatBehavior = RepeatBehavior.Forever
                },
                HandoffBehavior.SnapshotAndReplace);
        }

        public void StopAnimation()
        {
            double currentTime = Time;

            BeginAnimation(TimeProperty, null);
            SetCurrentValue(TimeProperty, currentTime);
        }
    }
}
