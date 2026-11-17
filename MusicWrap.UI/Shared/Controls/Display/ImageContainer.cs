using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Controls
{
    public class ImageContainer : Control
    {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                nameof(Source),
                typeof(ImageSource),
                typeof(ImageContainer),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsRender
                    ));

        public ImageSource? Source
        {
            get => (ImageSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(ImageContainer),
                new FrameworkPropertyMetadata(
                    new CornerRadius(8),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(
                nameof(Stretch),
                typeof(Stretch),
                typeof(ImageContainer),
                new FrameworkPropertyMetadata(
                    Stretch.Uniform,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public static readonly DependencyProperty CollapseToAxisProperty =
            DependencyProperty.Register(
                nameof(CollapseToAxis),
                typeof(ImageCollapseAxis),
                typeof(ImageContainer),
                new FrameworkPropertyMetadata(
                    ImageCollapseAxis.None,
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public ImageCollapseAxis CollapseToAxis
        {
            get => (ImageCollapseAxis)GetValue(CollapseToAxisProperty);
            set => SetValue(CollapseToAxisProperty, value);
        }
        #region Callbacks

        #endregion
        #region Life Cycle
        protected override Size MeasureOverride(Size availableSize)
        {
            if (Source is null || Source.Width <= 1 || Source.Height <= 1)
            {
                return new Size(0, 0);
            }

            Debug.WriteLine($"MeasureOverride: availableSize={availableSize}, Source={Source.Width}x{Source.Height}");

            var sourceSize = new Size(Source.Width, Source.Height);

            return CollapseToAxis switch
            {
                ImageCollapseAxis.Horizontal => MessureFromWidth(sourceSize, availableSize),
                ImageCollapseAxis.Vertical => MesureFromHeight(sourceSize, availableSize),
                _ => new Size(
                    Math.Min(sourceSize.Width, availableSize.Width),
                    Math.Min(sourceSize.Height, availableSize.Height)
                )
            };
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (Source is null)
                return;

            var sourceSize = new Size(Source.Width, Source.Height);
            var availableSize = RenderSize;

            if (sourceSize.Width <= 0 ||
                sourceSize.Height <= 0 ||
                availableSize.Width <= 0 ||
                availableSize.Height <= 0)
            {
                return;
            }

            var scaleX = availableSize.Width / sourceSize.Width;
            var scaleY = availableSize.Height / sourceSize.Height;

            var scale = Stretch switch
            {
                Stretch.None => 1.0,
                Stretch.Fill => double.NaN,
                Stretch.Uniform => Math.Min(scaleX, scaleY),
                Stretch.UniformToFill => Math.Max(scaleX, scaleY),
                _ => Math.Min(scaleX, scaleY)
            };

            Rect imageRect;

            if (Stretch == Stretch.Fill)
            {
                imageRect = new Rect(
                    0,
                    0,
                    availableSize.Width,
                    availableSize.Height);
            }
            else
            {
                var width = sourceSize.Width * scale;
                var height = sourceSize.Height * scale;

                imageRect = new Rect(
                    (availableSize.Width - width) / 2,
                    (availableSize.Height - height) / 2,
                    width,
                    height);
            }

            var radius = CornerRadius.TopLeft;

            var geometry = new RectangleGeometry(
                imageRect,
                radius,
                radius);

            dc.PushClip(geometry);

            dc.DrawImage(Source, imageRect);

            dc.Pop();

            if (BorderThickness.Left <= 0 &&
                BorderThickness.Top <= 0 &&
                BorderThickness.Right <= 0 &&
                BorderThickness.Bottom <= 0)
            {
                return;
            }

            var thickness = Math.Max(
                Math.Max(BorderThickness.Left, BorderThickness.Right),
                Math.Max(BorderThickness.Top, BorderThickness.Bottom));

            if (thickness <= 0)
                return;

            var pen = new Pen(BorderBrush, thickness);

            var halfThickness = thickness / 2;

            var borderRect = new Rect(
                imageRect.X + halfThickness,
                imageRect.Y + halfThickness,
                Math.Max(0, imageRect.Width - thickness),
                Math.Max(0, imageRect.Height - thickness));

            var borderGeometry = new RectangleGeometry(
                borderRect,
                Math.Max(0, radius - halfThickness),
                Math.Max(0, radius - halfThickness));

            dc.DrawGeometry(null, pen, borderGeometry);
        }
        #endregion
        #region Internal
        private static Size MessureFromWidth(Size sourceSize, Size availableSize)
        {
            var width = Math.Min(sourceSize.Width, availableSize.Width);
            var height = width * sourceSize.Height / sourceSize.Width;

            if (!double.IsInfinity(availableSize.Height) &&
                height > availableSize.Height)
            {
                height = availableSize.Height;
                width = height * sourceSize.Width / sourceSize.Height;
            }

            return new Size(width, height);
        }
        private static Size MesureFromHeight(Size sourceSize, Size availableSize)
        {
            var height = Math.Min(sourceSize.Height, availableSize.Height);
            var width = height * sourceSize.Width / sourceSize.Height;

            if (!double.IsInfinity(availableSize.Width) &&
                width > availableSize.Width)
            {
                width = availableSize.Width;
                height = width * sourceSize.Height / sourceSize.Width;
            }

            return new Size(width, height);
        }
        #endregion
    }

    public enum ImageCollapseAxis
    {
        None,
        Horizontal,
        Vertical
    }
}
