using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
                    FrameworkPropertyMetadataOptions.AffectsRender));

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

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Source is null)
                return new Size(0, 0);

            var size = new Size(Source.Width, Source.Height);

            return new Size(
                Math.Min(size.Width, availableSize.Width),
                Math.Min(size.Height, availableSize.Height));
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
    }
}
