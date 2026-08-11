using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Controls
{
    public class SquarePanel : Panel
    {
        #region Dependency Properties
        public static readonly DependencyProperty MaxSizeProperty =
            DependencyProperty.Register(
                nameof(MaxSize),
                typeof(double),
                typeof(SquarePanel),
                new FrameworkPropertyMetadata(double.PositiveInfinity));
        public double MaxSize
        {
            get => (double)GetValue(MaxSizeProperty);
            set => SetValue(MaxSizeProperty, value);
        }
        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            double size = Math.Min(
            Math.Min(availableSize.Width, availableSize.Height),
            MaxSize);

            var desired = new Size(size, size);

            foreach (UIElement child in InternalChildren)
                child.Measure(desired);

            return desired;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            double size = Math.Min(
                Math.Min(finalSize.Width, finalSize.Height),
                MaxSize);

            var rect = new Rect(
                (finalSize.Width - size) / 2,
                (finalSize.Height - size) / 2,
                size,
                size);

            foreach (UIElement child in InternalChildren)
                child.Arrange(rect);

            return finalSize;
        }
    }
}
