
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
                new UIPropertyMetadata(0.65, PixelShaderConstantCallback(1)));

        public double Strength
        {
            get => (double)GetValue(StrengthProperty);
            set => SetValue(StrengthProperty, value);
        }



        public static readonly DependencyProperty TintColorProperty =
            DependencyProperty.Register(
                nameof(TintColor),
                typeof(Color),
                typeof(FluidBackdropEffect),
                new UIPropertyMetadata(
                    Colors.Transparent,
                    PixelShaderConstantCallback(2)));

        public Color TintColor
        {
            get => (Color)GetValue(TintColorProperty);
            set => SetValue(TintColorProperty, value);
        }



        public static readonly DependencyProperty AcrylicParametersProperty =
            DependencyProperty.Register(
                nameof(AcrylicParameters),
                typeof(Point4D),
                typeof(FluidBackdropEffect),
                new UIPropertyMetadata(
                    // x = Tint opacity
                    // y = Tint luminosity opacity
                    // z = 1: use TintLuminosityOpacity, 0: auto
                    // w = Global Opacity of the noise
                    new Point4D(
                        0.145,
                        0.775,
                        0.0,
                        0.05),
                    PixelShaderConstantCallback(3)));

        public Point4D AcrylicParameters
        {
            get => (Point4D)GetValue(AcrylicParametersProperty);
            set => SetValue(AcrylicParametersProperty, value);
        }

        public static readonly DependencyProperty SeedsProperty =
            DependencyProperty.Register(
                nameof(Seeds),
                typeof(Point4D),
                typeof(FluidBackdropEffect),
                new UIPropertyMetadata(
                    // random seeds
                    new Point4D(0.0, 0.0, 0.0, 0.0),
                    PixelShaderConstantCallback(4)));

        public Point4D Seeds
        {
            get => (Point4D)GetValue(SeedsProperty);
            set => SetValue(SeedsProperty, value);
        }

        #endregion


        public FluidBackdropEffect()
        {
            PixelShader = FluidShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TimeProperty);
            UpdateShaderValue(StrengthProperty);
            UpdateShaderValue(TintColorProperty);
            UpdateShaderValue(AcrylicParametersProperty);
            UpdateShaderValue(SeedsProperty);
        }

        public void StartAnimation()
        {
            double duration;
            Point4D seeds;

            lock (Random)
            {
                duration = Random.NextDouble() * 30.0 + 30.0;

                seeds = new Point4D(
                    Random.NextDouble(),
                    Random.NextDouble(),
                    Random.NextDouble(),
                    0.0);
            }

            SetCurrentValue(SeedsProperty, seeds);

            BeginAnimation(
                TimeProperty,
                new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(duration),
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true
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
